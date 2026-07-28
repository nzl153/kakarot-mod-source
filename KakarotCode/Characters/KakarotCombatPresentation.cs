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

// Combat presentation is isolated from synchronized gameplay state.
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

    // Retain original textures so interrupted pose swaps can be restored.
    private static readonly Dictionary<ulong, Texture2D> KamehamehaPoseOriginalTextures = new();
    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalPositions = new();
    private static readonly Dictionary<ulong, Vector2> KamehamehaPoseOriginalScales = new();

    // Cards that use the beam-casting pose.
    private static readonly HashSet<string> KamehamehaPoseCardIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_TENFOLD_KAMEHAMEHA",
        "KAKAROTMOD-KAKAROT_FATHER_SON_WAVE",
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

    // Transformation and charge cards share the aura burst.
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
                    // Beam cards use the optional scene, then fall back to generated light.
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
                    // Keep the attack pose active across the full multi-hit lunge.
                    var attackPoseDuration = 0.14f * Math.Clamp(repeatCount, 1, 5) + 0.12f;
                    TrySwapToPose(player.Creature, staticModel, KakarotFormVisuals.ResolveAttackPosePath(player.Creature), attackPoseDuration);

                    // Spirit Bomb has its own impact sequence.
                    if (!string.Equals(cardEntryId, SpiritBombCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        ScheduleMeleeImpactFx(staticModel, visualsRoot, player, cardPlay, repeatCount);
                    }
                }

                if (string.Equals(cardEntryId, SpiritBombCardId, StringComparison.OrdinalIgnoreCase))
                {
                    PlaySpiritBombVfx(player, cardPlay, visualsRoot, staticModel);
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(cardEntryId) && TransformAuraCardIds.Contains(cardEntryId))
            {
                PlayTransformAura(staticModel);
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
                PlaySfx(visualsRoot, skillSfx.NodeName, skillSfx.Path, -4f, true);
            }
            else if (cardPlay.Card.Type == CardType.Skill)
            {
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

    private const string KamehamehaBeamPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_beam.tscn";

    private const string KamehamehaImpactPackedScenePath = "res://Kakarot/Vfx/kakarot_kamehameha_impact.tscn";

    private const float KamehamehaImpactDelaySeconds = 0.17f;

    private static readonly Vector2 KamehamehaBeamCharacterOffset = new(50f, -29f);

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
        }
    }

    private static Vector2 ComputeKamehamehaBeamOriginWorld(Sprite2D staticModel)
    {
        float dir = staticModel.FlipH ? -1f : 1f;
        return staticModel.GlobalPosition +
            new Vector2(KamehamehaBeamCharacterOffset.X * dir, KamehamehaBeamCharacterOffset.Y);
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

    // Aura bursts use a separate node so form and idle tweens remain untouched.
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
            // Presentation failures must not interrupt combat.
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

            var spawnWorld = impactWorld + new Vector2(0f, -360f);

            var holder = new Node2D { Name = "KakarotSpiritBombFx", ZIndex = 30 };
            anchor.AddChild(holder);
            holder.GlobalPosition = spawnWorld;

            var glow = CreateRadialGlowSprite(0.55f, 0f);
            glow.Scale = new Vector2(0.4f, 0.4f);
            holder.AddChild(glow);

            var core = CreateRadialGlowSprite(0.95f, 0f);
            core.Scale = new Vector2(0.22f, 0.22f);
            holder.AddChild(core);

            var tween = holder.CreateTween();
            tween.SetParallel(false);
            tween.SetPauseMode(Tween.TweenPauseMode.Process);

            tween.TweenProperty(glow, "scale", new Vector2(3.0f, 3.0f), 0.55)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(core, "scale", new Vector2(1.7f, 1.7f), 0.55)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

            tween.TweenProperty(holder, "global_position", impactWorld, 0.22)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

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
