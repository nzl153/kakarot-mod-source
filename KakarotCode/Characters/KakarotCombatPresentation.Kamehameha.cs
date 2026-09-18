using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace KakarotMod.KakarotCode.Characters;

// 龟波气功表现层：处理蓄力姿势、发射点、最远目标定位、光束资源回退和程序化表现。
// 群体光束长度按当前朝向一侧的最远敌人计算，避免在前排目标处提前截断。
public static partial class KakarotCombatPresentation
{
    // Retain original textures so interrupted pose swaps can be restored.
    private static readonly Dictionary<ulong, Texture2D> KamehamehaPoseOriginalTextures = new();

    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalPositions = new();

    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalScales = new();

    // KAKAROT_DRAGON_FIST 的显示名是“全力龟派气功”；类名参与存档 ID，不能重命名。
    // 真正的龙拳爆发对应 KakarotDragonFistBurst。
    private static readonly HashSet<string> KamehamehaPoseCardIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_TENFOLD_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_FATHER_SON_WAVE",
        "KAKAROTMOD-KAKAROT_DRAGON_FIST",
    };

    // 光束配色。未列出的卡使用蓝色默认值。
    private static readonly Color KamehamehaBeamColorDefault = new(0.25f, 0.62f, 1.0f);

    private static readonly Color KamehamehaCoreColorDefault = new(0.88f, 0.97f, 1.0f);

    private static readonly Dictionary<string, (Color Beam, Color Core)> KamehamehaBeamPalette =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["KAKAROTMOD-KAKAROT_TENFOLD_KAMEHAMEHA"] = (new Color(1.0f, 0.12f, 0.05f), new Color(1.0f, 0.75f, 0.45f)),
        };

    private const float KamehamehaPoseDurationSeconds = 0.55f;

    private const float KamehamehaPoseScaleMultiplier = 1.12f;

    private const string KamehamehaBeamTexturePath = "res://Kakarot/Images/Vfx/kamehameha_beam.png";

    // 使用已导入的 shader 路径；未生成 .uid 的新资源可能无法被 ResourceLoader.Exists 发现并触发回退。
    private const string KamehamehaBeamShaderPath = "res://Kakarot/Shaders/kakarot_kamehameha_beam.gdshader";

    // 调试开关：true 使用程序化 shader，false 回退到贴图/场景路径。
    private const bool KamehamehaUseShaderBeam = true;

    // 画布高度决定可用辉光空间，实际束宽由 shader thickness 比例控制。
    // 调整整体粗细优先改此值，避免 thickness 过大导致画布被完全填满。
    private const float KamehamehaShaderBeamThickness = 900f;

    private const float KamehamehaShaderBeamGrowSeconds = 0.26f;

    private const float KamehamehaShaderBeamHoldSeconds = 0.34f;

    private const float KamehamehaShaderBeamFadeSeconds = 0.30f;

    // 手心气团直径相对光束根部宽度计算，需明显大于根部以保持球体轮廓。
    private const float KamehamehaMuzzleOrbScale = 2.6f;

    // 椭圆拉伸提供沿光束方向的流向感。
    private const float KamehamehaMuzzleOrbStretch = 1.55f;

    // 向前压入主干以隐藏气团与光束接缝。
    private const float KamehamehaMuzzleOrbPush = 0.22f;

    // 根部宽度换算需与 shader 默认 neck_width 和 thickness 保持一致。
    private const float KamehamehaBeamNeckWidth = 0.46f;

    private const float KamehamehaBeamThicknessRatio = 0.52f;

    // 末端预留辉光空间，progress 相应缩短，避免波头被精灵边界裁切。
    private const float KamehamehaBeamLengthHeadroom = 1.10f;

    // 光束越过最远敌人后继续延伸，形成贯穿整排的视觉效果。
    private const float KamehamehaBeamOvershoot = 320f;

    // 场上无法解析敌人时的兜底长度。
    private const float KamehamehaBeamFallbackLength = 900f;

    private const string KamehamehaBeamPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_beam.tscn";

    private const string KamehamehaImpactPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_impact.tscn";

    private const float KamehamehaImpactDelaySeconds = 0.17f;

    private static readonly Vector2 KamehamehaBeamCharacterOffset = new(50f, -58f);

    // 各形态手心位置不同，仅列出偏离默认值的形态。
    private static readonly Dictionary<KakarotAuraForm, Vector2> KamehamehaBeamOffsetByForm = new()
    {
        [KakarotAuraForm.SuperSaiyan3] = new Vector2(105f, -58f),
        [KakarotAuraForm.SuperSaiyan4] = new Vector2(50f, -43f),
    };

    private static readonly Vector2 KamehamehaBeamEnemyOffset = new(0f, -36f);

    private const float KamehamehaBeamThickness = 0.34f;

    private const float KamehamehaBeamTextureThicknessScale = 0.18f;

    // Reentrant casts retain the first texture until the final pose timer restores it.
    private static void TrySwapToKamehamehaPose(Creature creature, Sprite2D staticModel)
    {
        try
        {
            if (creature == null || staticModel == null)
            {
                return;
            }

            var posePath = KakarotFormVisuals.ResolveKamehamehaPosePath(creature);
            if (string.IsNullOrEmpty(posePath))
            {
                return;
            }

            var poseTex = ResourceLoader.Load<Texture2D>(posePath);
            if (poseTex == null)
            {
                return;
            }

            var id = staticModel.GetInstanceId();
            if (!KamehamehaPoseOriginalTextures.ContainsKey(id))
            {
                KamehamehaPoseOriginalTextures[id] = staticModel.Texture;
            }
            if (!KamehamehaPoseOriginalScales.ContainsKey(id))
            {
                var rest = KakarotFormVisuals.GetRestTransform(creature);
                KamehamehaPoseOriginalPositions[id] = rest.Pos;
                KamehamehaPoseOriginalScales[id] = rest.Scale;
            }

            staticModel.Scale = KamehamehaPoseOriginalScales[id] * KamehamehaPoseScaleMultiplier;
            CrossfadeTexture(staticModel, poseTex, PoseCrossfadeSeconds);

            var tree = staticModel.GetTree();
            if (tree != null)
            {
                tree.CreateTimer(KamehamehaPoseDurationSeconds).Timeout += () =>
                {
                    TryRestoreFromKamehamehaPose(staticModel);
                };
            }
            else
            {
                TryRestoreFromKamehamehaPose(staticModel);
            }
        }
        catch
        {
        }
    }

    // Form changes call this before replacing the character texture.
    public static void TryRestoreFromKamehamehaPose(Sprite2D staticModel)
    {
        TryRestoreFromKamehamehaPose(staticModel, false);
    }

    public static void TryRestoreFromKamehamehaPose(Sprite2D staticModel, bool crossfade)
    {
        if (staticModel == null || !GodotObject.IsInstanceValid(staticModel))
        {
            return;
        }

        var id = staticModel.GetInstanceId();
        if (KamehamehaPoseOriginalTextures.TryGetValue(id, out var original))
        {
            try
            {
                if (crossfade)
                {
                    CrossfadeTexture(staticModel, original, PoseCrossfadeSeconds);
                }
                else
                {
                    staticModel.Texture = original;
                }
            }
            catch
            {
            }
            KamehamehaPoseOriginalTextures.Remove(id);
        }

        var hasOriginalPos = KamehamehaPoseOriginalPositions.TryGetValue(id, out var originalPos);
        if (KamehamehaPoseOriginalScales.TryGetValue(id, out var originalScale))
        {
            try
            {
                StopIdle(staticModel);
                if (hasOriginalPos)
                {
                    staticModel.Position = originalPos;
                }
                staticModel.Scale = originalScale;
                StartIdleBreathing(staticModel, hasOriginalPos ? originalPos : staticModel.Position, originalScale);
            }
            catch
            {
            }
            KamehamehaPoseOriginalPositions.Remove(id);
            KamehamehaPoseOriginalScales.Remove(id);
        }
    }

    // Beam scenes, textures, and generated light share the same hand-to-target geometry.
    private static void TryPlayKamehamehaBeamVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            Creature primaryTarget = ResolvePrimaryBeamTarget(player, cardPlay);

            // 群体技保持当前朝向；单体技按选中目标转身。
            if (cardPlay.Target != null)
            {
                KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
            }

            Vector2 beamOriginWorld = ComputeKamehamehaBeamOriginWorld(staticModel, player?.Creature);
            Vector2 beamEndWorld = ComputeKamehamehaBeamEndWorld(staticModel, primaryTarget, beamOriginWorld, player);

            bool spawned = false;

            if (ResourceLoader.Exists(KamehamehaBeamPackedScenePath))
            {
                var packed = ResourceLoader.Load<PackedScene>(KamehamehaBeamPackedScenePath);
                var instance = packed?.Instantiate<Node2D>();
                if (instance != null)
                {
                    anchor.AddChild(instance);
                    instance.GlobalPosition = beamOriginWorld;
                    instance.Rotation = (beamEndWorld - beamOriginWorld).Angle();
                    // Preserve the scene's direction convention; resource scale controls beam length.
                    float dir = staticModel.FlipH ? -1f : 1f;
                    instance.Scale = new Vector2(dir, 1f);

                    var beamSceneTree = instance.GetTree();
                    if (beamSceneTree != null)
                    {
                        beamSceneTree.CreateTimer(2.0).Timeout += () =>
                        {
                            if (GodotObject.IsInstanceValid(instance))
                            {
                                instance.QueueFree();
                            }
                        };
                    }
                    spawned = true;
                }
            }

            if (KamehamehaUseShaderBeam && !spawned)
            {
                var beamEntry = cardPlay.Card?.Id.Entry ?? string.Empty;
                if (!KamehamehaBeamPalette.TryGetValue(beamEntry, out var palette))
                {
                    palette = (KamehamehaBeamColorDefault, KamehamehaCoreColorDefault);
                }

                spawned = TryPlayKamehamehaBeamShader(anchor, beamOriginWorld, beamEndWorld, palette.Beam, palette.Core);
            }

            if (!spawned && ResourceLoader.Exists(KamehamehaBeamTexturePath))
            {
                var tex = ResourceLoader.Load<Texture2D>(KamehamehaBeamTexturePath);
                if (tex != null)
                {
                    TryPlayKamehamehaBeamFromTexture(anchor, beamOriginWorld, beamEndWorld, tex);
                    spawned = true;
                }
            }

            if (!spawned)
            {
                TryPlayKamehamehaBeamVfxProcedural(anchor, beamOriginWorld, beamEndWorld);
                spawned = true;
            }

            // 光束本体已提供清晰命中反馈，不额外叠加逐目标爆点，避免群体目标出现位置错觉和视觉干扰。
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Kamehameha beam failed: {ex}");
        }
    }

    private static Vector2 ComputeKamehamehaBeamOriginWorld(Sprite2D staticModel, Creature caster)
    {
        var offset = KamehamehaBeamCharacterOffset;
        if (caster != null
            && KamehamehaBeamOffsetByForm.TryGetValue(KakarotAuraFormResolver.ResolveCurrent(caster), out var formOffset))
        {
            offset = formOffset;
        }

        float dir = staticModel.FlipH ? -1f : 1f;
        return staticModel.GlobalPosition + new Vector2(offset.X * dir, offset.Y);
    }

    // 光束保持发射者手部高度；朝向按近敌判断，长度按同侧最远敌人判断，两者用途不同。
    private static Vector2 ComputeKamehamehaBeamEndWorld(
        Sprite2D staticModel, Creature primaryTarget, Vector2 beamOriginWorld, Player player = null)
    {
        float dir = staticModel.FlipH ? -1f : 1f;

        if (TryResolveFarthestBeamTargetX(player, staticModel, out float farthestX))
        {
            return new Vector2(farthestX + KamehamehaBeamOvershoot * dir, beamOriginWorld.Y);
        }

        // 无法解析全场目标时回退到主目标。
        if (primaryTarget != null && TryGetCreatureChestWorld(primaryTarget, out Vector2 enemyAnchor))
        {
            return new Vector2(enemyAnchor.X + KamehamehaBeamOvershoot * dir, beamOriginWorld.Y);
        }

        return beamOriginWorld + new Vector2(KamehamehaBeamFallbackLength * dir, 0f);
    }

    // 返回当前朝向一侧最远可命中敌人的世界 X；同侧无目标时回退到全场最远。
    private static bool TryResolveFarthestBeamTargetX(Player player, Sprite2D staticModel, out float farthestX)
    {
        farthestX = 0f;

        var combatState = player?.Creature?.CombatState;
        var room = NCombatRoom.Instance;
        if (combatState == null || room == null)
        {
            return false;
        }

        var selfNode = room.GetCreatureNode(player.Creature);
        if (selfNode == null)
        {
            return false;
        }

        float facing = staticModel.FlipH ? -1f : 1f;
        float selfX = selfNode.GlobalPosition.X;

        bool foundFacing = false;
        float bestFacingDx = float.MinValue;
        bool foundAny = false;
        float bestAnyDx = float.MinValue;
        float bestAnyX = 0f;

        foreach (Creature candidate in combatState.HittableEnemies)
        {
            if (candidate is not { CurrentHp: > 0 })
            {
                continue;
            }

            var node = room.GetCreatureNode(candidate);
            if (node == null)
            {
                continue;
            }

            float delta = node.GlobalPosition.X - selfX;
            float dx = Math.Abs(delta);

            if (dx > bestAnyDx)
            {
                bestAnyDx = dx;
                bestAnyX = node.GlobalPosition.X;
                foundAny = true;
            }

            // 同侧判定与 ResolvePrimaryBeamTarget 保持一致，此处仅改为取最远目标。
            if (delta * facing >= 0f && dx > bestFacingDx)
            {
                bestFacingDx = dx;
                farthestX = node.GlobalPosition.X;
                foundFacing = true;
            }
        }

        if (foundFacing)
        {
            return true;
        }

        if (foundAny)
        {
            farthestX = bestAnyX;
            return true;
        }

        return false;
    }

    private static bool TryGetCreatureChestWorld(Creature creature, out Vector2 world)
    {
        world = Vector2.Zero;
        if (creature == null)
        {
            return false;
        }

        var enemyNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (enemyNode?.Visuals is not Node2D enemyVisuals)
        {
            return false;
        }

        var enemyStatic = enemyVisuals.GetNodeOrNull<Sprite2D>("StaticModel");
        if (enemyStatic?.Texture != null)
        {
            // 使用贴图矩形中心而非精灵基点，使不同身高体型的落点保持在身体区域。
            Rect2 rect = enemyStatic.GetRect();
            world = enemyStatic.ToGlobal(rect.GetCenter()) + new Vector2(0f, -8f);
            return true;
        }

        Vector2 basePos = enemyStatic?.GlobalPosition ?? enemyVisuals.GlobalPosition;
        world = basePos + KamehamehaBeamEnemyOffset;
        return true;
    }

    // AOE beams aim at the median living enemy.
    private static Creature ResolvePrimaryBeamTarget(Player player, CardPlay cardPlay)
    {
        var combatState = player?.Creature?.CombatState;
        if (combatState == null)
        {
            return null;
        }

        bool alive(Creature c) => c is { CurrentHp: > 0 };

        if (cardPlay.Card.TargetType != TargetType.AllEnemies
            && cardPlay.Target != null
            && alive(cardPlay.Target)
            && combatState.HittableEnemies.Any(e => ReferenceEquals(e, cardPlay.Target)))
        {
            return cardPlay.Target;
        }

        List<Creature> enemies = combatState.HittableEnemies.Where(alive).ToList();
        if (enemies.Count == 0)
        {
            return null;
        }

        // 群体技的朝向逻辑使用最近敌人，因此表现层保持同源判断。
        var room = NCombatRoom.Instance;
        var selfNode = room?.GetCreatureNode(player.Creature);
        if (selfNode == null)
        {
            return enemies[0];
        }

        // 优先选择当前朝向一侧最近的敌人，同侧无目标时回退到全场最近。
        var casterModel = selfNode.Visuals?.GetNodeOrNull<Sprite2D>("StaticModel");
        float facing = casterModel != null && casterModel.FlipH ? -1f : 1f;
        float selfX = selfNode.GlobalPosition.X;

        Creature nearestFacing = null;
        float nearestFacingDx = float.MaxValue;
        Creature nearestAny = enemies[0];
        float nearestAnyDx = float.MaxValue;

        foreach (Creature candidate in enemies)
        {
            var candidateNode = room.GetCreatureNode(candidate);
            if (candidateNode == null)
            {
                continue;
            }

            float delta = candidateNode.GlobalPosition.X - selfX;
            float dx = Math.Abs(delta);

            if (dx < nearestAnyDx)
            {
                nearestAnyDx = dx;
                nearestAny = candidate;
            }

            if (delta * facing >= 0f && dx < nearestFacingDx)
            {
                nearestFacingDx = dx;
                nearestFacing = candidate;
            }
        }

        return nearestFacing ?? nearestAny;
    }

    private static bool TryPlayKamehamehaBeamShader(Node2D anchor, Vector2 originWorld, Vector2 endWorld, Color beamColor, Color coreColor)
    {
        return SpawnEnergyBeam(
            anchor,
            originWorld,
            endWorld,
            beamColor,
            coreColor,
            KamehamehaShaderBeamThickness,
            KamehamehaShaderBeamGrowSeconds,
            KamehamehaShaderBeamHoldSeconds,
            KamehamehaShaderBeamFadeSeconds,
            dark: false,
            muzzleOrbScale: KamehamehaMuzzleOrbScale);
    }

    private static void TryPlayKamehamehaBeamFromTexture(Node2D anchor, Vector2 originWorld, Vector2 endWorld, Texture2D texture)
    {
        var holder = new Node2D { Name = "KamehamehaBeamTexFx" };
        holder.ZIndex = 24;
        anchor.AddChild(holder);
        holder.GlobalPosition = originWorld;
        holder.Rotation = (endWorld - originWorld).Angle();

        var textureWidth = Math.Max(texture.GetWidth(), 1);
        var distance = originWorld.DistanceTo(endWorld);
        // Anchor the beam at its left edge so X scaling extends toward the target.
        var sprite = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            Offset = new Vector2(textureWidth * 0.5f, 0f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };

        float startLen = Math.Max(distance * 0.2f, 32f);
        float startScaleX = startLen / textureWidth;
        float endScaleX = distance / textureWidth;
        sprite.Scale = new Vector2(startScaleX, KamehamehaBeamTextureThicknessScale);
        holder.AddChild(sprite);

        var tween = holder.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "scale", new Vector2(endScaleX, KamehamehaBeamTextureThicknessScale * 2.1f), 0.22)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate:a", 0f, 0.48).SetDelay(0.12);

        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(holder))
            {
                holder.QueueFree();
            }
        };
    }

    // Layered beam sprites share one origin-to-target transform.
    private static void TryPlayKamehamehaBeamVfxProcedural(Node2D anchor, Vector2 originWorld, Vector2 endWorld)
    {
        var burst = new Node2D { Name = "KamehamehaBeamFx" };
        burst.ZIndex = 24;
        anchor.AddChild(burst);
        burst.GlobalPosition = originWorld;
        burst.Rotation = (endWorld - originWorld).Angle();

        var distance = originWorld.DistanceTo(endWorld);

        var halo = CreateRadialGlowSprite(0.35f, 0f);
        halo.Position = new Vector2(distance * 0.5f, 0f);
        halo.Scale = new Vector2(0.45f, 0.55f * KamehamehaBeamThickness * 1.6f);
        burst.AddChild(halo);

        var mid = CreateRadialGlowSprite(0.55f, 0f);
        mid.Position = new Vector2(distance * 0.55f, 0f);
        mid.Scale = new Vector2(0.22f, 0.38f * KamehamehaBeamThickness * 1.7f);
        burst.AddChild(mid);

        var coreTex = new GradientTexture2D
        {
            Width = 256,
            Height = 96,
            Fill = GradientTexture2D.FillEnum.Linear,
            FillFrom = new Vector2(0f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
        var coreGrad = new Gradient();
        coreGrad.SetColor(0, new Color(0.95f, 1f, 1f, 0f));
        coreGrad.SetColor(1, new Color(0.45f, 0.92f, 1f, 0.95f));
        coreGrad.AddPoint(0.65f, new Color(0.2f, 0.55f, 1f, 0.45f));
        coreGrad.AddPoint(1f, new Color(0.08f, 0.25f, 0.9f, 0f));
        coreTex.Gradient = coreGrad;

        const float coreTexWidth = 256f;
        float coreScaleStartX = Math.Max(distance * 0.2f, 32f) / coreTexWidth;
        float coreScaleEndX = distance / coreTexWidth;
        var core = new Sprite2D
        {
            Texture = coreTex,
            Centered = true,
            Offset = new Vector2(coreTexWidth * 0.5f, 0f),
            Material = CreateAdditiveMaterial(),
            Scale = new Vector2(coreScaleStartX, KamehamehaBeamThickness),
        };
        burst.AddChild(core);

        var tween = burst.CreateTween();
        tween.SetParallel(true);

        tween.TweenProperty(halo, "scale", new Vector2(0.95f, 1.05f * KamehamehaBeamThickness * 2.1f), 0.22)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(mid, "scale", new Vector2(0.65f, 0.72f * KamehamehaBeamThickness * 2.0f), 0.2)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(core, "scale", new Vector2(coreScaleEndX, KamehamehaBeamThickness * 1.25f), 0.2)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(burst, "modulate:a", 0f, 0.42).SetDelay(0.11);

        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(burst))
            {
                burst.QueueFree();
            }
        };
    }

    private static List<Creature> ResolveKamehamehaImpactTargets(Player player, CardPlay cardPlay)
    {
        ICombatState cs = player?.Creature?.CombatState;
        if (cs == null)
        {
            return [];
        }

        bool alive(Creature c) => c is { CurrentHp: > 0 };

        if (cardPlay.Card.TargetType == TargetType.AllEnemies)
        {
            return cs.HittableEnemies.Where(alive).ToList();
        }

        if (cardPlay.Target != null && alive(cardPlay.Target) &&
            cs.HittableEnemies.Any(e => ReferenceEquals(e, cardPlay.Target)))
        {
            return [cardPlay.Target];
        }

        return cs.HittableEnemies.Where(alive).ToList();
    }

    // 不使用逐敌人命中爆点，以免基点差异导致爆点偏到脚下，并避免群体光束产生多余视觉噪声。
}
