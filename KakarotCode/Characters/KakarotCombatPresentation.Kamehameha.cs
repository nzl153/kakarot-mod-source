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

// 龟波气功。
//
// 全 mod 最复杂的一张牌的表现层：换蓄力立绘、算发射点、算命中点、编排光束，
// 外加三级回退（打包场景 → 着色器 → 贴图 → 程序化），任何一级资源缺失都还能出画面。
//
// 瞄准之所以这么多代码，是因为光束要打到「最远的那个敌人」而不是主目标，
// 否则站位一散，光束会从中途凭空截断。
public static partial class KakarotCombatPresentation
{
    // Retain original textures so interrupted pose swaps can be restored.
    private static readonly Dictionary<ulong, Texture2D> KamehamehaPoseOriginalTextures = new();

    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalPositions = new();

    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalScales = new();

    // 用发波姿势的卡。光束也由这个集合控——见 useKamehamehaPose。
    //
    // 🔴 KAKAROT_DRAGON_FIST 在游戏里叫「全力龟派气功」，不是龙拳。
    // 类名 KakarotDragonFist 是早期留下的命名错配，「龙拳爆发」才是
    // KakarotDragonFistBurst。类名改不得（Id.Entry 由类名派生，改名毁存档），
    // 真名只存在于 localization/zhs/cards.json，读代码一定会认错，认准这条注释。
    private static readonly HashSet<string> KamehamehaPoseCardIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_TENFOLD_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_FATHER_SON_WAVE",
        "KAKAROTMOD-KAKAROT_DRAGON_FIST",
    };

    // 光束配色。未列出的一律走蓝色默认值。
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

    // ⚠ 光束 shader 必须写在这个已存在的文件里，不要新建 .gdshader。
    // 新建的文件没有 .uid，Godot 不会自动导入，ResourceLoader.Exists() 返回 false，
    // SpawnEnergyBeam 直接 return false，然后掉到最后的贴图分支 ——
    // 表现是「光束变回了旧的贴图风格」，而且编译期完全发现不了。
    // 旧版本的 shader 内容在 git 历史里，需要对比就去翻。
    private const string KamehamehaBeamShaderPath = "res://Kakarot/Shaders/kakarot_kamehameha_beam.gdshader";

    // 调试开关：true 走程序化 shader，false 退回原来的贴图/场景路径。
    private const bool KamehamehaUseShaderBeam = true;

    // 画布高度 = 光束能张到多宽的上限，不是束宽本身。
    // shader 的 thickness = 0.52，束体实际约 468px（悟空立绘约 600px 高，接近 3/4 个身子）。
    // 剩下的余量留给辉光衰减 —— 否则画面上会出现一个发光的方块硬边。
    // ⚠ 想让光束更粗改这里，不要改 shader 的 thickness：
    //   那个是「占画布的比例」，超过 0.6 会撑满画布、中心饱和成一整片纯白。
    private const float KamehamehaShaderBeamThickness = 900f;

    private const float KamehamehaShaderBeamGrowSeconds = 0.26f;

    private const float KamehamehaShaderBeamHoldSeconds = 0.34f;

    private const float KamehamehaShaderBeamFadeSeconds = 0.30f;

    // 手心气团直径 = 光束根部宽度 x 这个倍数。
    // 绑定到根部而不是画布：球必须明显比根部粗才读得出是球，
    // 只粗一点点的话，白芯一软化就被主干吞掉。
    private const float KamehamehaMuzzleOrbScale = 2.6f;

    // 气团沿束向拉长的比例。正圆读成「卡了一个球」，椭圆自带流向。
    private const float KamehamehaMuzzleOrbStretch = 1.55f;

    // 气团往前压进主干的比例（占球径）。右半个球埋进光束里，接缝就藏住了。
    private const float KamehamehaMuzzleOrbPush = 0.22f;

    // 光束根部宽度换算：shader 里 width = neck_width * thickness（占画布高度）。
    // 这两个值必须和 kakarot_kamehameha_beam_v2.gdshader 的默认值保持一致。
    private const float KamehamehaBeamNeckWidth = 0.46f;

    private const float KamehamehaBeamThicknessRatio = 0.52f;

    // 精灵比实际距离长出来的倍数。末尾留一段空画布兜住波头的辉光，
    // 否则波头会被精灵右边界切出一条竖直硬边。progress 只推到 1/这个值。
    private const float KamehamehaBeamLengthHeadroom = 1.10f;

    // 光束越过最远那只敌人之后再往外走多少像素。
    // 目的是读作「打穿整排」而不是「正好停在最后一只身上」。
    private const float KamehamehaBeamOvershoot = 320f;

    // 场上一个敌人都找不到时的兜底长度。
    private const float KamehamehaBeamFallbackLength = 900f;

    private const string KamehamehaBeamPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_beam.tscn";

    private const string KamehamehaImpactPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_impact.tscn";

    private const float KamehamehaImpactDelaySeconds = 0.17f;

    private static readonly Vector2 KamehamehaBeamCharacterOffset = new(50f, -58f);

    // 各形态立绘的手心位置不同，只列出跟默认值有偏差的形态，其余走默认。
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

            // 群体技绝不改朝向：玩家朝哪边就朝哪边，特效去找那一侧的敌人。
            // 单体技才按选中的目标转身。
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

            // 🔴 光束不再补命中爆点。
            //
            // 试过两版：逐敌人播（位置取精灵基点＝脚底，群体光束时其余敌人
            // 身上成了「凭空在脚下炸一下」）、只在光束落点播一次（位置对了，
            // 但那一下和光束本身抢读，反而把光束的干净感破坏掉）。
            // 结论是这里不需要爆点——光束打到人身上这件事，光束自己已经说清楚了。
            // 别再加回来。
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

    // Horizontal beams keep the caster's hand height.
    // 光束的终点。
    //
    // 以前这里直接用 ResolvePrimaryBeamTarget 的结果，而那个函数取的是
    // 「朝向那一侧最近的敌人」（朝向逻辑要跟它同源，不能改）。
    // 于是群怪时光束停在最靠前那只身上，后面几只完全没被扫到，演出效果大打折扣。
    //
    // 光束的终点和朝向用的目标不是同一件事：朝向要最近的，长度要最远的。
    // 所以这里单独找「朝向那一侧最远的可命中敌人」，再往外多走一段，
    // 读作打穿整排而不是停在第一只身上。
    private static Vector2 ComputeKamehamehaBeamEndWorld(
        Sprite2D staticModel, Creature primaryTarget, Vector2 beamOriginWorld, Player player = null)
    {
        float dir = staticModel.FlipH ? -1f : 1f;

        if (TryResolveFarthestBeamTargetX(player, staticModel, out float farthestX))
        {
            return new Vector2(farthestX + KamehamehaBeamOvershoot * dir, beamOriginWorld.Y);
        }

        // 找不到敌人节点时退回原来的单体行为，至少不会比以前差。
        if (primaryTarget != null && TryGetCreatureChestWorld(primaryTarget, out Vector2 enemyAnchor))
        {
            return new Vector2(enemyAnchor.X + KamehamehaBeamOvershoot * dir, beamOriginWorld.Y);
        }

        return beamOriginWorld + new Vector2(KamehamehaBeamFallbackLength * dir, 0f);
    }

    // 朝向那一侧最远的可命中敌人的世界 X。同侧没人时退回全场最远。
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

            // 同侧判定和 ResolvePrimaryBeamTarget 一致，只是这里取最远而不是最近。
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
            // 🔴 精灵的 GlobalPosition 是它的基点，不是身体中心。
            // 原来一律加固定的 -36：高个子勉强对得上，矮个子（比如蜥蜴那种）
            // 爆点就落在脚底甚至地面下。按贴图矩形取真实中心，高矮自适应。
            // Centered=true 的精灵矩形中心就是原点，行为与原来一致。
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

        // 取"最近的敌人"而不是"中间那个"。朝向逻辑（ApplyFacing）用的就是最近敌人，
        // 两边必须同源，否则群体技会把人物强行拧向队列中间那只。
        var room = NCombatRoom.Instance;
        var selfNode = room?.GetCreatureNode(player.Creature);
        if (selfNode == null)
        {
            return enemies[0];
        }

        // 群体技不转身，所以特效必须自己去找「当前朝向那一侧」最近的敌人，
        // 否则人朝右、特效却飞向左边的敌人。同侧没人时才退回全场最近的。
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

    // 旧的逐敌人命中爆点实现已删除。它取 StaticModel 基点＝脚底做位置，
    // 又对每个敌人各播一次——群体光束只指向中间那个敌人，其余敌人身上就是
    // 「凭空在脚下炸一下」。替代品是 SpawnBeamImpact，只在光束真正的落点炸一次。
}
