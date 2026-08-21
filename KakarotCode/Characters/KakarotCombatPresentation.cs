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

// Combat presentation is isolated from synchronized gameplay state.
public static partial class KakarotCombatPresentation
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

    private const string NimbusCloudSfxPath = "res://Kakarot/Audio/sfx/combat/skill/sfx_nimbus_cloud.wav";
    private const string SpiritBombSfxNodeName = "SpiritBombSfx";
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

    private const string NimbusCloudSfxNodeName = "NimbusCloudSfx";

    private const string NimbusCloudCardId = "KAKAROTMOD-KAKAROT_NIMBUS_CLOUD";

    private static readonly Dictionary<ulong, Tween> ActiveTweens = new();
    private static readonly Dictionary<ulong, Tween> IdleTweens = new();
    private static readonly Dictionary<string, AudioStream> AudioCache = new();

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
        ["KAKAROTMOD-KAKAROT_NIMBUS_CLOUD"] = (NimbusCloudSfxPath, NimbusCloudSfxNodeName),
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
                // 元气弹是"举起来蓄力再扔"，不该有近战前冲，也不该切出拳姿势。
                var isSpiritBomb = string.Equals(cardEntryId, SpiritBombCardId, StringComparison.OrdinalIgnoreCase);
                if (!isSpiritBomb)
                {
                    PlayWindupTween(staticModel, repeatCount, windupRest.Pos, windupRest.Scale, windupScaleMultiplier);
                }

                if (!string.IsNullOrWhiteSpace(cardEntryId) && SpecialAttackSfxOverrides.TryGetValue(cardEntryId, out var special))
                {
                    PlaySfx(visualsRoot, special.NodeName, special.Path, special.VolumeDb, true);
                    // Beam cards use the optional scene, then fall back to generated light.
                    if (string.Equals(special.Path, KamehamehaSfxPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (useKamehamehaPose)
                        {
                            TrySwapToKamehamehaPose(player.Creature, staticModel);
                        }
                        TryPlayKamehamehaBeamVfx(player, cardPlay, visualsRoot, staticModel);
                    }
                    else if (string.Equals(cardEntryId, DragonFistBurstCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        // 这条分支原本只给龟波切姿势，龙拳落进来就成了"站着不动放大招"。
                        TrySwapToPose(
                            player.Creature,
                            staticModel,
                            KakarotFormVisuals.ResolveAttackPosePath(player.Creature),
                            0.42f);
                        TryPlayDragonFistVfx(player, cardPlay, visualsRoot, staticModel);
                    }
                }
                else if (!isSpiritBomb)
                {
                    PlaySfx(visualsRoot, AttackSfxNodeName, AttackSfxPath, -4f, true);
                    // Keep the attack pose active across the full multi-hit lunge.
                    var attackPoseDuration = 0.14f * Math.Clamp(repeatCount, 1, 5) + 0.12f;
                    TrySwapToPose(player.Creature, staticModel, KakarotFormVisuals.ResolveAttackPosePath(player.Creature), attackPoseDuration);
                    ScheduleMeleeImpactFx(staticModel, visualsRoot, player, cardPlay, repeatCount);

                    if (string.Equals(cardEntryId, RuyiStaffCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        TryPlayRuyiStaffVfx(player, cardPlay, visualsRoot, staticModel);
                    }

                    if (string.Equals(cardEntryId, DestructionCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        TryPlayDestructionVfx(player, cardPlay, visualsRoot, staticModel);
                    }

                    // 自爆的炸点在自己身上，所以走这里而不是 WithHitVfxNode。
                    if (string.Equals(cardEntryId, SelfDestructCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        TryPlaySelfDestructVfx(player, visualsRoot, staticModel);
                    }

                    if (string.Equals(cardEntryId, AfterimageFistCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        _afterimageCaster = player.Creature;
                        _afterimageCasterModel = staticModel;
                    }
                }

                if (isSpiritBomb)
                {
                    PlaySpiritBombVfx(player, cardPlay, visualsRoot, staticModel);
                }
                return;
            }

            // 解析器会过滤无关卡牌；这里只发起纯表现播放。
            KakarotAuraVfx.TryPlay(staticModel, player.Creature, cardEntryId);

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
                // 筋斗云是一条平直无起音的持续哨音，没有瞬态穿不透战斗混音，
                // 源文件已归一到 -1dBFS，播放再单独抬到 0dB；其余专属音维持 -4dB。
                float db = string.Equals(cardEntryId, NimbusCloudCardId, StringComparison.OrdinalIgnoreCase) ? 0f : -4f;
                PlaySfx(visualsRoot, skillSfx.NodeName, skillSfx.Path, db, true);
            }
            else if (cardPlay.Card.Type is CardType.Skill or CardType.Power)
            {
                // Power 类型原本完全没有音效分支，筋斗云/战斗节奏/赛亚觉醒等一整类卡是哑的。
                // 通用技能音先兜底；需要专属音的卡往 SpecialSkillSfxOverrides 里加一行即可。
                PlaySfx(visualsRoot, SkillSfxNodeName, SkillSfxPath, -5f, true);
            }
        }
        catch
        {
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
            TrySwapToPose(creature, staticModel, KakarotFormVisuals.ResolveHurtPosePath(creature), 0.22f);
        }
        catch
        {
        }
    }

    private const string KamehamehaBeamTexturePath = "res://Kakarot/Images/Vfx/kamehameha_beam.png";

    private const string RuyiStaffCardId = "KAKAROTMOD-KAKAROT_RUYI_STAFF";

    private const string DestructionCardId = "KAKAROTMOD-KAKAROT_DESTRUCTION";

    // 破坏（破壊）：紫色的「抹除」。动作仍是普通攻击，特效落在目标那一侧。
    private const int DestructionMoteCount = 20;

    private const int DestructionEmberCount = 14;

    private const int DestructionShardCount = 16;

    private static readonly Color DestructionCoreColor = new(0.78f, 0.42f, 1f);

    private static readonly Color DestructionEdgeColor = new(0.34f, 0.03f, 0.52f);

    private const string AfterimageFistCardId = "KAKAROTMOD-KAKAROT_AFTERIMAGE_FIST";

    private const float RuyiStaffThickness = 15f;

    private const float RuyiStaffCapThickness = 21f;

    // 龙珠的如意棒是纯红一根，两端没有金箍（有金箍的是西游记的金箍棒）。
    // 端箍实现保留着，想换回去把这里改 true 即可。
    // 金箍造型做好了但龙珠原作的如意棒是纯红无箍，按原作走。
    // 用 static readonly 而不是 const：const 会被常量折叠，让下面整块变成不可达代码触发 CS0162。
    private static readonly bool RuyiStaffShowCaps = false;

    private static int _afterimageHitIndex;

    // 残像拳段数由 energyX 决定，打牌时算不出总时长。
    // 记住施法者，每段命中把攻击立绘的保持时间往后顶一次。
    private static Creature _afterimageCaster;

    private static Sprite2D _afterimageCasterModel;

    private const string WolfFangHeadTexturePath = "res://Kakarot/Images/Vfx/kakarot_wolf_fang_head.png";

    // 残影观感：很淡、偏冷灰。数值故意压得低，宁可不够也不要糊成一团。
    private static readonly Color WolfFangTint = new(0.78f, 0.80f, 0.86f, 0.42f);

    private const float WolfFangHeadHeight = 260f;

    // 狼牙每段命中的白光强度。调这一个数就行，0 就是完全没有白光。
    private const float WolfFangFlashAlpha = 0.34f;

    // 每段命中位置递进，靠这个计数器实现（纯表现层，不进同步状态）。
    private static int _wolfFangHitIndex;

    private const string SpiritBombRaisePosePath = "res://Kakarot/Images/Charui/kakarot_combat_model_spirit_bomb_raise_pose.png";

    private const string SpiritBombThrowPosePath = "res://Kakarot/Images/Charui/kakarot_combat_model_spirit_bomb_throw_pose.png";

    private const string DragonFistDragonTexturePath = "res://Kakarot/Images/Vfx/kakarot_dragon_fist_dragon.png";

    private const string DragonFistBurstCardId = "KAKAROTMOD-KAKAROT_DRAGON_FIST_BURST";

    // 龙头贴在画面右端，所以锚点放在龙头上、龙身向后拖。
    private const float DragonFistTargetHeight = 300f;

    private const float DragonFistFlightSeconds = 0.34f;

    private const string KamehamehaBeamShaderPath = "res://Kakarot/Shaders/kakarot_kamehameha_beam.gdshader";

    // 暗色变体：blend_mix，能真正遮住背景。加法混合画不出黑。
    private const string DarkBeamShaderPath = "res://Kakarot/Shaders/kakarot_energy_beam_dark.gdshader";

    // 调试开关：true 走程序化 shader，false 退回原来的贴图/场景路径。
    private const bool KamehamehaUseShaderBeam = true;

    private const float KamehamehaShaderBeamThickness = 292f;

    private const float KamehamehaShaderBeamGrowSeconds = 0.26f;

    private const float KamehamehaShaderBeamHoldSeconds = 0.34f;

    private const float KamehamehaShaderBeamFadeSeconds = 0.30f;

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

    // 程序化光束：形状与辉光全部由 shader 计算，不依赖 kamehameha_beam.png。
    // 失败时返回 false，调用方会自动退回贴图路径。
    // 横向棍贴图：宽度只有 4 像素（沿长度方向拉伸），明暗做在高度方向上，
    // 也就是棍子的粗细方向。这样 sprite 不用旋转，长宽缩放各管各的。
    private static ImageTexture CreateCylinderTextureH(
        int thicknessPixels,
        Color edge,
        Color body,
        Color highlight,
        float highlightAt)
    {
        const int width = 4;
        var img = Image.CreateEmpty(width, thicknessPixels, false, Image.Format.Rgba8);
        for (int y = 0; y < thicknessPixels; y++)
        {
            float t = thicknessPixels <= 1 ? 0.5f : y / (float)(thicknessPixels - 1);
            float rim = Mathf.Pow(Math.Abs(t - 0.5f) * 2f, 1.5f);
            Color c = body.Lerp(edge, rim);
            float spec = Mathf.Exp(-Mathf.Pow((t - highlightAt) / 0.13f, 2f));
            c = c.Lerp(highlight, spec * 0.85f);
            for (int x = 0; x < width; x++)
            {
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 沿宽度做圆柱明暗的贴图：边缘压暗 → 本体 → 偏一侧的高光。
    // 一根纯色矩形读起来是纸片，加上这条明暗曲线才像一根圆棍。
    private static ImageTexture CreateCylinderTexture(
        int width,
        Color edge,
        Color body,
        Color highlight,
        float highlightAt)
    {
        const int height = 4;
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int x = 0; x < width; x++)
        {
            float t = width <= 1 ? 0.5f : x / (float)(width - 1);

            // 到中轴的距离决定明暗，边缘最暗
            float rim = Mathf.Pow(Math.Abs(t - 0.5f) * 2f, 1.5f);
            Color c = body.Lerp(edge, rim);

            // 高光带：偏离中轴一点，才有受光方向
            float spec = Mathf.Exp(-Mathf.Pow((t - highlightAt) / 0.13f, 2f));
            c = c.Lerp(highlight, spec * 0.85f);

            for (int y = 0; y < height; y++)
            {
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 纯色硬边贴图。GradientTexture2D 是异步生成的，就绪前 UV 会退化，别用它。
    private static ImageTexture CreateSolidTexture(int width, int height)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        return ImageTexture.CreateFromImage(img);
    }

    // 沿飞行路径按时间撒光点，跟着龙头走，视觉上是拖尾。
    private static void SpawnDragonFistTrail(Node2D anchor, Vector2 originWorld, Vector2 impactWorld)
    {
        const int trailCount = 10;
        for (int i = 0; i < trailCount; i++)
        {
            float t = (i + 1f) / (trailCount + 1f);
            Vector2 at = originWorld.Lerp(impactWorld, t) + new Vector2(0f, Mathf.Sin(t * 6.28318f) * 18f);

            var mote = CreateRadialGlowSprite(0.9f, 0f);
            float size = Mathf.Lerp(0.10f, 0.20f, 1f - t);
            mote.Scale = new Vector2(size, size);
            mote.Modulate = new Color(1f, 0.82f, 0.35f, 0f);
            mote.ZIndex = 25;
            anchor.AddChild(mote);
            mote.GlobalPosition = at;

            var tw = mote.CreateTween();
            tw.SetParallel(false);
            tw.TweenInterval(t * DragonFistFlightSeconds);
            tw.TweenProperty(mote, "modulate:a", 0.85f, 0.05);
            tw.TweenProperty(mote, "modulate:a", 0f, 0.30);
            tw.Parallel().TweenProperty(mote, "scale", new Vector2(size * 0.3f, size * 0.3f), 0.30);
            tw.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(mote))
                {
                    mote.QueueFree();
                }
            }));
        }
    }

    // 量出存活敌人的横向跨度，让如意棒横跨整排而不是只压住一只。
    private static float ResolveRuyiStaffSpan(Player player, out float centerX)
    {
        const float baseLength = 460f;
        const float maxLength = 980f;
        const float padding = 150f;

        centerX = 0f;
        var combatState = player?.Creature?.CombatState;
        var room = NCombatRoom.Instance;
        if (combatState == null || room == null)
        {
            return baseLength;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (Creature enemy in combatState.HittableEnemies)
        {
            if (enemy is not { CurrentHp: > 0 })
            {
                continue;
            }

            var node = room.GetCreatureNode(enemy);
            if (node == null)
            {
                continue;
            }

            minX = Math.Min(minX, node.GlobalPosition.X);
            maxX = Math.Max(maxX, node.GlobalPosition.X);
        }

        if (minX > maxX)
        {
            return baseLength;
        }

        centerX = (minX + maxX) * 0.5f;
        return Math.Clamp(maxX - minX + padding, baseLength, maxLength);
    }

    // 如意棒：在敌人正前方横空出现一根长棍，高高扬起后劈下，最终砸成水平。
    // 🔴 holder 必须做 GlobalScale 归一：长度/粗细都是世界像素，
    // 而父节点（角色 visuals）带缩放，不归一会被缩成一根牙签。
    private static void TryPlayRuyiStaffVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            Creature target = ResolvePrimaryBeamTarget(player, cardPlay);
            if (target == null || !TryGetCreatureChestWorld(target, out Vector2 chestWorld))
            {
                return;
            }

            // 棍长随敌人阵型走：单只时是基础长度，一群时横跨整排。
            float staffLength = ResolveRuyiStaffSpan(player, out float spanCenterX);

            var holder = new Node2D { Name = "KakarotRuyiStaffFx", ZIndex = 40 };
            anchor.AddChild(holder);
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = new Vector2(spanCenterX, chestWorld.Y - 26f);
            holder.GlobalRotation = Mathf.DegToRad(-78f);

            // 棍身沿局部 X 铺开 —— 旋转 0 度就是水平，正好是"劈到水平"的终点姿态。
            const int shadeRes = 48;
            var shaftTex = CreateCylinderTextureH(
                shadeRes,
                new Color(0.20f, 0.03f, 0.04f),
                new Color(0.80f, 0.12f, 0.11f),
                new Color(1.0f, 0.66f, 0.58f),
                0.36f);

            ImageTexture capTex = RuyiStaffShowCaps
                ? CreateCylinderTextureH(
                    shadeRes,
                    new Color(0.28f, 0.17f, 0.03f),
                    new Color(0.95f, 0.74f, 0.22f),
                    new Color(1.0f, 0.98f, 0.80f),
                    0.36f)
                : null;

            var shaft = new Sprite2D
            {
                Texture = shaftTex,
                Centered = true,
                // 贴图宽 4 高 shadeRes：X 铺长度，Y 铺粗细，不用旋转
                Scale = new Vector2(staffLength / 4f, RuyiStaffThickness / shadeRes),
            };
            holder.AddChild(shaft);

            Sprite2D capLeft = null;
            Sprite2D capRight = null;
            if (RuyiStaffShowCaps)
            {
                Sprite2D MakeCap(float x) => new()
                {
                    Texture = capTex,
                    Centered = true,
                    Scale = new Vector2(14f / 4f, RuyiStaffCapThickness / shadeRes),
                    Position = new Vector2(x, 0f),
                };

                capLeft = MakeCap(-staffLength * 0.5f + 7f);
                holder.AddChild(capLeft);

                capRight = MakeCap(staffLength * 0.5f - 7f);
                holder.AddChild(capRight);
            }

            // 出现时从零长度弹出，避免凭空贴一根棍子上去
            shaft.Scale = new Vector2(0.5f, RuyiStaffThickness / shadeRes);

            var tween = holder.CreateTween();
            tween.SetParallel(false);

            // ① 成形：棍身横向拉满
            tween.TweenProperty(shaft, "scale", new Vector2(staffLength / 4f, RuyiStaffThickness / shadeRes), 0.14)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);

            // ② 扬起蓄力
            tween.TweenProperty(holder, "rotation", Mathf.DegToRad(-96f), 0.10)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);

            // ③ 劈下，砸成水平
            tween.TweenProperty(holder, "rotation", 0f, 0.08)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.In);

            tween.TweenCallback(Callable.From(() =>
            {
                SpawnRuyiSwingArc(anchor, chestWorld, staffLength);
                PlayImpactFeedback(ShakeStrength.Medium, ShakeDuration.Short, hitStop: true);
                SpawnRadialBurst(anchor, chestWorld, 2.4f, 0.30f);
            }));

            // ④ 砸实后的余震，再淡出
            tween.TweenProperty(holder, "rotation", Mathf.DegToRad(4f), 0.06)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tween.TweenInterval(0.08);
            tween.TweenProperty(shaft, "modulate:a", 0f, 0.18);
            if (capLeft != null)
            {
                tween.Parallel().TweenProperty(capLeft, "modulate:a", 0f, 0.18);
            }

            if (capRight != null)
            {
                tween.Parallel().TweenProperty(capRight, "modulate:a", 0f, 0.18);
            }
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Ruyi staff failed: {ex}");
        }
    }

    // 劈下瞬间在落点甩一道弧形残影，读作"挥过去的轨迹"。
    private static void SpawnRuyiSwingArc(Node2D anchor, Vector2 atWorld, float staffLength)
    {
        for (int i = 0; i < 3; i++)
        {
            var ghost = new Sprite2D
            {
                Texture = CreateStreakTexture(),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 0.55f, 0.45f, 0.34f - i * 0.09f),
                Scale = new Vector2(staffLength / 128f * 0.9f, 0.55f),
                Rotation = Mathf.DegToRad(-52f + i * 22f),
                ZIndex = 32,
            };
            anchor.AddChild(ghost);
            ghost.GlobalPosition = atWorld + new Vector2(0f, -staffLength * 0.22f);

            var tw = ghost.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(ghost, "modulate:a", 0f, 0.18 + i * 0.03);
            tw.TweenProperty(ghost, "rotation", Mathf.DegToRad(6f + i * 10f), 0.14);
            tw.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(ghost))
                {
                    ghost.QueueFree();
                }
            }));
        }
    }

    // 残像拳：每段命中留一道拳风。段数由 energyX 决定，只能走逐段回调。
    public static Node2D CreateAfterimageHitVfx(Creature enemy)
    {
        try
        {
            int index = _afterimageHitIndex++;

            // 每段命中把攻击姿势续期，打完最后一段后 0.45 秒才切回站姿。
            if (_afterimageCaster != null && GodotObject.IsInstanceValid(_afterimageCasterModel))
            {
                TrySwapToPose(
                    _afterimageCaster,
                    _afterimageCasterModel,
                    KakarotFormVisuals.ResolveAttackPosePath(_afterimageCaster),
                    0.45f);
            }

            var holder = new Node2D { Name = "KakarotAfterimageFx", ZIndex = 31 };

            if (TryGetCreatureChestWorld(enemy, out Vector2 chestWorld))
            {
                holder.TreeEntered += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.GlobalPosition = chestWorld;
                    }
                };
            }

            // 角度与高度逐段交替，读起来像连打而不是同一道风重复播放。
            float angle = Mathf.DegToRad(index % 2 == 0 ? -22f : 18f);
            float offsetY = -30f + (index % 3) * 26f;

            var streak = new Sprite2D
            {
                Texture = CreateStreakTexture(),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(0.92f, 0.96f, 1f, 0.75f),
                Scale = new Vector2(1.5f, 0.9f),
                Position = new Vector2(-40f, offsetY),
                Rotation = angle,
            };
            holder.AddChild(streak);

            var tween = holder.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(streak, "position", new Vector2(46f, offsetY), 0.16)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(streak, "modulate:a", 0f, 0.18);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));

            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Afterimage vfx failed: {ex}");
            return null;
        }
    }

    // 狼牙风风拳每一段命中生成一个狼头残影。
    // 由引擎的 WithHitVfxNode 逐段调用 —— 这是唯一能与真实段数同步的挂点，
    // 在 OnPlay 里播 4 次会因为多段共用一条 AttackCommand 而对不上。
    public static Node2D CreateWolfFangHitVfx(Creature enemy)
    {
        try
        {
            if (!ResourceLoader.Exists(WolfFangHeadTexturePath))
            {
                GD.PrintErr($"[Kakarot][Vfx] wolf head missing: {WolfFangHeadTexturePath}");
                return null;
            }

            var texture = ResourceLoader.Load<Texture2D>(WolfFangHeadTexturePath);
            if (texture == null)
            {
                return null;
            }

            int index = _wolfFangHitIndex++ & 3;

            var holder = new Node2D { Name = "KakarotWolfFangFx", ZIndex = 31 };

            // 引擎会把这个节点挂到自己选的父节点上，不一定是敌人。
            // 不主动定位的话 (0,0) 会落在画布原点——表现就是特效跑到屏幕左上角。
            // 入树后再设全局坐标，此时父链才成立。
            if (TryGetCreatureChestWorld(enemy, out Vector2 enemyChestWorld))
            {
                holder.TreeEntered += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.GlobalPosition = enemyChestWorld;
                    }
                };
            }

            // 原版 vfx_attack_slash 的白爆亮度改不了，4 段 × 全体敌人会叠成一片，
            // 把狼头盖住。改成自己画一层，亮度在这一个数上。
            var flash = new Sprite2D
            {
                Texture = GetKiGlowTexture(),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(0.86f, 0.92f, 1f, WolfFangFlashAlpha),
                Scale = new Vector2(0.55f, 0.55f),
                ZIndex = -1,
            };
            holder.AddChild(flash);
            var flashTween = flash.CreateTween();
            flashTween.SetParallel(true);
            flashTween.TweenProperty(flash, "scale", new Vector2(1.15f, 0.95f), 0.16)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            flashTween.TweenProperty(flash, "modulate:a", 0f, 0.20)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            flashTween.Chain().TweenCallback(Callable.From(() => FreeIfValid(flash)));

            float scale = WolfFangHeadHeight / Math.Max(texture.GetHeight(), 1);
            var sprite = new Sprite2D
            {
                Texture = texture,
                Centered = true,
                Modulate = WolfFangTint,
                Scale = new Vector2(scale * 0.82f, scale * 0.82f),
                // 四段沿斜向递进，读起来像连续扑咬而不是原地闪四下。
                Position = new Vector2(-70f + index * 34f, -46f + index * 24f),
                Rotation = Mathf.DegToRad(-10f + index * 6f),
            };
            holder.AddChild(sprite);

            var tween = holder.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(sprite, "scale", new Vector2(scale * 1.12f, scale * 1.12f), 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sprite, "position", sprite.Position + new Vector2(38f, -8f), 0.20)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sprite, "modulate:a", 0f, 0.24).SetDelay(0.06);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));

            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Wolf fang vfx failed: {ex}");
            return null;
        }
    }

    // 龙拳爆发：金龙从施法者拳头窜出，龙头咬向目标。
    private static void TryPlayDragonFistVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            if (!ResourceLoader.Exists(DragonFistDragonTexturePath))
            {
                GD.PrintErr($"[Kakarot][Vfx] dragon texture missing: {DragonFistDragonTexturePath}");
                return;
            }

            var texture = ResourceLoader.Load<Texture2D>(DragonFistDragonTexturePath);
            if (texture == null)
            {
                GD.PrintErr("[Kakarot][Vfx] dragon texture failed to load.");
                return;
            }

            Creature target = ResolvePrimaryBeamTarget(player, cardPlay);
            if (cardPlay.Target != null)
            {
                KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
            }

            float dir = staticModel.FlipH ? -1f : 1f;
            Vector2 originWorld = staticModel.GlobalPosition + new Vector2(70f * dir, -70f);
            Vector2 impactWorld;
            if (target != null && TryGetCreatureChestWorld(target, out Vector2 chestWorld))
            {
                // 只取目标的横坐标，高度保持与出拳点齐平 —— 否则龙会斜着扎向敌人脚下。
                impactWorld = new Vector2(chestWorld.X, originWorld.Y);
            }
            else
            {
                impactWorld = originWorld + new Vector2(560f * dir, 0f);
            }

            var holder = new Node2D { Name = "KakarotDragonFistFx", ZIndex = 26 };
            anchor.AddChild(holder);
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = originWorld;

            float angle = (impactWorld - originWorld).Angle();
            holder.GlobalRotation = angle;

            int texW = Math.Max(texture.GetWidth(), 1);
            int texH = Math.Max(texture.GetHeight(), 1);
            float fullScale = DragonFistTargetHeight / texH;

            var sprite = new Sprite2D
            {
                Texture = texture,
                Centered = true,
                // 锚点落在龙头，龙身向来路方向拖出去。
                Offset = new Vector2(-texW * 0.5f, 0f),
                Scale = new Vector2(fullScale * 0.35f, fullScale * 0.35f),
                // 朝左发招时整条龙会上下颠倒，翻一次修正。
                FlipV = Mathf.Cos(angle) < 0f,
            };
            holder.AddChild(sprite);

            // 出膛闪光：拳头位置炸一下白光，龙才像是"窜出来"的而不是凭空平移
            var muzzle = CreateRadialGlowSprite(0.95f, 0f);
            muzzle.Scale = new Vector2(0.25f, 0.25f);
            muzzle.ZIndex = 27;
            anchor.AddChild(muzzle);
            muzzle.GlobalPosition = originWorld;
            var muzzleTween = muzzle.CreateTween();
            muzzleTween.SetParallel(true);
            muzzleTween.TweenProperty(muzzle, "scale", new Vector2(1.15f, 1.15f), 0.20)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            muzzleTween.TweenProperty(muzzle, "modulate:a", 0f, 0.24);
            muzzleTween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(muzzle))
                {
                    muzzle.QueueFree();
                }
            }));

            var tween = holder.CreateTween();
            tween.SetParallel(false);
            tween.TweenProperty(holder, "global_position", impactWorld, DragonFistFlightSeconds)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(sprite, "scale", new Vector2(fullScale, fullScale), DragonFistFlightSeconds)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

            SpawnDragonFistTrail(anchor, originWorld, impactWorld);

            tween.TweenCallback(Callable.From(() =>
            {
                PlayImpactFeedback(ShakeStrength.Strong, ShakeDuration.Normal, hitStop: true);
                SpawnRadialBurst(anchor, impactWorld, 3.4f, 0.38f);
            }));
            tween.TweenProperty(sprite, "modulate:a", 0f, 0.22);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Dragon fist failed: {ex}");
        }
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
            KamehamehaShaderBeamFadeSeconds);
    }

    // 程序化能量束。龟波和弗利萨的死亡光线共用这一份实现——
    // 两套分叉的结果就是修好一边另一边还是墙。粗细与三段时长是唯一区别。
    // container 收 Node 而不是 Node2D：战斗特效容器是 Control。
    internal static bool SpawnEnergyBeam(
        Node container,
        Vector2 originWorld,
        Vector2 endWorld,
        Color beamColor,
        Color coreColor,
        float thicknessPixels,
        float growSeconds,
        float holdSeconds,
        float fadeSeconds,
        bool dark = false)
    {
        string shaderPath = dark ? DarkBeamShaderPath : KamehamehaBeamShaderPath;
        if (!ResourceLoader.Exists(shaderPath))
        {
            GD.PrintErr($"[Kakarot][Vfx] beam shader missing: {shaderPath}");
            return false;
        }

        var shader = ResourceLoader.Load<Shader>(shaderPath);
        if (shader == null)
        {
            GD.PrintErr("[Kakarot][Vfx] beam shader failed to load.");
            return false;
        }

        var noise = new NoiseTexture2D
        {
            Width = 256,
            Height = 256,
            Seamless = true,
            Noise = new FastNoiseLite
            {
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                Frequency = 0.014f,
                FractalOctaves = 3,
            },
        };

        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("noise_tex", noise);
        material.SetShaderParameter("beam_color", beamColor);
        material.SetShaderParameter("core_color", coreColor);
        material.SetShaderParameter("progress", 0f);
        material.SetShaderParameter("intensity", 1f);

        var holder = new Node2D { Name = "KakarotEnergyBeamFx", ZIndex = 24 };
        container.AddChild(holder);
        holder.GlobalPosition = originWorld;
        // originWorld / endWorld 是世界坐标，而 Scale 走的是局部坐标。
        // anchor 自身带缩放时两者不等价，光束会又细又长，所以先把继承缩放归一。
        holder.GlobalScale = Vector2.One;
        holder.GlobalRotation = (endWorld - originWorld).Angle();

        // 画布只提供 UV 网格，颜色全部由 shader 覆写。
        // 🔴 必须用 ImageTexture：GradientTexture2D 是异步生成的，贴图就绪前 UV 不会正确
        // 铺满 0~1，整块面积会退化成一条线。这个坑排查过，别换回去。
        const int canvasWidth = 256;
        const int canvasHeight = 64;
        var canvasImage = Image.CreateEmpty(canvasWidth, canvasHeight, false, Image.Format.Rgba8);
        canvasImage.Fill(Colors.White);
        var canvas = ImageTexture.CreateFromImage(canvasImage);

        float distance = Math.Max(originWorld.DistanceTo(endWorld), 1f);
        var sprite = new Sprite2D
        {
            Texture = canvas,
            Centered = true,
            Offset = new Vector2(canvasWidth * 0.5f, 0f),
            Material = material,
            Scale = new Vector2(distance / canvasWidth, thicknessPixels / canvasHeight),
        };
        holder.AddChild(sprite);

        var tween = holder.CreateTween();
        tween.TweenProperty(material, "shader_parameter/progress", 1f, growSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(holdSeconds);
        tween.TweenProperty(material, "shader_parameter/intensity", 0f, fadeSeconds);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(holder))
            {
                holder.QueueFree();
            }
        };

        return true;
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

    internal static CanvasItemMaterial CreateAdditiveMaterial()
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

    // 元气弹演出：聚 → 成型 → 投掷 → 散。
    // 汇聚过程是这一招的辨识度所在（"把大家的元气分给我"），静态球体浪费了这张牌。
    private const int SpiritBombMoteCount = 22;
    private const float SpiritBombGatherSeconds = 1.05f;
    private const float SpiritBombMoteMinRadius = 620f;
    private const float SpiritBombMoteMaxRadius = 1250f;

    private static void PlaySpiritBombVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            var target = ResolvePrimaryBeamTarget(player, cardPlay);
            if (cardPlay.Target != null)
            {
                KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
            }

            Vector2 impactWorld;
            if (target == null || !TryGetCreatureChestWorld(target, out impactWorld))
            {
                float dirFallback = staticModel.FlipH ? -1f : 1f;
                impactWorld = staticModel.GlobalPosition + new Vector2(420f * dirFallback, -40f);
            }

            // 球在举手的悟空头顶成型，而不是敌人头顶。
            var gatherWorld = staticModel.GlobalPosition + new Vector2(0f, -300f);

            var holder = new Node2D { Name = "KakarotSpiritBombFx", ZIndex = 30 };
            anchor.AddChild(holder);
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = gatherWorld;

            var glow = CreateRadialGlowSprite(0.55f, 0f);
            glow.Scale = new Vector2(0.06f, 0.06f);
            holder.AddChild(glow);

            var core = CreateRadialGlowSprite(0.95f, 0f);
            core.Scale = new Vector2(0.03f, 0.03f);
            holder.AddChild(core);

            SpawnSpiritBombMotes(anchor, gatherWorld);

            // 举手贯穿「汇聚+成型」，投掷瞬间换成下挥，整段结束才复原成站姿。
            const float spiritBombPoseSeconds =
                SpiritBombGatherSeconds + 0.18f + 0.26f + 0.45f;
            TrySwapToPose(player.Creature, staticModel, SpiritBombRaisePosePath, spiritBombPoseSeconds);

            var tween = holder.CreateTween();
            tween.SetParallel(false);
            tween.SetPauseMode(Tween.TweenPauseMode.Process);

            // ① 汇聚：球随着元气飞入而长大
            tween.TweenProperty(glow, "scale", new Vector2(3.0f, 3.0f), SpiritBombGatherSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(core, "scale", new Vector2(1.7f, 1.7f), SpiritBombGatherSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

            // ② 成型停顿 + 预备动作：身体先后拉蓄力，再甩出去
            tween.TweenCallback(Callable.From(() => PlaySpiritBombThrowMotion(player.Creature, staticModel)));
            tween.TweenInterval(0.18);

            // ③ 投掷
            tween.TweenProperty(holder, "global_position", impactWorld, 0.26)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            // ④ 扩散
            tween.TweenCallback(Callable.From(() =>
            {
                // 音效跟着落点走，不再跟出拳那一下绑定。
                PlaySfx(visualsRoot, SpiritBombSfxNodeName, HitSfxPath, -2f, true);
                PlayImpactFeedback(ShakeStrength.Strong, ShakeDuration.Normal, hitStop: true);
                SpawnRadialBurst(anchor, impactWorld, 5.0f, 0.45f);
            }));
            tween.TweenInterval(0.45);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Spirit bomb failed: {ex}");
        }
    }

    // 甩出去的力量感：后拉蓄力 → 前冲甩出 → 回稳。贴图切换卡在"甩出"那一帧，
    // 这样人眼看到的是一个连续动作，而不是一次生硬的换图。
    private static void PlaySpiritBombThrowMotion(Creature creature, Sprite2D staticModel)
    {
        try
        {
            if (creature == null || !GodotObject.IsInstanceValid(staticModel))
            {
                return;
            }

            var rest = KakarotFormVisuals.GetRestTransform(creature);
            float dir = staticModel.FlipH ? -1f : 1f;

            var motion = staticModel.CreateTween();
            motion.SetParallel(false);

            // 后拉：跟成型停顿同长，玩家看到球定住的同时人在蓄力
            motion.TweenProperty(staticModel, "position", rest.Pos + new Vector2(-14f * dir, 0f), 0.18)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);

            motion.TweenCallback(Callable.From(() =>
            {
                var throwTex = ResourceLoader.Load<Texture2D>(SpiritBombThrowPosePath);
                if (throwTex != null && GodotObject.IsInstanceValid(staticModel))
                {
                    // 只换贴图，不动复原簿记——原始站姿已由 TrySwapToPose 记下。
                    CrossfadeTexture(staticModel, throwTex, PoseCrossfadeSeconds);
                }
            }));

            // 甩出：快、带一点过冲
            motion.TweenProperty(staticModel, "position", rest.Pos + new Vector2(30f * dir, 0f), 0.09)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);

            // 回稳：慢，收住余韵
            motion.TweenProperty(staticModel, "position", rest.Pos, 0.34)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Spirit bomb throw motion failed: {ex.Message}");
        }
    }

    // 元气从四面八方飞向汇聚点。偏下半圈生成，读起来像"从大地和众人身上升起"。
    // 破坏：紫色能量向目标收束 → 球体吞没 → 塌缩抹除 → 余烬升腾。
    // 动作沿用普通攻击，所以整段必须压在近战命中的节奏内（约 0.35s 到塌缩），
    // 拖长了就会变成「打完了特效才演」。
    private static void TryPlayDestructionVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            Creature target = cardPlay.Target ?? ResolvePrimaryBeamTarget(player, cardPlay);
            if (target == null || !TryGetCreatureChestWorld(target, out Vector2 targetWorld))
            {
                return;
            }

            var holder = new Node2D { Name = "KakarotDestructionFx", ZIndex = 41 };
            anchor.AddChild(holder);
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = targetWorld;

            // 吞没球：加法混合，从零胀到盖住目标。
            var sphere = new Sprite2D
            {
                Texture = CreateRadialGlowTexture(128, DestructionCoreColor, DestructionEdgeColor),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Scale = new Vector2(0.05f, 0.05f),
                Modulate = new Color(1f, 1f, 1f, 0f),
            };
            holder.AddChild(sphere);

            // 塌缩瞬间的白紫核闪。
            var flash = new Sprite2D
            {
                Texture = CreateRadialGlowTexture(128, new Color(1f, 0.93f, 1f), DestructionCoreColor),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Scale = new Vector2(0.05f, 0.05f),
                Modulate = new Color(1f, 1f, 1f, 0f),
            };
            holder.AddChild(flash);

            SpawnDestructionMotes(anchor, targetWorld);
            TintCreatureDuringErasure(target);

            var tween = holder.CreateTween();
            tween.SetParallel(false);

            // ① 收束成型
            tween.TweenProperty(sphere, "modulate:a", 0.70f, 0.10);
            tween.Parallel().TweenProperty(sphere, "scale", new Vector2(2.2f, 2.2f), 0.16)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

            // ② 吞没：涨到最大压住目标
            tween.TweenProperty(sphere, "scale", new Vector2(3.2f, 3.2f), 0.10)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            // ③ 抹除：向内塌缩——「被压成一点然后消失」
            tween.TweenProperty(sphere, "scale", new Vector2(0.28f, 0.28f), 0.08)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            // 塌缩期间反而提亮：同步淡出会变成「凭空消失」，中间还会空一帧。
            tween.Parallel().TweenProperty(sphere, "modulate:a", 1f, 0.08);

            tween.TweenCallback(Callable.From(() =>
            {
                PlayImpactFeedback(ShakeStrength.Strong, ShakeDuration.Normal, hitStop: true);
                // 不用通用的 SpawnRadialBurst：那是白色的，会把紫色冲淡。
                SpawnDestructionImpact(anchor, targetWorld);
                SpawnDestructionEmbers(anchor, targetWorld);
            }));

            tween.TweenProperty(flash, "modulate:a", 1f, 0.05);
            tween.Parallel().TweenProperty(sphere, "modulate:a", 0f, 0.06);
            tween.Parallel().TweenProperty(flash, "scale", new Vector2(6.2f, 6.2f), 0.20)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(flash, "modulate:a", 0f, 0.22);

            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Destruction failed: {ex}");
        }
    }

    // 紫色碎片从四面八方收束到目标身上。
    private static void SpawnDestructionMotes(Node2D anchor, Vector2 targetWorld)
    {
        var moteTex = CreateRadialGlowTexture(64, DestructionCoreColor, DestructionEdgeColor);

        for (int i = 0; i < DestructionMoteCount; i++)
        {
            float angle = Mathf.Tau * (i / (float)DestructionMoteCount) + GD.Randf() * 0.22f;
            float radius = Mathf.Lerp(210f, 430f, GD.Randf());

            var mote = new Sprite2D
            {
                Texture = moteTex,
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                ZIndex = 40,
            };
            anchor.AddChild(mote);
            // holder 之外的散件同样要做 GlobalScale 归一，否则被父级缩放吃掉。
            float size = 0.5f * Mathf.Lerp(0.5f, 1.1f, GD.Randf());
            mote.GlobalScale = new Vector2(size, size);
            mote.GlobalPosition = targetWorld + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            var tw = mote.CreateTween();
            tw.SetParallel(false);
            tw.TweenInterval(GD.Randf() * 0.06);
            tw.TweenProperty(mote, "modulate:a", 1f, 0.05);
            tw.Parallel().TweenProperty(mote, "global_position", targetWorld, Mathf.Lerp(0.18f, 0.26f, GD.Randf()))
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            tw.TweenProperty(mote, "modulate:a", 0f, 0.05);
            tw.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(mote))
                {
                    mote.QueueFree();
                }
            }));
        }
    }

    // 抹除瞬间的陨石式炸开：横向铺开的冲击波环 + 向外飞的碎片。
    // 去掉原版 vfx_heavy_blunt 之后这一下没有实体，只剩核闪，打击感撑不住。
    private static void SpawnDestructionImpact(Node2D anchor, Vector2 targetWorld)
    {
        // 冲击波环：纵向压扁成 0.42，读作贴地扩散而不是一个正圆气泡。
        var ring = new Sprite2D
        {
            Texture = CreateRingTexture(160, 0.085f, new Color(1f, 0.85f, 1f), DestructionCoreColor),
            Centered = true,
            Material = CreateAdditiveMaterial(),
            Modulate = new Color(1f, 1f, 1f, 0.95f),
            ZIndex = 42,
        };
        anchor.AddChild(ring);
        ring.GlobalScale = new Vector2(0.35f, 0.35f * 0.42f);
        ring.GlobalPosition = targetWorld + new Vector2(0f, 10f);

        var ringTween = ring.CreateTween();
        ringTween.SetParallel(true);
        ringTween.TweenProperty(ring, "global_scale", new Vector2(3.95f, 3.95f * 0.42f), 0.26)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        ringTween.TweenProperty(ring, "modulate:a", 0f, 0.26)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        ringTween.Chain().TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(ring))
            {
                ring.QueueFree();
            }
        }));

        // 放射碎片：尖端朝外。贴图 u=0 宽、u=1 尖，Sprite2D 的 +X 就是 Rotation 指向，
        // 所以直接把 Rotation 设成飞出方向，尖端自然朝外。
        var shardTex = CreateShardTexture(64, 16, DestructionCoreColor, DestructionEdgeColor);

        for (int i = 0; i < DestructionShardCount; i++)
        {
            float angle = Mathf.Tau * (i / (float)DestructionShardCount) + GD.Randf() * 0.30f;
            float speed = Mathf.Lerp(150f, 340f, GD.Randf());
            float size = Mathf.Lerp(0.5f, 1.2f, GD.Randf());

            var shard = new Sprite2D
            {
                Texture = shardTex,
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Rotation = angle,
                Modulate = new Color(1f, 1f, 1f, 1f),
                ZIndex = 42,
            };
            anchor.AddChild(shard);
            shard.GlobalScale = new Vector2(size * 1.1f, size * 0.9f);
            shard.GlobalPosition = targetWorld;

            // 纵向按 0.72 压扁飞散，和冲击波环的透视保持一致。
            var to = targetWorld + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.72f) * speed;

            var tw = shard.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(shard, "global_position", to, 0.24)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(shard, "modulate:a", 0f, 0.24)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(shard))
                {
                    shard.QueueFree();
                }
            }));
        }
    }

    // 环形冲击波贴图：距中心 0.78 处最亮，两侧高斯衰减。
    internal static ImageTexture CreateRingTexture(int size, float thickness, Color inner, Color outer)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d >= 1f)
                {
                    img.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }

                float band = Mathf.Exp(-Mathf.Pow((d - 0.78f) / thickness, 2f));
                Color c = inner.Lerp(outer, Mathf.Clamp((d - 0.5f) / 0.5f, 0f, 1f));
                c.A = band;
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 碎片贴图：横向拉长、一端收成尖。
    internal static ImageTexture CreateShardTexture(int width, int height, Color inner, Color outer)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = width <= 1 ? 0f : x / (float)(width - 1);
                float v = height <= 1 ? 0f : Math.Abs(y - (height - 1) * 0.5f) / ((height - 1) * 0.5f);

                float taper = Mathf.Pow(Mathf.Clamp(1f - u, 0f, 1f), 0.6f);
                float a = Mathf.Pow(Mathf.Clamp(taper - v * 0.9f, 0f, 1f), 1.4f);

                Color c = inner.Lerp(outer, u);
                c.A = a;
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 抹除之后往上飘散的余烬——读作「化为尘埃」。
    private static void SpawnDestructionEmbers(Node2D anchor, Vector2 targetWorld)
    {
        var emberTex = CreateRadialGlowTexture(64, DestructionCoreColor, DestructionEdgeColor);

        for (int i = 0; i < DestructionEmberCount; i++)
        {
            var ember = new Sprite2D
            {
                Texture = emberTex,
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0.9f),
                ZIndex = 40,
            };
            anchor.AddChild(ember);
            float size = Mathf.Lerp(0.16f, 0.42f, GD.Randf());
            ember.GlobalScale = new Vector2(size, size);
            ember.GlobalPosition = targetWorld + new Vector2(
                (GD.Randf() - 0.5f) * 150f,
                (GD.Randf() - 0.5f) * 130f);

            var rise = ember.GlobalPosition + new Vector2((GD.Randf() - 0.5f) * 90f, -Mathf.Lerp(120f, 260f, GD.Randf()));

            var tw = ember.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(ember, "global_position", rise, Mathf.Lerp(0.42f, 0.70f, GD.Randf()))
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(ember, "modulate:a", 0f, Mathf.Lerp(0.38f, 0.64f, GD.Randf()));
            tw.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(ember))
                {
                    ember.QueueFree();
                }
            }));
        }
    }

    // 抹除期间把目标染成紫色再还原。
    // 这是唯一碰到「不属于我们」的节点的地方：目标很可能当场被打死、节点随即被释放，
    // 所以取节点、还原都要验活；tween 挂在 sprite 自己身上，节点没了 tween 一起没，不会留残影。
    private static void TintCreatureDuringErasure(Creature target)
    {
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(target);
            if (node?.Visuals is not Node2D visuals)
            {
                return;
            }

            var sprite = visuals.GetNodeOrNull<Sprite2D>("StaticModel");
            if (sprite == null)
            {
                return;
            }

            Color original = sprite.Modulate;

            var tw = sprite.CreateTween();
            tw.SetParallel(false);
            tw.TweenProperty(sprite, "modulate", new Color(0.62f, 0.30f, 0.95f, original.A), 0.16);
            tw.TweenInterval(0.10);
            tw.TweenProperty(sprite, "modulate", original, 0.22);
            tw.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(sprite))
                {
                    sprite.Modulate = original;
                }
            }));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Destruction tint failed: {ex}");
        }
    }

    // 程序生成的径向辉光。用 ImageTexture 而不是 GradientTexture2D——
    // 后者是异步生成的，就绪前采样会退化（龟波光束踩过这个坑）。
    internal static ImageTexture CreateRadialGlowTexture(int size, Color inner, Color outer)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d >= 1f)
                {
                    img.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }

                Color c = inner.Lerp(outer, Mathf.Pow(d, 0.85f));
                // 外圈用 3.2 次方快速收掉，再叠一个高斯实心核。
                // 只有幂衰减时加法混合会糊成一大团紫雾，球心也读不出来。
                float core = 0.55f * Mathf.Exp(-((d / 0.17f) * (d / 0.17f)));
                c.A = Mathf.Min(1f, Mathf.Pow(1f - d, 3.2f) + core);
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    private static void SpawnSpiritBombMotes(Node2D anchor, Vector2 gatherWorld)
    {
        for (int i = 0; i < SpiritBombMoteCount; i++)
        {
            float angle = Mathf.Pi * (0.08f + 0.84f * GD.Randf());
            float radius = Mathf.Lerp(SpiritBombMoteMinRadius, SpiritBombMoteMaxRadius, GD.Randf());
            var from = gatherWorld + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            var mote = CreateRadialGlowSprite(0.9f, 0f);
            float size = Mathf.Lerp(0.035f, 0.075f, GD.Randf());
            mote.Scale = new Vector2(size, size);
            mote.Modulate = new Color(1f, 1f, 1f, 0f);
            mote.ZIndex = 29;
            anchor.AddChild(mote);
            mote.GlobalPosition = from;

            float delay = GD.Randf() * (SpiritBombGatherSeconds * 0.55f);
            float travel = Mathf.Lerp(0.42f, 0.62f, GD.Randf());

            var moteTween = mote.CreateTween();
            moteTween.SetParallel(false);
            moteTween.TweenInterval(delay);
            moteTween.TweenProperty(mote, "modulate:a", 1f, 0.10);
            moteTween.Parallel().TweenProperty(mote, "global_position", gatherWorld, travel)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            moteTween.TweenProperty(mote, "modulate:a", 0f, 0.08);
            moteTween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(mote))
                {
                    mote.QueueFree();
                }
            }));
        }
    }

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

            tween.TweenProperty(flash, "scale", new Vector2(13f, 13f), 0.12)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(flash, "modulate:a", 1f, 0.1)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

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
        }
    }

    // 打击反馈统一走原版 NGame —— 它会读玩家的震屏强度偏好（无障碍设置），
    // 自己写 tween 抖相机会无视这个选项。
    private static void PlayImpactFeedback(ShakeStrength strength, ShakeDuration duration, bool hitStop)
    {
        try
        {
            var game = NGame.Instance;
            if (game == null)
            {
                return;
            }

            game.ScreenShake(strength, duration);
            if (hitStop)
            {
                game.DoHitStop(strength, duration);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] impact feedback failed: {ex.Message}");
        }
    }

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

    // Match melee effects to the lunge timeline.
    private const float MeleeStrikeStartSeconds = 0.06f;
    private const float MeleeContactSeconds = 0.115f;
    private const float MeleeLoopSeconds = 0.21f;

    // Each multi-hit segment schedules a wind trail and target impact.
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
                }
            };

            tree.CreateTimer(hitDelay).Timeout += () =>
            {
                try
                {
                    PlayImpactFeedback(ShakeStrength.VeryWeak, ShakeDuration.Short, hitStop: false);
                    foreach (Creature enemy in ResolveKamehamehaImpactTargets(player, cardPlay))
                    {
                        SpawnMeleeImpactFx(enemy);
                    }
                }
                catch
                {
                    // Ignore optional presentation failures.
                }
            };
        }
    }

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

    private static void SpawnPunchWindStreak(Node2D anchor, Sprite2D staticModel, float dir)
    {
        try
        {
            var holder = new Node2D { Name = "KakarotPunchWind", ZIndex = 23 };
            anchor.AddChild(holder);
            var startPos = staticModel.GlobalPosition + new Vector2(54f * dir, -72f);
            holder.GlobalPosition = startPos;

            const float streakTexWidth = 128f;
            var streakTex = CreateStreakTexture();

            float[] yOffsets = { -13f, 0f, 13f };
            float[] alphas = { 0.45f, 0.9f, 0.45f };
            float[] lenScale = { 0.7f, 1f, 0.7f };

            for (var i = 0; i < yOffsets.Length; i++)
            {
                var streak = new Sprite2D
                {
                    Texture = streakTex,
                    Centered = true,
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
        }
    }

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
        }
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

        // Squash and stretch sells the lunge when no attack pose is available.
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
            tween.TweenProperty(staticModel, "position", anticipatePos, 0.06f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(staticModel, "scale", anticipateScale, 0.06f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            tween.TweenProperty(staticModel, "position", strikePos, 0.05f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(staticModel, "scale", strikeScale, 0.05f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

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

    // Action tweens pause idle breathing and restart it from the stored baseline.
    public static void StartIdleBreathing(
        Sprite2D staticModel,
        Vector2 restPos,
        Vector2 restScale,
        float scaleMultiplier = 1.013f,
        float verticalOffset = 2.0f,
        float halfCycleSeconds = 1.5f)
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

            var breatheScale = restScale * scaleMultiplier;
            tween.TweenProperty(staticModel, "position:y", restPos.Y - verticalOffset, halfCycleSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(staticModel, "scale", breatheScale, halfCycleSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(staticModel, "position:y", restPos.Y, halfCycleSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(staticModel, "scale", restScale, halfCycleSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

            IdleTweens[id] = tween;
        }
        catch
        {
        }
    }

    public static void StopIdle(Sprite2D staticModel)
    {
        if (staticModel == null || !GodotObject.IsInstanceValid(staticModel))
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

    public static void StopAllMotion(Sprite2D staticModel, Vector2 restPos, Vector2 restScale)
    {
        if (staticModel == null || !GodotObject.IsInstanceValid(staticModel))
        {
            return;
        }

        StopIdle(staticModel);

        var id = staticModel.GetInstanceId();
        if (ActiveTweens.TryGetValue(id, out var active) && GodotObject.IsInstanceValid(active))
        {
            active.Kill();
        }

        ActiveTweens.Remove(id);

        var crossfade = staticModel.GetNodeOrNull<Sprite2D>("KakarotPoseCrossfade");
        if (crossfade != null && GodotObject.IsInstanceValid(crossfade))
        {
            crossfade.QueueFree();
        }

        staticModel.Position = restPos;
        staticModel.Scale = restScale;
    }

    // Pose swaps are optional and always restore the first retained texture.
    private const float PoseCrossfadeSeconds = 0.1f;

    // Fade a child copy of the old texture to soften form and pose changes.
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
        }
    }

    private static bool IsKakarot(Creature creature)
    {
        var entry = creature.Player?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(entry) && entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }
}
