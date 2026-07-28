using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KakarotMod.KakarotCode.Characters;

/// <summary>
/// Presentation-only combat animations/SFX for Kakarot.
/// Must never affect gameplay results.
/// </summary>
public static class KakarotCombatPresentation
{
    private const string AttackSfxPath = "res://Kakarot/Audio/sfx/combat/attack_windup/sfx_attack_windup_a.wav";
    private const string HitSfxPath = "res://Kakarot/Audio/sfx/combat/hit/sfx_hit_light_a.wav";
    private const string DefendSfxPath = "res://Kakarot/Audio/sfx/combat/defend/sfx_defend_a.wav";
    private const string ChargeSfxPath = "res://Kakarot/Audio/sfx/combat/charge/sfx_charge_up_a.wav";
    private const string DragonFistBurstSfxPath = "res://Kakarot/Audio/sfx/combat/attack_windup/sfx_dragon_fist_burst_a.wav";
    private const string KaiokenSfxPath = "res://Kakarot/Audio/sfx/combat/charge/sfx_kaioken_a.wav";
    private const string KamehamehaSfxPath = "res://Kakarot/Audio/sfx/combat/attack_windup/sfx_kamehameha_a.wav";
    private const string SuperSaiyanTransformSfxPath = "res://Kakarot/Audio/sfx/combat/charge/sfx_transform_ssj_a.wav";
    private const string SuperSaiyanGodTransformSfxPath = "res://Kakarot/Audio/sfx/combat/charge/sfx_transform_divine_a.wav";
    private const string SuperSaiyan4TransformSfxPath = "res://Kakarot/Audio/sfx/combat/charge/sfx_transform_ss4_a.wav";
    private const string UltraInstinctTransformSfxPath = "res://Kakarot/Audio/sfx/combat/charge/sfx_transform_ui_a.wav";
    private const string SkillSfxPath = "res://Kakarot/Audio/sfx/combat/skill/sfx_skill_generic_a.wav";
    private const string AttackSfxNodeName = "AttackSfx";
    private const string HitSfxNodeName = "HitSfx";
    private const string DefendSfxNodeName = "DefendSfx";
    private const string ChargeSfxNodeName = "ChargeSfx";
    private const string DragonFistBurstSfxNodeName = "DragonFistBurstSfx";
    private const string KaiokenSfxNodeName = "KaiokenSfx";
    private const string KamehamehaSfxNodeName = "KamehamehaSfx";
    private const string SuperSaiyanTransformSfxNodeName = "TransformSfx";
    private const string SuperSaiyanGodTransformSfxNodeName = "DivineTransformSfx";
    private const string SuperSaiyan4TransformSfxNodeName = "Ss4TransformSfx";
    private const string UltraInstinctTransformSfxNodeName = "UiTransformSfx";
    private const string SkillSfxNodeName = "SkillSfx";

    private static readonly Dictionary<ulong, Tween> ActiveTweens = new();
    private static readonly Dictionary<ulong, Tween> IdleTweens = new();
    private static readonly Dictionary<string, AudioStream> AudioCache = new();

    /// <summary>记录 staticModel 在发�?pose swap 前的原始贴图，用于结束后强制恢复�?/summary>
    private static readonly Dictionary<ulong, Texture2D> KamehamehaPoseOriginalTextures = new();
    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalPositions = new();
    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalScales = new();

