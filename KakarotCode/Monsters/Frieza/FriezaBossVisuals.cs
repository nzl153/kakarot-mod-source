#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

internal static class FriezaBossVisuals
{
    private const string SaucerTargetNodeName = "KakarotFriezaSaucerTarget";
    private const string BreathingMetaName = "KakarotFriezaBreathing";
    private const string FlashTexturePath = "res://images/vfx/dot.png";
    private const string DeathBeamTexturePath =
        "res://Kakarot/Images/Frieza/death_beam_vfx.png";
    private const string DeathSaucerTexturePath =
        "res://Kakarot/Images/Frieza/death_saucer_vfx.png";

    private static readonly string[] PhaseTextures =
    [
        "res://Kakarot/Images/Frieza/frieza_white.png",
        "res://Kakarot/Images/Frieza/frieza_golden.png",
        "res://Kakarot/Images/Frieza/frieza_black.png",
    ];

    public static void SetPhase(Creature creature, int phase, bool animate = true)
    {
        TryVisual(() => SetPhaseInternal(creature, phase, animate));
    }

    private static void SetPhaseInternal(Creature creature, int phase, bool animate)
    {
        var sprite = NCombatRoom.Instance?
            .GetCreatureNode(creature)?
            .Visuals
            .GetNodeOrNull<Sprite2D>("Visuals/StaticModel");
        if (sprite == null || phase < 1 || phase > PhaseTextures.Length)
        {
            return;
        }

        var texture = ResourceLoader.Load<Texture2D>(PhaseTextures[phase - 1]);
        if (texture == null)
        {
            return;
        }

        if (sprite.Texture?.ResourcePath == texture.ResourcePath)
        {
            return;
        }

        sprite.Texture = texture;
        sprite.Modulate = Colors.White;
        if (!animate)
        {
            return;
        }

        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "modulate", new Color(1.8f, 1.8f, 1.8f, 1f), 0.12);
        tween.TweenProperty(sprite, "modulate", Colors.White, 0.28);
    }

    public static void EnsureBreathing(Creature creature, float amplitude = 3f)
    {
        TryVisual(() => EnsureBreathingInternal(creature, amplitude));
    }

    private static void EnsureBreathingInternal(Creature creature, float amplitude)
    {
        Sprite2D? sprite = GetStaticModel(creature);
        if (sprite == null || sprite.HasMeta(BreathingMetaName))
        {
            return;
        }

        sprite.SetMeta(BreathingMetaName, true);
        Vector2 basePosition = sprite.Position;
        Vector2 baseScale = sprite.Scale;
        Tween tween = sprite.CreateTween().SetLoops();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(
            sprite,
            "position",
            basePosition + new Vector2(0f, -amplitude),
            1.1f);
        tween.Parallel().TweenProperty(sprite, "scale", baseScale * 1.01f, 1.1f);
        tween.TweenProperty(sprite, "position", basePosition, 1.1f);
        tween.Parallel().TweenProperty(sprite, "scale", baseScale, 1.1f);
    }

    public static void PlayAttackMotion(Creature creature, float distance = 22f)
    {
        TryVisual(() => PlayAttackMotionInternal(creature, distance));
    }

    private static void PlayAttackMotionInternal(Creature creature, float distance)
    {
        Node2D? visualRoot = GetVisualRoot(creature);
        Sprite2D? sprite = GetStaticModel(creature);
        if (visualRoot == null || sprite == null)
        {
            return;
        }

        Vector2 basePosition = visualRoot.Position;
        Tween motion = visualRoot.CreateTween();
        motion.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        motion.TweenProperty(
            visualRoot,
            "position",
            basePosition + new Vector2(-distance, -2f),
            0.09f);
        motion.TweenProperty(visualRoot, "position", basePosition, 0.16f)
            .SetEase(Tween.EaseType.InOut);

        Tween flash = sprite.CreateTween();
        flash.TweenProperty(sprite, "modulate", new Color(1.45f, 1.25f, 1.55f, 1f), 0.06f);
        flash.TweenProperty(sprite, "modulate", Colors.White, 0.18f);
    }

    public static void PlayCastMotion(Creature creature, Color color)
    {
        TryVisual(() => PlayCastMotionInternal(creature, color));
    }

    private static void PlayCastMotionInternal(Creature creature, Color color)
    {
        Sprite2D? sprite = GetStaticModel(creature);
        if (sprite == null)
        {
            return;
        }

        Tween tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "modulate", color, 0.12f);
        tween.TweenProperty(sprite, "modulate", Colors.White, 0.28f);
        PlayPulse(creature, color, 0.42f);
    }

    public static void PlaySummonEffect(Creature creature)
    {
        TryVisual(() => PlaySummonEffectInternal(creature, allowDeferred: true));
    }

    private static void PlaySummonEffectInternal(Creature creature, bool allowDeferred)
    {
        Node2D? visualRoot = GetVisualRoot(creature);
        Sprite2D? sprite = GetStaticModel(creature);
        if (visualRoot == null || sprite == null)
        {
            if (allowDeferred)
            {
                Callable.From(() =>
                    TryVisual(() =>
                        PlaySummonEffectInternal(creature, allowDeferred: false))).CallDeferred();
            }
            return;
        }

        Vector2 targetScale = visualRoot.Scale;
        visualRoot.Scale = targetScale * 0.84f;
        sprite.Modulate = new Color(1.4f, 1.15f, 1.6f, 0.15f);
        Tween tween = visualRoot.CreateTween();
        tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visualRoot, "scale", targetScale, 0.32f);
        Tween fade = sprite.CreateTween();
        fade.TweenProperty(sprite, "modulate", Colors.White, 0.28f);
        PlayPulse(creature, new Color(0.75f, 0.35f, 1f, 0.9f), 0.48f);
        EnsureBreathing(creature);
    }

    public static void PlayTransformationFlash(Creature creature, int phase)
    {
        TryVisual(() => PlayTransformationFlashInternal(creature, phase));
    }

    private static void PlayTransformationFlashInternal(Creature creature, int phase)
    {
        Color color = phase == 2
            ? new Color(1.65f, 1.3f, 0.35f, 1f)
            : new Color(0.7f, 0.25f, 1.5f, 1f);
        PlayPulse(creature, color, 0.6f);
        PlayEffect(
            creature,
            "res://Kakarot/Images/Frieza/supernova_charge.png",
            new Vector2(0.22f, 0.22f),
            0.55f);
    }

    public static void PlayEffect(
        Creature creature,
        string texturePath,
        Vector2 scale,
        float duration = 0.55f,
        Vector2? globalOffset = null)
    {
        TryVisual(() => PlayEffectInternal(
            creature,
            texturePath,
            scale,
            duration,
            globalOffset ?? Vector2.Zero));
    }

    public static void PlayFriezaBeam(
        Creature creature,
        IEnumerable<Creature> targets,
        Color tint,
        int bursts = 1,
        float thickness = 0.18f)
    {
        TryVisual(() => PlayFriezaBeamInternal(
            creature,
            targets.Where(static target => target.IsAlive).ToArray(),
            tint,
            bursts,
            thickness));
    }

    private static void PlayFriezaBeamInternal(
        Creature creature,
        Creature[] targets,
        Color tint,
        int bursts,
        float thickness)
    {
        Node? container = NCombatRoom.Instance?.CombatVfxContainer;
        NCreature? sourceNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        Texture2D? beamTexture = ResourceLoader.Load<Texture2D>(DeathBeamTexturePath);
        if (container == null || sourceNode == null || beamTexture == null)
        {
            return;
        }

        Vector2 sourcePosition = sourceNode.VfxSpawnPosition + new Vector2(-48f, -42f);
        int visibleBursts = Math.Clamp(bursts, 1, 3);
        foreach (Creature target in targets)
        {
            NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
            if (targetNode == null)
            {
                continue;
            }

            Vector2 targetPosition = targetNode.VfxSpawnPosition;
            for (int burst = 0; burst < visibleBursts; burst++)
            {
                SpawnFittedBeam(
                    container,
                    beamTexture,
                    sourcePosition,
                    targetPosition,
                    tint,
                    thickness,
                    burst * 0.11f);
            }
        }

        SpawnImpact(container, sourcePosition, tint, 1.15f, 0f);
    }

    private static void SpawnFittedBeam(
        Node container,
        Texture2D texture,
        Vector2 origin,
        Vector2 end,
        Color tint,
        float thickness,
        float delay)
    {
        Vector2 direction = end - origin;
        float distance = direction.Length();
        if (distance < 1f || texture.GetWidth() <= 0)
        {
            return;
        }

        var beam = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            Rotation = direction.Angle() - Mathf.Pi,
            Scale = new Vector2(distance / texture.GetWidth(), thickness * 0.28f),
            Modulate = new Color(tint.R, tint.G, tint.B, 0f),
            ZIndex = 36,
        };
        container.AddChildSafely(beam);
        beam.GlobalPosition = (origin + end) * 0.5f;

        Tween tween = beam.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(beam, "modulate:a", tint.A, 0.035f);
        tween.Parallel().TweenProperty(beam, "scale:y", thickness, 0.055f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(0.15f);
        tween.TweenProperty(beam, "modulate:a", 0f, 0.13f);
        tween.Parallel().TweenProperty(beam, "scale:y", thickness * 0.45f, 0.13f);
        tween.TweenCallback(Callable.From(beam.QueueFreeSafely));

        SpawnImpact(container, end, tint, 1.55f, delay + 0.05f);
    }

    public static void PlayEnergyBolts(
        Creature source,
        IEnumerable<Creature> targets,
        Color primary,
        Color secondary,
        int hits = 1,
        float sizeMultiplier = 1f,
        float arcHeight = 90f)
    {
        TryVisual(() => PlayEnergyBoltsInternal(
            source,
            targets.Where(static target => target.IsAlive).ToArray(),
            primary,
            secondary,
            hits,
            sizeMultiplier,
            arcHeight));
    }

    private static void PlayEnergyBoltsInternal(
        Creature source,
        Creature[] targets,
        Color primary,
        Color secondary,
        int hits,
        float sizeMultiplier,
        float arcHeight)
    {
        Node? container = NCombatRoom.Instance?.CombatVfxContainer;
        NCreature? sourceNode = NCombatRoom.Instance?.GetCreatureNode(source);
        Texture2D? texture = ResourceLoader.Load<Texture2D>(FlashTexturePath);
        if (container == null || sourceNode == null || texture == null)
        {
            return;
        }

        int visibleHits = Math.Clamp(hits, 1, 5);
        foreach (Creature target in targets)
        {
            NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
            if (targetNode == null)
            {
                continue;
            }

            Vector2 rawOrigin = sourceNode.VfxSpawnPosition;
            Vector2 rawEnd = targetNode.VfxSpawnPosition;
            Vector2 direction = (rawEnd - rawOrigin).Normalized();
            Vector2 perpendicular = new(-direction.Y, direction.X);
            Vector2 origin = rawOrigin + direction * 46f + new Vector2(0f, -28f);
            Vector2 end = rawEnd - direction * 16f;
            for (int hitIndex = 0; hitIndex < visibleHits; hitIndex++)
            {
                float lane = (hitIndex - (visibleHits - 1) * 0.5f) * 18f;
                float alternatingArc = hitIndex % 2 == 0 ? -arcHeight : arcHeight * 0.45f;
                Vector2 start = origin + perpendicular * lane;
                Vector2 finish = end + perpendicular * lane * 0.35f;
                Vector2 control = (start + finish) * 0.5f + new Vector2(0f, alternatingArc);
                Color color = hitIndex % 2 == 0 ? primary : secondary;
                SpawnEnergyBolt(
                    container,
                    texture,
                    start,
                    control,
                    finish,
                    color,
                    sizeMultiplier,
                    hitIndex * 0.065f);
            }
        }
    }

    private static void SpawnEnergyBolt(
        Node container,
        Texture2D texture,
        Vector2 origin,
        Vector2 control,
        Vector2 end,
        Color color,
        float sizeMultiplier,
        float delay)
    {
        var bolt = new Node2D
        {
            ZIndex = 36,
        };
        var outer = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(0.045f, 0.045f) * sizeMultiplier,
            Modulate = new Color(color.R, color.G, color.B, color.A * 0.82f),
        };
        var core = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(0.017f, 0.017f) * sizeMultiplier,
            Modulate = new Color(1.5f, 1.5f, 1.5f, 1f),
            ZIndex = 1,
        };
        var trail = new Line2D
        {
            Width = 9f * sizeMultiplier,
            DefaultColor = new Color(color.R, color.G, color.B, color.A * 0.58f),
            Antialiased = true,
            ZIndex = 35,
        };
        trail.AddPoint(Vector2.Zero);
        trail.AddPoint(Vector2.Zero);
        container.AddChildSafely(trail);
        container.AddChildSafely(bolt);
        bolt.GlobalPosition = origin;
        bolt.AddChildSafely(outer);
        bolt.AddChildSafely(core);

        Vector2 previous = origin;
        Tween tween = bolt.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenMethod(
                Callable.From<float>(progress =>
                {
                    Vector2 position = MathHelper.BezierCurve(origin, end, control, progress);
                    bolt.GlobalPosition = position;
                    trail.GlobalPosition = position;
                    trail.SetPointPosition(0, previous - position);
                    trail.SetPointPosition(1, Vector2.Zero);
                    previous = position;
                }),
                0f,
                1f,
                0.24f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
            SpawnImpact(container, end, color, 1.2f * sizeMultiplier, 0f)));
        tween.TweenProperty(bolt, "modulate:a", 0f, 0.055f);
        tween.Parallel().TweenProperty(trail, "modulate:a", 0f, 0.08f);
        tween.TweenCallback(Callable.From(bolt.QueueFreeSafely));
        tween.TweenCallback(Callable.From(trail.QueueFreeSafely));
    }

    public static void PlayDeathSaucerFlight(Creature source, Creature target, bool reflected)
    {
        TryVisual(() => PlayDeathSaucerFlightInternal(source, target, reflected));
    }

    private static void PlayDeathSaucerFlightInternal(
        Creature source,
        Creature target,
        bool reflected)
    {
        Node? container = NCombatRoom.Instance?.CombatVfxContainer;
        NCreature? sourceNode = NCombatRoom.Instance?.GetCreatureNode(source);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        Texture2D? texture = ResourceLoader.Load<Texture2D>(DeathSaucerTexturePath);
        if (container == null || sourceNode == null || targetNode == null || texture == null)
        {
            return;
        }

        Vector2 origin = sourceNode.VfxSpawnPosition + new Vector2(0f, -48f);
        Vector2 end = targetNode.VfxSpawnPosition + new Vector2(0f, -20f);
        float arcDirection = reflected ? 1f : -1f;
        Vector2 control = (origin + end) * 0.5f + new Vector2(0f, 230f * arcDirection);
        var saucer = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(0.11f, 0.11f),
            Modulate = new Color(1.15f, 0.9f, 1.25f, 0f),
            ZIndex = 39,
        };
        container.AddChildSafely(saucer);
        saucer.GlobalPosition = origin;

        Tween tween = saucer.CreateTween();
        tween.SetParallel();
        tween.TweenProperty(saucer, "modulate:a", 1f, 0.08f);
        tween.TweenProperty(saucer, "rotation", reflected ? -Mathf.Tau * 3f : Mathf.Tau * 3f, 0.58f)
            .SetTrans(Tween.TransitionType.Linear);
        tween.TweenProperty(saucer, "scale", new Vector2(0.17f, 0.17f), 0.30f)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenMethod(
                Callable.From<float>(progress =>
                    saucer.GlobalPosition = MathHelper.BezierCurve(origin, end, control, progress)),
                0f,
                1f,
                0.58f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.Chain();
        tween.TweenCallback(Callable.From(() =>
            SpawnImpact(
                container,
                end,
                reflected
                    ? new Color(1.5f, 0.85f, 0.25f, 1f)
                    : new Color(1.25f, 0.18f, 1.4f, 1f),
                2.1f,
                0f)));
        tween.TweenProperty(saucer, "modulate:a", 0f, 0.09f);
        tween.TweenCallback(Callable.From(saucer.QueueFreeSafely));
    }

    public static void PlayTargetPulse(
        IEnumerable<Creature> targets,
        Color color,
        float sizeMultiplier = 1f)
    {
        TryVisual(() => PlayImpactEffectInternal(
            targets.Where(static target => target.IsAlive).ToArray(),
            color,
            sizeMultiplier));
    }

    public static void PlayShockwave(Creature creature, Color color, float sizeMultiplier = 1f)
    {
        TryVisual(() =>
        {
            PlayPulse(creature, color, 0.42f, 0.44f * sizeMultiplier);
            PlayPulse(
                creature,
                new Color(1.45f, 1.45f, 1.45f, 0.75f),
                0.30f,
                0.25f * sizeMultiplier);
        });
    }

    public static void PlayHeavyWindup(Creature creature, bool strongest = false)
    {
        TryVisual(() =>
        {
            PlayAttackMotionInternal(creature, strongest ? 88f : 64f);
            NCombatRoom.Instance?.RadialBlur(VfxPosition.Left);
            NGame.Instance?.ScreenShake(
                strongest ? ShakeStrength.Strong : ShakeStrength.Medium,
                strongest ? ShakeDuration.Normal : ShakeDuration.Short,
                180f);
        });
    }

    public static void PlayHitStop(bool strongest = false)
    {
        TryVisual(() =>
            NGame.Instance?.DoHitStop(
                strongest ? ShakeStrength.Strong : ShakeStrength.Weak,
                strongest ? ShakeDuration.Normal : ShakeDuration.Short));
    }

    private static void SpawnImpact(
        Node container,
        Vector2 position,
        Color color,
        float sizeMultiplier,
        float delay)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(FlashTexturePath);
        if (texture == null)
        {
            return;
        }

        var impact = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(0.035f, 0.035f) * sizeMultiplier,
            Modulate = new Color(color.R, color.G, color.B, 0f),
            ZIndex = 38,
        };
        container.AddChildSafely(impact);
        impact.GlobalPosition = position;

        Tween tween = impact.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(impact, "modulate:a", color.A, 0.025f);
        tween.SetParallel();
        tween.TweenProperty(
            impact,
            "scale",
            new Vector2(0.16f, 0.16f) * sizeMultiplier,
            0.24f);
        tween.TweenProperty(impact, "modulate:a", 0f, 0.24f);
        tween.Chain().TweenCallback(Callable.From(impact.QueueFreeSafely));
    }

    public static void PlayImpactEffect(
        System.Collections.Generic.IEnumerable<Creature> targets,
        Color color,
        float sizeMultiplier = 1f)
    {
        TryVisual(() => PlayImpactEffectInternal(
            targets.Where(static target => target.IsAlive).ToArray(),
            color,
            sizeMultiplier));
    }

    private static void PlayImpactEffectInternal(
        Creature[] targets,
        Color color,
        float sizeMultiplier)
    {
        Node? container = NCombatRoom.Instance?.CombatVfxContainer;
        Texture2D? texture = ResourceLoader.Load<Texture2D>(FlashTexturePath);
        if (container == null || texture == null)
        {
            return;
        }

        foreach (Creature target in targets)
        {
            NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
            if (targetNode == null)
            {
                continue;
            }

            var impact = new Sprite2D
            {
                Texture = texture,
                Scale = new Vector2(0.025f, 0.025f) * sizeMultiplier,
                Modulate = color,
                ZIndex = 38,
            };
            container.AddChildSafely(impact);
            impact.GlobalPosition = targetNode.VfxSpawnPosition;

            Tween tween = impact.CreateTween();
            tween.SetParallel();
            tween.TweenProperty(
                impact,
                "scale",
                new Vector2(0.12f, 0.12f) * sizeMultiplier,
                0.28f);
            tween.TweenProperty(impact, "modulate:a", 0f, 0.28f);
            tween.Chain().TweenCallback(Callable.From(impact.QueueFreeSafely));
        }
    }

    private static void PlayEffectInternal(
        Creature creature,
        string texturePath,
        Vector2 scale,
        float duration,
        Vector2 globalOffset)
    {
        var container = NCombatRoom.Instance?.CombatVfxContainer;
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        var texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (container == null || creatureNode == null || texture == null)
        {
            return;
        }

        var sprite = new Sprite2D
        {
            Texture = texture,
            Scale = scale,
            ZIndex = 20,
        };
        container.AddChildSafely(sprite);
        sprite.GlobalPosition = creatureNode.VfxSpawnPosition + globalOffset;
        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "modulate:a", 0f, duration);
        tween.TweenCallback(Callable.From(sprite.QueueFreeSafely));
    }

    private static void PlayPulse(
        Creature creature,
        Color color,
        float duration,
        float endScale = 0.22f)
    {
        var container = NCombatRoom.Instance?.CombatVfxContainer;
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        Texture2D? texture = ResourceLoader.Load<Texture2D>(FlashTexturePath);
        if (container == null || creatureNode == null || texture == null)
        {
            return;
        }

        var pulse = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(0.06f, 0.06f),
            Modulate = color,
            ZIndex = 18,
        };
        container.AddChildSafely(pulse);
        pulse.GlobalPosition = creatureNode.VfxSpawnPosition;
        Tween tween = pulse.CreateTween();
        tween.SetParallel();
        tween.TweenProperty(pulse, "scale", new Vector2(endScale, endScale), duration);
        tween.TweenProperty(pulse, "modulate:a", 0f, duration);
        tween.Chain().TweenCallback(Callable.From(pulse.QueueFreeSafely));
    }

    private static Node2D? GetVisualRoot(Creature creature)
    {
        return NCombatRoom.Instance?
            .GetCreatureNode(creature)?
            .Visuals
            .GetNodeOrNull<Node2D>("Visuals");
    }

    private static Sprite2D? GetStaticModel(Creature creature)
    {
        return NCombatRoom.Instance?
            .GetCreatureNode(creature)?
            .Visuals
            .GetNodeOrNull<Sprite2D>("Visuals/StaticModel");
    }

    public static void SyncSaucerTarget(FriezaBoss boss)
    {
        TryVisual(() => SyncSaucerTargetInternal(boss));
    }

    private static void SyncSaucerTargetInternal(FriezaBoss boss)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null)
        {
            return;
        }

        Creature? target = boss.Creature.CombatState?.Players
            .FirstOrDefault(player => player.NetId == boss.SaucerTargetNetId && player.Creature.IsAlive)
            ?.Creature;
        target ??= boss.Creature.CombatState?.Players
            .FirstOrDefault(static player => player.Creature.IsAlive)
            ?.Creature;

        NCreature? targetNode = boss.IsSaucerPending && target != null
            ? room.GetCreatureNode(target)
            : null;
        foreach (NCreature creatureNode in room.CreatureNodes.Concat(room.RemovingCreatureNodes).ToList())
        {
            Node? existing = creatureNode.GetNodeOrNull(SaucerTargetNodeName);
            if (existing == null || creatureNode == targetNode)
            {
                continue;
            }

            existing.QueueFreeSafely();
        }

        if (targetNode == null || targetNode.GetNodeOrNull(SaucerTargetNodeName) != null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(
            "res://Kakarot/Images/Frieza/death_saucer_target.png");
        if (texture == null)
        {
            return;
        }

        var marker = new Sprite2D
        {
            Name = SaucerTargetNodeName,
            Texture = texture,
            Scale = new Vector2(0.14f, 0.14f),
            Modulate = new Color(1f, 1f, 1f, 0.9f),
            ZIndex = 40,
        };
        targetNode.AddChildSafely(marker);
        marker.GlobalPosition = targetNode.VfxSpawnPosition + new Vector2(0f, -35f);

        Tween spinTween = marker.CreateTween().SetLoops();
        spinTween.TweenProperty(marker, "rotation", Mathf.Tau, 1.2f)
            .SetTrans(Tween.TransitionType.Linear);
        Tween pulseTween = marker.CreateTween().SetLoops();
        pulseTween.TweenProperty(marker, "modulate:a", 0.55f, 0.45f);
        pulseTween.TweenProperty(marker, "modulate:a", 0.9f, 0.45f);
    }

    public static void ClearSaucerTarget()
    {
        TryVisual(ClearSaucerTargetInternal);
    }

    private static void ClearSaucerTargetInternal()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null)
        {
            return;
        }

        foreach (NCreature creatureNode in room.CreatureNodes.Concat(room.RemovingCreatureNodes).ToList())
        {
            creatureNode.GetNodeOrNull(SaucerTargetNodeName)?.QueueFreeSafely();
        }
    }

    private static void TryVisual(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Presentation must never interrupt a synchronized combat command.
        }
    }
}
