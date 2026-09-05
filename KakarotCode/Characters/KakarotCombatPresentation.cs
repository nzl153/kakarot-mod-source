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

                    if (!string.IsNullOrWhiteSpace(cardEntryId) && ShenronWishDragonCardIds.Contains(cardEntryId))
                    {
                        TryPlayDragonFistVfx(player, cardPlay, visualsRoot, staticModel, ShenronWishDragonTint);
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

            // 「我要药」的 CardType 是 Skill，但它指定敌人、也真的造成伤害，
            // 所以它需要攻击分支那套朝向/姿势处理，光挂个特效会变成「背对着敌人放龙」。
            if (string.Equals(cardEntryId, ShenronWishPotionCardId, StringComparison.OrdinalIgnoreCase))
            {
                if (cardPlay.Target != null)
                {
                    KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
                }
                else
                {
                    KakarotFormVisuals.RefreshFacing(player.Creature);
                }

                TrySwapToPose(player.Creature, staticModel, KakarotFormVisuals.ResolveAttackPosePath(player.Creature), 0.42f);
                TryPlayDragonFistVfx(player, cardPlay, visualsRoot, staticModel, ShenronWishDragonTint);
            }

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