    /// <summary>触发"发波架势立绘"的卡 ID 白名单（龙拳因为不是发波视觉，按用户决定不参与）�?/summary>
    private static readonly HashSet<string> KamehamehaPoseCardIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_TENFOLD_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_FATHER_SON_WAVE",
    };

    /// <summary>pose 显示窗口：覆盖整�?windup + 光束生命周期，到光束几乎透明时恢复�?/summary>
    private const float KamehamehaPoseDurationSeconds = 0.55f;
    private const float KamehamehaPoseScaleMultiplier = 1.12f;
    private static readonly Dictionary<string, int> AttackRepeatOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Multi-hit cards: replay the lunge to better match impact cadence.
        ["KAKAROTMOD-KAKAROT_WOLF_FANG_FIST"] = 4,
        ["KAKAROTMOD-KAKAROT_EIGHT_ARMED_FIST"] = 3,
        ["KAKAROTMOD-KAKAROT_AFTERIMAGE_FIST"] = 3,
    };
    private static readonly HashSet<string> DefenseCardOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Dedicated defensive/block cards.
        "KAKAROTMOD-KAKAROT_DEFEND",
        "KAKAROTMOD-KAKAROT_TURTLE_SCHOOL",
        "KAKAROTMOD-KAKAROT_JUSTICE_HEART",
        "KAKAROTMOD-KAKAROT_WILD_GUARD",
        "KAKAROTMOD-KAKAROT_SAIYAN_GUARD",
        "KAKAROTMOD-KAKAROT_TOP_FIST",
        "KAKAROTMOD-KAKAROT_INSTANT_TRANSMISSION",
        "KAKAROTMOD-KAKAROT_GREAT_APE_FORM",
    };
    private static readonly HashSet<string> ChargeCardOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_CHARGE_UP",
        "KAKAROTMOD-KAKAROT_CANCEL_SUPER_SAIYAN_FORM",
    };

    // 触发"爆气"金光的卡：变身系 + 集气。播放独立光环特效，与换形态同时炸开。
    private static readonly HashSet<string> TransformAuraCardIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_SUPER_SAIYAN_TRANSFORM",
        "KAKAROTMOD-KAKAROT_KAIOKEN",
        "KAKAROTMOD-KAKAROT_SUPER_SAIYAN_BLUE_TRANSFORM",
        "KAKAROTMOD-KAKAROT_SUPER_SAIYAN_GOD_TRANSFORM",
        "KAKAROTMOD-KAKAROT_GOD_KI",
        "KAKAROTMOD-KAKAROT_ULTRA_INSTINCT_OMEN",
        "KAKAROTMOD-KAKAROT_PEAK_ULTRA_INSTINCT",
        "KAKAROTMOD-KAKAROT_GREAT_APE_FORM",
        "KAKAROTMOD-KAKAROT_SHENRON_WISH_TRANSFORM",
        "KAKAROTMOD-KAKAROT_RETURN_TO_ORIGIN",
        "KAKAROTMOD-KAKAROT_CHARGE_UP",
    };

    private const string SpiritBombCardId = "KAKAROTMOD-KAKAROT_SPIRIT_BOMB";
    private const string SolarFistCardId = "KAKAROTMOD-KAKAROT_SOLAR_FIST";
    private static readonly Dictionary<string, (string Path, string NodeName, float VolumeDb)> SpecialAttackSfxOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["KAKAROTMOD-KAKAROT_DRAGON_FIST_BURST"] = (DragonFistBurstSfxPath, DragonFistBurstSfxNodeName, -1f),
        ["KAKAROTMOD-KAKAROT_KAMEHAMEHA"] = (KamehamehaSfxPath, KamehamehaSfxNodeName, -3f),
        ["KAKAROTMOD-KAKAROT_DRAGON_FIST"] = (KamehamehaSfxPath, KamehamehaSfxNodeName, -3f),
        ["KAKAROTMOD-KAKAROT_TENFOLD_KAMEHAMEHA"] = (KamehamehaSfxPath, KamehamehaSfxNodeName, -3f),
        ["KAKAROTMOD-KAKAROT_FATHER_SON_WAVE"] = (KamehamehaSfxPath, KamehamehaSfxNodeName, -3f),
    };
    private static readonly Dictionary<string, (string Path, string NodeName)> SpecialSkillSfxOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["KAKAROTMOD-KAKAROT_KAIOKEN"] = (KaiokenSfxPath, KaiokenSfxNodeName),
        ["KAKAROTMOD-KAKAROT_SUPER_SAIYAN_TRANSFORM"] = (SuperSaiyanTransformSfxPath, SuperSaiyanTransformSfxNodeName),
        ["KAKAROTMOD-KAKAROT_SUPER_SAIYAN_GOD_TRANSFORM"] = (SuperSaiyanGodTransformSfxPath, SuperSaiyanGodTransformSfxNodeName),
        ["KAKAROTMOD-KAKAROT_SUPER_SAIYAN_BLUE_TRANSFORM"] = (SuperSaiyanGodTransformSfxPath, SuperSaiyanGodTransformSfxNodeName),
        ["KAKAROTMOD-KAKAROT_RETURN_TO_ORIGIN"] = (SuperSaiyan4TransformSfxPath, SuperSaiyan4TransformSfxNodeName),
        ["KAKAROTMOD-KAKAROT_ULTRA_INSTINCT_OMEN"] = (UltraInstinctTransformSfxPath, UltraInstinctTransformSfxNodeName),
        ["KAKAROTMOD-KAKAROT_PEAK_ULTRA_INSTINCT"] = (UltraInstinctTransformSfxPath, UltraInstinctTransformSfxNodeName),
    };

    public static void TryPlayAttackWindup(Player player, CardPlay cardPlay)
    {
        try
        {
            if (player?.Creature == null || cardPlay?.Card == null)
            {
                return;
            }

            if (cardPlay.Card.Owner != player)
            {
                return;
            }

            if (!IsKakarot(player.Creature))
            {
                return;
            }

            var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
            var visualsRoot = creatureNode?.Visuals;
            var staticModel = visualsRoot?.GetNodeOrNull<Sprite2D>("StaticModel");
            if (staticModel == null)
            {
                return;
            }

            var cardEntryId = cardPlay.Card.Id?.Entry;
            if (cardPlay.Card.Type == CardType.Attack)
            {
                // For multi-part bosses (e.g., left/right claws), face the actual selected target first.
                if (cardPlay.Target != null)
                {
                    KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
                }
                else
                {
                    KakarotFormVisuals.RefreshFacing(player.Creature);
                }

                var repeatCount = ResolveAttackRepeatCount(cardEntryId);
                var windupRest = KakarotFormVisuals.GetRestTransform(player.Creature);
                var useKamehamehaPose = !string.IsNullOrWhiteSpace(cardEntryId)
                    && KamehamehaPoseCardIds.Contains(cardEntryId);
                var windupScaleMultiplier = useKamehamehaPose ? KamehamehaPoseScaleMultiplier : 1f;
                PlayWindupTween(staticModel, repeatCount, windupRest.Pos, windupRest.Scale, windupScaleMultiplier);

                if (!string.IsNullOrWhiteSpace(cardEntryId) && SpecialAttackSfxOverrides.TryGetValue(cardEntryId, out var special))
                {
                    PlaySfx(visualsRoot, special.NodeName, special.Path, special.VolumeDb, true);
                    // 龟派气功系多张卡共用同一套可选粒子场景；缺文件时用程序化渐变作为兜底�?
                    if (string.Equals(special.Path, KamehamehaSfxPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (useKamehamehaPose)
                        {
                            TrySwapToKamehamehaPose(player.Creature, staticModel);
                        }
                        TryPlayKamehamehaBeamVfx(player, cardPlay, visualsRoot, staticModel);
                    }
                }
                else
                {
                    PlaySfx(visualsRoot, AttackSfxNodeName, AttackSfxPath, -4f, true);
                    // 近战攻击：在前冲位移期间临时切到"出拳 pose"（缺图自动跳过）。时长随多段攻击拉长。
                    var attackPoseDuration = 0.14f * Math.Clamp(repeatCount, 1, 5) + 0.12f;
                    TrySwapToPose(player.Creature, staticModel, KakarotFormVisuals.ResolveAttackPosePath(player.Creature), attackPoseDuration);

                    // 拳风 + 命中炸开：常规近战才加；元气弹有自己的大特效，跳过避免叠加。
                    if (!string.Equals(cardEntryId, SpiritBombCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        ScheduleMeleeImpactFx(staticModel, visualsRoot, player, cardPlay, repeatCount);
                    }
                }

                // 元气弹：头顶聚能量球 → 砸向敌人 → 命中爆裂（程序化，零素材）。
                if (string.Equals(cardEntryId, SpiritBombCardId, StringComparison.OrdinalIgnoreCase))
                {
                    PlaySpiritBombVfx(player, cardPlay, visualsRoot, staticModel);
                }
                return;
            }

            // 变身/集气："爆气"金光特效，与换形态同时炸开（独立节点，不动角色本体）。
            if (!string.IsNullOrWhiteSpace(cardEntryId) && TransformAuraCardIds.Contains(cardEntryId))
            {
                PlayTransformAura(staticModel);
            }

            // 太阳拳：全屏强光一闪（程序化，零素材）。
            if (string.Equals(cardEntryId, SolarFistCardId, StringComparison.OrdinalIgnoreCase))
            {
                PlaySolarFlareVfx(visualsRoot, staticModel);
            }

            if (IsDefenseCard(cardPlay.Card, cardEntryId))
            {
                PlaySfx(visualsRoot, DefendSfxNodeName, DefendSfxPath, -5f, true);
            }

            if (IsChargeCard(cardPlay.Card, cardEntryId))
            {
                PlaySfx(visualsRoot, ChargeSfxNodeName, ChargeSfxPath, -4f, true);
            }

            if (!string.IsNullOrWhiteSpace(cardEntryId) && SpecialSkillSfxOverrides.TryGetValue(cardEntryId, out var skillSfx))
            {
                PlaySfx(visualsRoot, skillSfx.NodeName, skillSfx.Path, -4f, true);
            }
            else if (cardPlay.Card.Type == CardType.Skill)
            {
                PlaySfx(visualsRoot, SkillSfxNodeName, SkillSfxPath, -5f, true);
            }
        }
        catch
        {
            // Presentation must never break combat flow.
        }
    }

    public static void TryPlayHitReaction(Creature creature)
    {
        try
        {
            if (creature == null || !IsKakarot(creature))
            {
                return;
            }

            var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
            var visualsRoot = creatureNode?.Visuals;
            var staticModel = visualsRoot?.GetNodeOrNull<Sprite2D>("StaticModel");
            if (staticModel == null)
            {
                return;
            }

            var hitRest = KakarotFormVisuals.GetRestTransform(creature);
            PlayHitTween(staticModel, hitRest.Pos, hitRest.Scale);
            PlaySfx(visualsRoot, HitSfxNodeName, HitSfxPath, -5f, false);
            // 受击：在后仰位移期间临时切到"受伤 pose"（缺图自动跳过）。
            TrySwapToPose(creature, staticModel, KakarotFormVisuals.ResolveHurtPosePath(creature), 0.22f);
        }
        catch
        {
            // Presentation must never break combat flow.
        }
    }

    /// <summary>自定义光束长条图（带透明通道更佳）；放进 PCK 即可替换程序化光束�?/summary>
    private const string KamehamehaBeamTexturePath = "res://Kakarot/Images/Vfx/kamehameha_beam.png";

    private const string KamehamehaBeamPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_beam.tscn";

    /// <summary>可选：敌人身上爆炸粒子场景；没有则用程序化环形光�?/summary>
    private const string KamehamehaImpactPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_impact.tscn";

    private const float KamehamehaImpactDelaySeconds = 0.17f;

    /// <summary>相对 <see cref="Sprite2D.GlobalPosition"/> 的起点偏移：X 随朝向取符号，Y 为向上抬高�?/summary>
    /// <summary>X 略向右延长起点（配合面朝右时龟派从手心穿出）；翻面时符号自动镜像�?/summary>
    private static readonly Vector2 KamehamehaBeamCharacterOffset = new(50f, -29f);

    /// <summary>敌人身上的命中点相对�?StaticModel 的偏移（Y 向上抬到胸口位置）�?/summary>
    private static readonly Vector2 KamehamehaBeamEnemyOffset = new(0f, -36f);

    /// <summary>程序化光束的视觉宽度（垂直方向），基本贴近原�?0.38f 的体感�?/summary>
    private const float KamehamehaBeamThickness = 0.34f;

    /// <summary>PNG 贴图光束的纵向缩放（保持�?0.18 体感）�?/summary>
    private const float KamehamehaBeamTextureThicknessScale = 0.18f;

    /// <summary>
    /// 发波架势临时替换：把当前 staticModel 贴图换成对应形态的"发波 pose"立绘，约 0.55s 后恢复�?
    /// 多次连发自动保留第一次记录的原贴图（KamehamehaPoseOriginalTextures），不会因为重入丢失原值�?
    /// </summary>
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
            // 同一战斗内重入：只在第一次记录原贴图，后�?swap 仅刷�?pose 贴图�?
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
                // SceneTree 不可用时立即恢复，避免贴图卡死�?
                TryRestoreFromKamehamehaPose(staticModel);
            }
        }
        catch
        {
            // Presentation-only.
        }
    }

    /// <summary>从发�?pose 强制恢复原贴图。Refresh 形态切换前会主动调用以避免冲突�?/summary>
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
                // Presentation-only.
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
                // Presentation-only.
            }
            KamehamehaPoseOriginalPositions.Remove(id);
            KamehamehaPoseOriginalScales.Remove(id);
        }
    }

    /// <summary>
    /// 龟派气功家族共用表现：优先整束场�?�?其次 PNG 贴图 �?最后程序化柔光；命中时在敌人侧播放爆炸�?
    /// 光束几何采用「两端连接」：起点锁在角色手心，rotation/scale 朝主要敌人胸口动态计算�?
    /// </summary>
    private static void TryPlayKamehamehaBeamVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            Creature primaryTarget = ResolvePrimaryBeamTarget(player, cardPlay);
            Vector2 beamOriginWorld = ComputeKamehamehaBeamOriginWorld(staticModel);
            Vector2 beamEndWorld = ComputeKamehamehaBeamEndWorld(staticModel, primaryTarget, beamOriginWorld);

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
                    // 保持已有场景的 dir 翻转语义，长度可在资源中调整。
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

            if (spawned)
            {
                var impactScheduleTree = staticModel.GetTree();
                if (impactScheduleTree != null)
                {
                    ScheduleKamehamehaImpactBursts(impactScheduleTree, player, cardPlay, KamehamehaImpactDelaySeconds);
                }
            }
        }
        catch
        {
            // Presentation-only; never interrupt combat.
        }
    }

    /// <summary>起点：角色手心位置（受朝向翻转）�?/summary>
    private static Vector2 ComputeKamehamehaBeamOriginWorld(Sprite2D staticModel)
    {
        float dir = staticModel.FlipH ? -1f : 1f;
        return staticModel.GlobalPosition +
            new Vector2(KamehamehaBeamCharacterOffset.X * dir, KamehamehaBeamCharacterOffset.Y);
    }

    /// <summary>Beam end uses the target X while staying level with the hand origin.</summary>
    private static Vector2 ComputeKamehamehaBeamEndWorld(Sprite2D staticModel, Creature primaryTarget, Vector2 beamOriginWorld)
    {
        Vector2 enemyAnchor;
        if (primaryTarget != null && TryGetCreatureChestWorld(primaryTarget, out enemyAnchor))
        {
            return new Vector2(enemyAnchor.X, beamOriginWorld.Y);
        }

        float dir = staticModel.FlipH ? -1f : 1f;
        return beamOriginWorld + new Vector2(420f * dir, 0f);
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
        Vector2 basePos = enemyStatic?.GlobalPosition ?? enemyVisuals.GlobalPosition;
        world = basePos + KamehamehaBeamEnemyOffset;
        return true;
    }

    /// <summary>主要目标：单体卡�?cardPlay.Target；AOE 卡取存活敌人列表中位（视觉中心点）�?/summary>
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

        return enemies[enemies.Count / 2];
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
        // 起点固定�?holder 原点 �?sprite 居中后向 +X 方向延伸；用 Offset 把贴�?anchor 推到左端�?
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

    private static CanvasItemMaterial CreateAdditiveMaterial()
    {
        return new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    private static Sprite2D CreateRadialGlowSprite(float innerAlpha, float outerAlpha)
    {
        var gradientTex = new GradientTexture2D
        {
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.75f, 0.98f, 1f, innerAlpha));
        grad.SetColor(1, new Color(0.2f, 0.5f, 1f, outerAlpha));
        gradientTex.Gradient = grad;

        return new Sprite2D
        {
            Texture = gradientTex,
            Centered = true,
            Material = CreateAdditiveMaterial(),
        };
    }

    private static Sprite2D CreateGoldGlow(float innerAlpha, float outerAlpha)
    {
        var gradientTex = new GradientTexture2D
        {
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 0.97f, 0.65f, innerAlpha));
        grad.SetColor(1, new Color(1f, 0.6f, 0.1f, outerAlpha));
        gradientTex.Gradient = grad;

        return new Sprite2D
        {
            Texture = gradientTex,
            Centered = true,
            Material = CreateAdditiveMaterial(),
        };
    }

    /// <summary>
    /// 变身/集气"爆气"金光：核心闪光 + 外圈扩散环 + 向上能量柱。纯程序化、零素材。
    /// 独立节点（不动角色 staticModel，避免与形态 Refresh/待机呼吸打架），与换形态同时炸开，播完自销毁。
    /// </summary>
    private static void PlayTransformAura(Sprite2D staticModel)
    {
        try
        {
            if (staticModel?.GetParent() is not Node2D parent)
            {
                return;
            }

            var aura = new Node2D { Name = "KakarotTransformAura", ZIndex = 22 };
            aura.Position = staticModel.Position + new Vector2(0f, -76f);
            parent.AddChild(aura);

            var flash = CreateGoldGlow(0.95f, 0f);
            flash.Scale = new Vector2(1.1f, 1.1f);
            aura.AddChild(flash);

            var ring = CreateGoldGlow(0.5f, 0f);
            ring.Scale = new Vector2(0.7f, 0.8f);
            aura.AddChild(ring);

            var column = CreateGoldGlow(0.6f, 0f);
            column.Position = new Vector2(0f, -40f);
            column.Scale = new Vector2(0.5f, 1.6f);
            aura.AddChild(column);

            var tween = aura.CreateTween();
            tween.SetParallel(true);
            tween.SetPauseMode(Tween.TweenPauseMode.Process);

            tween.TweenProperty(flash, "scale", new Vector2(3.6f, 3.9f), 0.32)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(flash, "modulate:a", 0f, 0.46)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

            tween.TweenProperty(ring, "scale", new Vector2(2.6f, 2.9f), 0.42)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "modulate:a", 0f, 0.5)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

            tween.TweenProperty(column, "scale", new Vector2(0.9f, 3.4f), 0.4)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(column, "position", new Vector2(0f, -120f), 0.4)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(column, "modulate:a", 0f, 0.48)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

            tween.Finished += () =>
            {
                if (GodotObject.IsInstanceValid(aura))
                {
                    aura.QueueFree();
                }
            };
        }
        catch
        {
            // Presentation-only; never break combat flow.
        }
    }

    private static Sprite2D CreateWhiteGlow(float outerAlpha)
    {
        var gradientTex = new GradientTexture2D
        {
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 1f, 0.96f, 1f));
        grad.SetColor(1, new Color(1f, 0.95f, 0.7f, outerAlpha));
        gradientTex.Gradient = grad;

        return new Sprite2D
        {
            Texture = gradientTex,
            Centered = true,
            Material = CreateAdditiveMaterial(),
        };
    }

    /// <summary>元气弹：在敌人正上方聚一颗大而圆的蓝白能量球 → 砸落到敌人身上 → 命中爆裂。纯程序化、零素材。</summary>
    private static void PlaySpiritBombVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            var target = ResolvePrimaryBeamTarget(player, cardPlay);
            Vector2 impactWorld;
            if (target == null || !TryGetCreatureChestWorld(target, out impactWorld))
            {
                float dir = staticModel.FlipH ? -1f : 1f;
                impactWorld = staticModel.GlobalPosition + new Vector2(420f * dir, -40f);
            }

            // 聚球点：敌人正上方
            var spawnWorld = impactWorld + new Vector2(0f, -360f);

            var holder = new Node2D { Name = "KakarotSpiritBombFx", ZIndex = 30 };
            anchor.AddChild(holder);
            holder.GlobalPosition = spawnWorld;

            // 外层柔光 + 内核，外圈 alpha 全为 0 → 边缘干净的圆球（不再有方形感）。
            var glow = CreateRadialGlowSprite(0.55f, 0f);
            glow.Scale = new Vector2(0.4f, 0.4f);
            holder.AddChild(glow);

            var core = CreateRadialGlowSprite(0.95f, 0f);
            core.Scale = new Vector2(0.22f, 0.22f);
            holder.AddChild(core);

            var tween = holder.CreateTween();
            tween.SetParallel(false);
            tween.SetPauseMode(Tween.TweenPauseMode.Process);

            // 1) 聚气：球体长大（又大又圆）
            tween.TweenProperty(glow, "scale", new Vector2(3.0f, 3.0f), 0.55)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(core, "scale", new Vector2(1.7f, 1.7f), 0.55)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

            // 2) 砸落到敌人身上（加速下坠）
            tween.TweenProperty(holder, "global_position", impactWorld, 0.22)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            // 3) 命中爆裂 + 收尾（大范围圆形爆闪）
            tween.TweenCallback(Callable.From(() => SpawnRadialBurst(anchor, impactWorld, 5.0f, 0.45f)));
            tween.TweenInterval(0.45);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));
        }
        catch
        {
            // Presentation-only; never break combat flow.
        }
    }

    /// <summary>太阳拳：以角色为中心的全屏强光一闪（极快提亮 → 滞留 → 淡出）。纯程序化、零素材。</summary>
    private static void PlaySolarFlareVfx(Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            var holder = new Node2D { Name = "KakarotSolarFlareFx", ZIndex = 40 };
            anchor.AddChild(holder);
            holder.GlobalPosition = staticModel.GlobalPosition + new Vector2(0f, -80f);

            var flash = CreateWhiteGlow(0f);
            flash.Scale = new Vector2(0.6f, 0.6f);
            flash.Modulate = new Color(1f, 1f, 1f, 0f);
            holder.AddChild(flash);

            var tween = holder.CreateTween();
            tween.SetParallel(false);
            tween.SetPauseMode(Tween.TweenPauseMode.Process);

            // 极快炸亮（致盲感）
            tween.TweenProperty(flash, "scale", new Vector2(13f, 13f), 0.12)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(flash, "modulate:a", 1f, 0.1)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

            // 滞留后淡出
            tween.TweenInterval(0.06);
            tween.TweenProperty(flash, "modulate:a", 0f, 0.34)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));
        }
        catch
        {
            // Presentation-only; never break combat flow.
        }
    }

    /// <summary>通用径向爆闪：在世界坐标处放一团光，快速放大并淡出后自销毁。</summary>
    private static void SpawnRadialBurst(Node2D anchor, Vector2 world, float maxScale, float duration)
    {
        var burst = new Node2D { Name = "KakarotRadialBurst", ZIndex = 31 };
        anchor.AddChild(burst);
        burst.GlobalPosition = world;

        var flash = CreateRadialGlowSprite(0.95f, 0f);
        flash.Scale = new Vector2(0.4f, 0.4f);
        burst.AddChild(flash);

        var tween = burst.CreateTween();
        tween.SetParallel(true);
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(flash, "scale", new Vector2(maxScale, maxScale), duration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(flash, "modulate:a", 0f, duration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(burst))
            {
                burst.QueueFree();
            }
        }));
    }

    // ===== 近战"打击感"：拳风 + 命中炸开（纯程序化、零素材、不影响玩法/netcode） =====
    // 时间轴对齐 PlayWindupTween：每段出拳 = 蓄力 0.06s → 前冲 0.05s → 收招 0.10s（loop 0.21s）。
    // 前冲开始 ≈ 0.06s（甩拳风），接触帧 ≈ 0.115s（敌人身上炸）。
    private const float MeleeStrikeStartSeconds = 0.06f;
    private const float MeleeContactSeconds = 0.115f;
    private const float MeleeLoopSeconds = 0.21f;

    /// <summary>
    /// 调度近战表现：每段出拳在「前冲帧」从角色拳头甩出拳风、在「接触帧」于目标身上炸一下。
    /// 多段攻击（撕裂步/八臂拳/残像拳）按 loop 节奏逐段对齐。全部走 SceneTree 计时器 + 失效校验，战斗结束/敌人死亡自动跳过。
    /// </summary>
    private static void ScheduleMeleeImpactFx(Sprite2D staticModel, Node visualsRoot, Player player, CardPlay cardPlay, int repeatCount)
    {
        var tree = staticModel?.GetTree();
        if (tree == null || visualsRoot is not Node2D anchor)
        {
            return;
        }

        var loops = Math.Clamp(repeatCount, 1, 5);
        for (var i = 0; i < loops; i++)
        {
            float windDelay = MeleeStrikeStartSeconds + i * MeleeLoopSeconds;
            float hitDelay = MeleeContactSeconds + i * MeleeLoopSeconds;

            tree.CreateTimer(windDelay).Timeout += () =>
            {
                try
                {
                    if (GodotObject.IsInstanceValid(staticModel) && GodotObject.IsInstanceValid(anchor))
                    {
                        SpawnPunchWindStreak(anchor, staticModel, staticModel.FlipH ? -1f : 1f);
                    }
                }
                catch
                {
                    // Presentation-only.
                }
            };

            tree.CreateTimer(hitDelay).Timeout += () =>
            {
                try
                {
                    foreach (Creature enemy in ResolveKamehamehaImpactTargets(player, cardPlay))
                    {
                        SpawnMeleeImpactFx(enemy);
                    }
                }
                catch
                {
                    // Presentation-only.
                }
            };
        }
    }

    /// <summary>横向速度线贴图：中间最亮、两端透明的细长条（叠加混合下即一道光痕）。</summary>
    private static GradientTexture2D CreateStreakTexture()
    {
        var tex = new GradientTexture2D
        {
            Width = 128,
            Height = 12,
            Fill = GradientTexture2D.FillEnum.Linear,
            FillFrom = new Vector2(0f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 1f, 1f, 0f));
        g.SetColor(1, new Color(1f, 1f, 1f, 0f));
        g.AddPoint(0.5f, new Color(1f, 1f, 1f, 0.95f));
        tex.Gradient = g;
        return tex;
    }

    /// <summary>空心冲击波环：中心与外缘透明、仅一圈亮带（径向渐变在 ~0.9 处给峰值）。</summary>
    private static Sprite2D CreateRingSprite(Color ringColor)
    {
        var tex = new GradientTexture2D
        {
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        var transparent = new Color(ringColor.R, ringColor.G, ringColor.B, 0f);
        var g = new Gradient();
        g.SetColor(0, transparent);
        g.SetColor(1, transparent);
        g.AddPoint(0.74f, transparent);
        g.AddPoint(0.9f, ringColor);
        tex.Gradient = g;

        return new Sprite2D
        {
            Texture = tex,
            Centered = true,
            Material = CreateAdditiveMaterial(),
        };
    }

    /// <summary>
    /// 拳风：从拳头高度「纯水平」朝出拳方向甩出一阵风（参考原版 vfx_horizontal_smoke：水平铺开、快淡入再淡出）。
    /// 三条紧凑的水平速度线在拳头上下铺开 + 整体朝前推一小段，零旋转、不管敌人多高。纯表现、零素材。
    /// </summary>
    private static void SpawnPunchWindStreak(Node2D anchor, Sprite2D staticModel, float dir)
    {
        try
        {
            var holder = new Node2D { Name = "KakarotPunchWind", ZIndex = 23 };
            anchor.AddChild(holder);
            // 起点：拳头高度（与角色水平），略偏出拳方向
            var startPos = staticModel.GlobalPosition + new Vector2(54f * dir, -72f);
            holder.GlobalPosition = startPos;

            const float streakTexWidth = 128f;
            var streakTex = CreateStreakTexture();

            // 三条水平线：中间最亮最长，上下两条更淡更短 → "一阵风"而非单线
            float[] yOffsets = { -13f, 0f, 13f };
            float[] alphas = { 0.45f, 0.9f, 0.45f };
            float[] lenScale = { 0.7f, 1f, 0.7f };

            for (var i = 0; i < yOffsets.Length; i++)
            {
                var streak = new Sprite2D
                {
                    Texture = streakTex,
                    Centered = true,
                    // Offset 把贴图推到左端 → 左端锚在拳头，scale.x 只朝 +X（出拳方向）拉伸；零旋转保持水平。
                    Offset = new Vector2(streakTexWidth * 0.5f, 0f),
                    Position = new Vector2(0f, yOffsets[i]),
                    Scale = new Vector2(0.1f * dir, 0.3f),
                    Modulate = new Color(1f, 1f, 1f, alphas[i]),
                    Material = CreateAdditiveMaterial(),
                };
                holder.AddChild(streak);

                var t = streak.CreateTween();
                t.SetParallel(true);
                t.SetPauseMode(Tween.TweenPauseMode.Process);
                t.TweenProperty(streak, "scale:x", 1.5f * lenScale[i] * dir, 0.09)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                t.TweenProperty(streak, "modulate:a", 0f, 0.14)
                    .SetDelay(0.05)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            }

            // 整体朝出拳方向推一小段——"打出去那一下"的位移
            var move = holder.CreateTween();
            move.SetPauseMode(Tween.TweenPauseMode.Process);
            move.TweenProperty(holder, "global_position", startPos + new Vector2(210f * dir, 0f), 0.13)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            var tree = holder.GetTree();
            if (tree != null)
            {
                tree.CreateTimer(0.3).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.QueueFree();
                    }
                };
            }
        }
        catch
        {
            // Presentation-only.
        }
    }

    /// <summary>
    /// 命中炸开：在敌人胸口炸一下 = 锐利白闪（打击点）+ 薄冲击波环（外溢）+ 放射火花线（拳风余势）。
    /// 克制不夸张，但读得出"实打实砸到"。纯表现、零素材。
    /// </summary>
    private static void SpawnMeleeImpactFx(Creature enemy)
    {
        try
        {
            if (enemy == null || enemy.CurrentHp <= 0)
            {
                return;
            }

            var creatureNode = NCombatRoom.Instance?.GetCreatureNode(enemy);
            if (creatureNode?.Visuals is not Node2D ev)
            {
                return;
            }

            var sm = ev.GetNodeOrNull<Sprite2D>("StaticModel");
            var holder = new Node2D { Name = "KakarotMeleeImpactFx", ZIndex = 33 };
            ev.AddChild(holder);
            holder.GlobalPosition = (sm?.GlobalPosition ?? ev.GlobalPosition) + new Vector2(0f, -40f);

            // 1) 锐利白闪——打击的"点"，又快又小又亮
            var flash = CreateWhiteGlow(0f);
            flash.Scale = new Vector2(0.45f, 0.45f);
            flash.Modulate = new Color(1f, 1f, 1f, 0f);
            holder.AddChild(flash);
            var ft = flash.CreateTween();
            ft.SetParallel(true);
            ft.SetPauseMode(Tween.TweenPauseMode.Process);
            ft.TweenProperty(flash, "scale", new Vector2(1.45f, 1.45f), 0.10)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            ft.TweenProperty(flash, "modulate:a", 1f, 0.04)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            ft.Chain().TweenProperty(flash, "modulate:a", 0f, 0.14)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

            // 2) 冲击波环——"砰"地一下瞬间炸开（Expo.Out 起爆极快），白色更淡、不抢戏
            var ring = CreateRingSprite(new Color(1f, 0.95f, 0.82f, 0.5f));
            ring.Scale = new Vector2(0.25f, 0.25f);
            holder.AddChild(ring);
            var rt = ring.CreateTween();
            rt.SetParallel(true);
            rt.SetPauseMode(Tween.TweenPauseMode.Process);
            rt.TweenProperty(ring, "scale", new Vector2(2.5f, 2.5f), 0.12)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            rt.TweenProperty(ring, "modulate:a", 0f, 0.16)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

            // 3) 放射火花线——三条短线快速拉长、淡出（拳风余势）
            var streakTex = CreateStreakTexture();
            float[] angles = { -0.4f, 0f, 0.4f };
            foreach (var a in angles)
            {
                var streak = new Sprite2D
                {
                    Texture = streakTex,
                    Centered = true,
                    Material = CreateAdditiveMaterial(),
                    Rotation = a,
                    Scale = new Vector2(0.2f, 0.18f),
                    Modulate = new Color(1f, 1f, 1f, 0.9f),
                };
                holder.AddChild(streak);
                var st = streak.CreateTween();
                st.SetParallel(true);
                st.SetPauseMode(Tween.TweenPauseMode.Process);
                st.TweenProperty(streak, "scale:x", 1.0f, 0.14)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                st.TweenProperty(streak, "modulate:a", 0f, 0.2)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            }

            var tree = holder.GetTree();
            if (tree != null)
            {
                tree.CreateTimer(0.5).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.QueueFree();
                    }
                };
            }
        }
        catch
        {
            // Presentation-only.
        }
    }

    /// <summary>多层柔光 + 细核心；几何按「起点→终点」方向旋转，长度按距离动态拉伸�?/summary>
    private static void TryPlayKamehamehaBeamVfxProcedural(Node2D anchor, Vector2 originWorld, Vector2 endWorld)
    {
        var burst = new Node2D { Name = "KamehamehaBeamFx" };
        burst.ZIndex = 24;
        anchor.AddChild(burst);
        burst.GlobalPosition = originWorld;
        burst.Rotation = (endWorld - originWorld).Angle();

        var distance = originWorld.DistanceTo(endWorld);

        // halo / mid 为视觉柔光，居中跟随主轴；core 为长条主光束，从起点向终点方向拉伸�?
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
            // �?Offset 把贴�?anchor 推到左端，scale.x 拉伸时只�?+X 方向�?
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

    private static void ScheduleKamehamehaImpactBursts(SceneTree tree, Player player, CardPlay cardPlay, float delaySeconds)
    {
        if (tree == null)
        {
            return;
        }

        tree.CreateTimer(delaySeconds).Timeout += () =>
        {
            try
            {
                foreach (Creature enemy in ResolveKamehamehaImpactTargets(player, cardPlay))
                {
                    TrySpawnKamehamehaImpactBurst(enemy);
                }
            }
            catch
            {
                // Presentation-only.
            }
        };
    }

    private static void TrySpawnKamehamehaImpactBurst(Creature enemy)
    {
        try
        {
            if (enemy == null || enemy.CurrentHp <= 0)
            {
                return;
            }

            if (ResourceLoader.Exists(KamehamehaImpactPackedScenePath))
            {
                var packed = ResourceLoader.Load<PackedScene>(KamehamehaImpactPackedScenePath);
                var instance = packed?.Instantiate<Node2D>();
                if (instance == null)
                {
                    TrySpawnKamehamehaImpactBurstProcedural(enemy);
                    return;
                }

                var creatureNode = NCombatRoom.Instance?.GetCreatureNode(enemy);
                if (creatureNode?.Visuals is not Node2D ev)
                {
                    instance.QueueFree();
                    return;
                }

                var sm = ev.GetNodeOrNull<Sprite2D>("StaticModel");
                ev.AddChild(instance);
                instance.GlobalPosition = (sm?.GlobalPosition ?? ev.GlobalPosition) + new Vector2(0f, -24f);
                instance.ZIndex = 32;

                var impactSceneTree = instance.GetTree();
                if (impactSceneTree != null)
                {
                    impactSceneTree.CreateTimer(2.0).Timeout += () =>
                    {
                        if (GodotObject.IsInstanceValid(instance))
                        {
                            instance.QueueFree();
                        }
                    };
                }

                return;
            }

            TrySpawnKamehamehaImpactBurstProcedural(enemy);
        }
        catch
        {
            // Presentation-only.
        }
    }

    private static void TrySpawnKamehamehaImpactBurstProcedural(Creature enemy)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(enemy);
        if (creatureNode?.Visuals is not Node2D ev)
        {
            return;
        }

        var sm = ev.GetNodeOrNull<Sprite2D>("StaticModel");
        var holder = new Node2D { Name = "KamehamehaImpactFx" };
        holder.ZIndex = 32;
        ev.AddChild(holder);
        holder.GlobalPosition = (sm?.GlobalPosition ?? ev.GlobalPosition) + new Vector2(0f, -28f);

        var matAdd = CreateAdditiveMaterial();

        for (var i = 0; i < 4; i++)
        {
            var ring = new Sprite2D { Centered = true, Material = matAdd };
            var gt = new GradientTexture2D
            {
                Width = 72,
                Height = 72,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f, 0f),
            };
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 0.75f));
            g.SetColor(1, new Color(0.35f, 0.75f, 1f, 0f));
            gt.Gradient = g;
            ring.Texture = gt;

            float phase = i * 0.04f;
            ring.Scale = Vector2.One * (0.18f + i * 0.04f);
            ring.Rotation = i * 0.35f;
            holder.AddChild(ring);

            var ringTween = ring.CreateTween();
            ringTween.SetParallel(true);
            ringTween.TweenProperty(ring, "scale", Vector2.One * (1.6f + i * 0.35f), 0.28)
                .SetDelay(phase)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            ringTween.TweenProperty(ring, "modulate:a", 0f, 0.38)
                .SetDelay(0.06f + phase);
        }

        var cleanupTree = holder.GetTree();
        if (cleanupTree != null)
        {
            cleanupTree.CreateTimer(0.55).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            };
        }
    }

    private static void PlayWindupTween(
        Sprite2D staticModel,
        int repeatCount,
        Vector2 basePos,
        Vector2 baseScale,
        float scaleMultiplier = 1f)
    {
        var id = staticModel.GetInstanceId();
        StopIdle(staticModel);
        if (ActiveTweens.TryGetValue(id, out var running) && GodotObject.IsInstanceValid(running))
        {
            running.Kill();
        }

        staticModel.Position = basePos;
        var actionBaseScale = baseScale * scaleMultiplier;
        staticModel.Scale = actionBaseScale;
        var direction = staticModel.FlipH ? -1f : 1f;

        // 路B 程序化出拳：预备(后拉+下蹲压扁) → 挥击(快速前冲+朝前拉伸) → 收招(回正)。
        // squash & stretch 用非等比缩放伪造发力感，全程不需要任何新素材。
        var anticipatePos = basePos - new Vector2(12f * direction, 0f);
        var anticipateScale = new Vector2(actionBaseScale.X * 1.04f, actionBaseScale.Y * 0.95f);
        var strikePos = basePos + new Vector2(46f * direction, 0f);
        var strikeScale = new Vector2(actionBaseScale.X * 1.06f, actionBaseScale.Y * 0.97f);

        var tween = staticModel.CreateTween();
        tween.SetParallel(false);
        tween.SetPauseMode(Tween.TweenPauseMode.Process);

        var loops = Math.Clamp(repeatCount, 1, 5);
        for (var i = 0; i < loops; i++)
        {
            // 预备：略微后拉并下蹲压扁（蓄力）
            tween.TweenProperty(staticModel, "position", anticipatePos, 0.06f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(staticModel, "scale", anticipateScale, 0.06f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            // 挥击：快速前冲并朝运动方向拉伸（爆发）
            tween.TweenProperty(staticModel, "position", strikePos, 0.05f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(staticModel, "scale", strikeScale, 0.05f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

            // 收招：回到基准（带 Back 缓动制造一点回弹收力）
            tween.TweenProperty(staticModel, "position", basePos, 0.10f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(staticModel, "scale", actionBaseScale, 0.10f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.InOut);
        }

        tween.Finished += () =>
        {
            staticModel.Position = basePos;
            staticModel.Scale = actionBaseScale;
            ActiveTweens.Remove(id);
            StartIdleBreathing(staticModel, basePos, actionBaseScale);
        };

        ActiveTweens[id] = tween;
    }

    private static int ResolveAttackRepeatCount(string cardEntryId)
    {
        if (!string.IsNullOrWhiteSpace(cardEntryId) && AttackRepeatOverrides.TryGetValue(cardEntryId, out var loops))
        {
            return loops;
        }

        return 1;
    }

    private static bool IsDefenseCard(CardModel card, string cardEntryId)
    {
        if (card.Tags.Contains(CardTag.Defend))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(cardEntryId) && DefenseCardOverrides.Contains(cardEntryId);
    }

    private static bool IsChargeCard(CardModel card, string cardEntryId)
    {
        return !string.IsNullOrWhiteSpace(cardEntryId) && ChargeCardOverrides.Contains(cardEntryId);
    }

    private static void PlayHitTween(Sprite2D staticModel, Vector2 basePos, Vector2 baseScale)
    {
        var id = staticModel.GetInstanceId();
        StopIdle(staticModel);
        if (ActiveTweens.TryGetValue(id, out var running) && GodotObject.IsInstanceValid(running))
        {
            running.Kill();
        }

        staticModel.Position = basePos;
        staticModel.Scale = baseScale;
        var direction = staticModel.FlipH ? -1f : 1f;

        var recoilPos = basePos + new Vector2(-18f * direction, 0f);
        var recoilScale = baseScale * 0.975f;

        var tween = staticModel.CreateTween();
        tween.SetParallel(false);
        tween.SetPauseMode(Tween.TweenPauseMode.Process);

        tween.TweenProperty(staticModel, "position", recoilPos, 0.06f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(staticModel, "scale", recoilScale, 0.06f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(staticModel, "position", basePos, 0.095f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(staticModel, "scale", baseScale, 0.095f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.InOut);

        tween.Finished += () =>
        {
            staticModel.Position = basePos;
            staticModel.Scale = baseScale;
            ActiveTweens.Remove(id);
            StartIdleBreathing(staticModel, basePos, baseScale);
        };

        ActiveTweens[id] = tween;
    }

    private static void PlaySfx(Node visualsRoot, string playerNodeName, string sfxPath, float volumeDb, bool randomPitch)
    {
        var stream = LoadAudioStream(sfxPath);
        if (stream == null)
        {
            return;
        }

        var player = visualsRoot.GetNodeOrNull<AudioStreamPlayer2D>(playerNodeName);
        if (player == null)
        {
            player = new AudioStreamPlayer2D
            {
                Name = playerNodeName,
                VolumeDb = volumeDb,
            };
            visualsRoot.AddChild(player);
        }

        player.Stream = stream;
        player.VolumeDb = volumeDb;
        player.PitchScale = randomPitch ? (float)GD.RandRange(0.97, 1.03) : 1f;
        player.Play();
    }

    private static AudioStream LoadAudioStream(string path)
    {
        if (AudioCache.TryGetValue(path, out var cached) && cached != null)
        {
            return cached;
        }

        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        var stream = ResourceLoader.Load<AudioStream>(path);
        AudioCache[path] = stream;
        return stream;
    }

    /// <summary>
    /// 待机呼吸：让静止立绘围绕存活基准做轻微上下浮动 + 缩放呼吸（循环 tween）。纯表现，绝不影响战斗/网络。
    /// 出招与受击 tween 会先 StopIdle、结束后再 StartIdleBreathing，避免互相打架与位置漂移。
    /// 若此刻正有出招/受击 tween 在跑则跳过（由那条 tween 结束时负责重启），避免叠加。
    /// </summary>
    public static void StartIdleBreathing(Sprite2D staticModel, Vector2 restPos, Vector2 restScale)
    {
        if (staticModel == null || !GodotObject.IsInstanceValid(staticModel))
        {
            return;
        }

        var id = staticModel.GetInstanceId();

        if (ActiveTweens.TryGetValue(id, out var action) && GodotObject.IsInstanceValid(action) && action.IsRunning())
        {
            return;
        }

        if (IdleTweens.TryGetValue(id, out var existing) && GodotObject.IsInstanceValid(existing))
        {
            existing.Kill();
        }

        staticModel.Position = restPos;
        staticModel.Scale = restScale;

        try
        {
            var tween = staticModel.CreateTween();
            tween.SetLoops();
            tween.SetPauseMode(Tween.TweenPauseMode.Process);

            var breatheScale = restScale * 1.018f;
            tween.TweenProperty(staticModel, "position:y", restPos.Y - 2.8f, 1.3f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(staticModel, "scale", breatheScale, 1.3f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(staticModel, "position:y", restPos.Y, 1.3f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(staticModel, "scale", restScale, 1.3f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

            IdleTweens[id] = tween;
        }
        catch
        {
            // Presentation-only.
        }
    }

    public static void StopIdle(Sprite2D staticModel)
    {
        if (staticModel == null)
        {
            return;
        }

        var id = staticModel.GetInstanceId();
        if (IdleTweens.TryGetValue(id, out var t) && GodotObject.IsInstanceValid(t))
        {
            t.Kill();
        }

        IdleTweens.Remove(id);
    }

    /// <summary>
    /// 通用"动作 pose 临时替换"：把 staticModel 贴图临时换成指定 pose，约 durationSeconds 后恢复。纯表现。
    /// 复用 KamehamehaPoseOriginalTextures 记录原贴图（重入只记一次）；形态切换时 Refresh 会强制还原。
    /// posePath 为空（缺图）则什么都不做 → 自动 fallback 到"只有位移"的旧表现，绝不崩。
    /// </summary>
    private const float PoseCrossfadeSeconds = 0.1f;

    /// <summary>
    /// 贴图柔和过渡：在 staticModel 上叠一个临时子精灵显示"旧贴图"，把主精灵换成新贴图，让旧贴图在
    /// ~duration 内淡出 → 视觉上是淡入淡出而非硬切。子精灵继承父变换（出招位移/呼吸时一起动），淡完即释放。纯表现。
    /// </summary>
    private static void CrossfadeTexture(Sprite2D staticModel, Texture2D newTex, float duration)
    {
        if (staticModel == null || !GodotObject.IsInstanceValid(staticModel) || newTex == null)
        {
            return;
        }

        var oldTex = staticModel.Texture;
        if (oldTex == null || oldTex == newTex)
        {
            staticModel.Texture = newTex;
            return;
        }

        var prev = staticModel.GetNodeOrNull<Sprite2D>("KakarotPoseCrossfade");
        if (prev != null)
        {
            prev.QueueFree();
        }

        var overlay = new Sprite2D
        {
            Name = "KakarotPoseCrossfade",
            Texture = oldTex,
            Centered = staticModel.Centered,
            FlipH = staticModel.FlipH,
            FlipV = staticModel.FlipV,
        };
        staticModel.AddChild(overlay);

        staticModel.Texture = newTex;

        var tween = overlay.CreateTween();
        tween.TweenProperty(overlay, "modulate:a", 0f, duration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(overlay))
            {
                overlay.QueueFree();
            }
        };
    }

    private static void TrySwapToPose(Creature creature, Sprite2D staticModel, string posePath, float durationSeconds)
    {
        try
        {
            if (creature == null || staticModel == null || string.IsNullOrEmpty(posePath))
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
            CrossfadeTexture(staticModel, poseTex, PoseCrossfadeSeconds);

            var tree = staticModel.GetTree();
            if (tree != null)
            {
                tree.CreateTimer(durationSeconds).Timeout += () =>
                {
                    TryRestoreFromKamehamehaPose(staticModel, true);
                };
            }
            else
            {
                TryRestoreFromKamehamehaPose(staticModel);
            }
        }
        catch
        {
            // Presentation-only.
        }
    }

    private static bool IsKakarot(Creature creature)
    {
        var entry = creature.Player?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(entry) && entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }
}
