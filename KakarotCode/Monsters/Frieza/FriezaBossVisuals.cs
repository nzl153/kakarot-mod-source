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
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

internal static class FriezaBossVisuals
{
    private const string SaucerTargetNodeName = "KakarotFriezaSaucerTarget";
    private const string BreathingMetaName = "KakarotFriezaBreathing";
    private const string FlashTexturePath = "res://images/vfx/dot.png";

    internal static void PlaySfx(Node anchor, string clip, float volumeDb = -5f)
    {
        // Match combat presentation's spatial player and gain, without changing Goku's private helper.
        string name = $"FriezaSfx_{clip}";
        var player = anchor.GetNodeOrNull<AudioStreamPlayer2D>(name);
        if (player == null)
        {
            string path = $"res://Kakarot/Audio/sfx/frieza/{clip}.wav";
            AudioStream? stream = ResourceLoader.Load<AudioStream>(path);
            if (stream == null)
            {
                GD.PrintErr($"[Kakarot][Frieza] sound missing: {path}");
                return;
            }
            player = new AudioStreamPlayer2D { Name = name, Stream = stream, MaxPolyphony = 6 };
            anchor.AddChild(player);
        }
        player.VolumeDb = volumeDb;
        player.Play();
    }

    private static void PlaySfx(Creature creature, string clip, float volumeDb = -5f)
    {
        Sprite2D? sprite = GetStaticModel(creature);
        if (sprite != null) PlaySfx(sprite, clip, volumeDb);
    }

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
        FriezaSupernovaVfx.EnsureCharge(creature);

        // 黑金形态贴身缠绕的暗气。只有第三形态有，切走就撤掉。
        FriezaVfxKit.EnsureDarkAura(creature, phase >= 3, new Color(0.52f, 0.12f, 0.88f));

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
        string clip = creature.Monster switch
        {
            FriezaGuldo => "guldo_psychic",
            FriezaRecoome => "recoome_charge",
            FriezaCaptainGinyu => "ginyu_command",
            FriezaBurterJeice => "squad_arrival",
            _ => "emperor_command",
        };
        PlaySfx(sprite, clip, -7f);
        if (creature.Monster is FriezaMinionBase)
            FriezaVfxKit.PlaySquadImpact(creature, color, 0.70f, "bind");
        else
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
        FriezaVfxKit.PlaySummonSwirl(creature, new Color(0.75f, 0.35f, 1f));
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
        // 最后一处静态贴图用法也去掉了。
        FriezaVfxKit.PlayTransformFlash(creature, color);
    }

    public static void PlayFriezaBeam(
        Creature creature,
        IEnumerable<Creature> targets,
        Color tint,
        int bursts = 1,
        float thickness = 0.18f,
        bool dark = false)
    {
        TryVisual(() => PlayFriezaBeamInternal(
            creature,
            targets.Where(static target => target.IsAlive).ToArray(),
            tint,
            bursts,
            thickness,
            dark));
    }

    private static void PlayFriezaBeamInternal(
        Creature creature,
        Creature[] targets,
        Color tint,
        int bursts,
        float thickness,
        bool dark)
    {
        Node? container = NCombatRoom.Instance?.CombatVfxContainer;
        Sprite2D? sourceSprite = GetStaticModel(creature);
        if (container == null || sourceSprite == null)
        {
            return;
        }

        float leftmostTargetX = GetMuzzlePosition(sourceSprite).X;
        foreach (Creature target in targets)
        {
            NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
            if (targetNode == null)
            {
                continue;
            }

            leftmostTargetX = Mathf.Min(leftmostTargetX, targetNode.VfxSpawnPosition.X);
        }

        FriezaVfxKit.PlayBeamCharge(container, sourceSprite, tint, 0.045f);
        PlaySfx(sourceSprite, bursts > 1 ? "death_beam_double" : "death_beam");
        SpawnShaderBeam(container, sourceSprite, leftmostTargetX, tint, thickness, bursts, dark);
        NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short, 180f);
    }

    // Targets set only the length; hit flashes remain owned by AttackCommand.
    internal static float GetHorizontalEndX(Sprite2D sourceSprite, float leftmostTargetX)
    {
        float screenLeft = (sourceSprite.GetCanvasTransform().AffineInverse() *
            sourceSprite.GetViewportRect().Position).X;
        return Mathf.Min(screenLeft - 160f, leftmostTargetX - 180f);
    }

    private static void SpawnShaderBeam(
        Node container,
        Sprite2D sourceSprite,
        float leftmostTargetX,
        Color tint,
        float thickness,
        int bursts,
        bool dark)
    {
        Color core = tint.Lerp(Colors.White, 0.62f);
        core.A = 1f;
        float pixels = Mathf.Clamp(thickness * 650f, 86f, 210f);
        float sustain = 0.26f + (Math.Clamp(bursts, 1, 3) - 1) * 0.11f;
        float lifetime = 0.08f + sustain + 0.14f;
        var holder = new Node2D { Name = "FriezaDeathBeam", ZIndex = 24 };
        container.AddChild(holder);
        holder.GlobalPosition = GetMuzzlePosition(sourceSprite);
        holder.GlobalScale = Vector2.One;
        holder.GlobalRotation = Mathf.Pi;

        var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        var canvas = ImageTexture.CreateFromImage(image);
        var layers = new ShaderMaterial[2];
        var sprites = new Sprite2D[2];
        for (int i = 0; i < layers.Length; i++)
        {
            string path = i == 0
                ? "res://Kakarot/Shaders/frieza_death_beam_base.gdshader"
                : "res://Kakarot/Shaders/frieza_death_beam.gdshader";
            var shader = ResourceLoader.Load<Shader>(path);
            if (shader == null)
            {
                GD.PrintErr($"[Kakarot][Frieza] beam shader missing: {path}");
                holder.QueueFree();
                return;
            }
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("beam_color", tint);
            material.SetShaderParameter("core_color", core);
            material.SetShaderParameter("dark", dark);
            material.SetShaderParameter("sustain", sustain);
            layers[i] = material;
            var sprite = new Sprite2D
            {
                Texture = canvas,
                Offset = new Vector2(4f, 0f),
                Material = material,
            };
            holder.AddChild(sprite);
            sprites[i] = sprite;
        }

        void Update(float age)
        {
            // Tween callbacks can outlive the source when combat ends.
            if (!GodotObject.IsInstanceValid(sourceSprite))
            {
                holder.QueueFree();
                return;
            }
            Vector2 origin = GetMuzzlePosition(sourceSprite);
            holder.GlobalPosition = origin;
            float length = origin.X - GetHorizontalEndX(sourceSprite, leftmostTargetX);
            for (int i = 0; i < layers.Length; i++)
            {
                sprites[i].Scale = new Vector2(length / 8f, pixels / 8f);
                layers[i].SetShaderParameter("age", age);
            }
        }

        // Damage has no fixed windup here, so the pilot beam must exist on the first frame.
        Update(0f);
        Tween tween = holder.CreateTween();
        tween.TweenMethod(Callable.From<float>(Update), 0f, lifetime, lifetime);
        tween.TweenCallback(Callable.From(holder.QueueFree));
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
        Creature source, Creature[] targets, Color primary, Color secondary,
        int hits, float sizeMultiplier, float arcHeight)
    {
        Node? container = NCombatRoom.Instance?.CombatVfxContainer;
        Sprite2D? sprite = GetStaticModel(source);
        if (container == null || sprite == null) return;

        float leftmost = GetMuzzlePosition(sprite).X;
        foreach (Creature target in targets)
        {
            NCreature? node = NCombatRoom.Instance?.GetCreatureNode(target);
            if (node != null) leftmost = Mathf.Min(leftmost, node.VfxSpawnPosition.X);
        }
        bool fast = source.Monster is FriezaBurterJeice;
        PlaySfx(sprite, fast ? (hits > 1 ? "burter_comet" : "jeice_crusher") : "recoome_bomber");
        FriezaVfxKit.PlayBeamCharge(container, sprite, primary, 0.025f,
            secondaryHand: fast && primary.B > primary.R);
        for (int i = 0; i < Math.Clamp(hits, 1, 5); i++)
        {
            SpawnEnergyBolt(container, sprite, leftmost, i % 2 == 0 ? primary : secondary,
                sizeMultiplier, i * 0.055f, fast);
        }
    }

    private static void SpawnEnergyBolt(
        Node container, Sprite2D source, float leftmost, Color color,
        float size, float delay, bool fast)
    {
        var holder = new Node2D { Name = "FriezaSquadBolt", ZIndex = 32, Visible = false };
        container.AddChild(holder);
        holder.GlobalScale = Vector2.One;
        Color core = color.Lerp(Colors.White, 0.6f);
        float length = fast ? 190f : 110f;
        for (int layer = 0; layer < 2; layer++)
        {
            bool light = layer == 1;
            var tail = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateShardTexture(72, 18,
                    light ? core : Colors.Black, light ? color : Colors.Black),
                Position = new Vector2(length * 0.5f * size, 0f),
                Scale = new Vector2(length / 72f, fast ? 0.65f : 1.65f) * size * (light ? 1f : 1.3f),
                Material = light ? KakarotCombatPresentation.CreateAdditiveMaterial() : null,
            };
            var head = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(64,
                    light ? core : Colors.Black, light ? color : Colors.Black),
                Scale = new Vector2(fast ? 1.25f : 1.8f, fast ? 0.60f : 1.65f) * size * (light ? 1f : 1.5f),
                Material = light ? KakarotCombatPresentation.CreateAdditiveMaterial() : null,
            };
            holder.AddChild(tail);
            holder.AddChild(head);
        }
        Vector2 origin = Vector2.Zero;
        Vector2 end = Vector2.Zero;
        Tween tween = holder.CreateTween();
        if (delay > 0f) tween.TweenInterval(delay);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(source) || !source.IsInsideTree())
            {
                tween.Kill();
                holder.QueueFree();
                return;
            }
            origin = GetMuzzlePosition(source, fast && color.B > color.R);
            end = new Vector2(GetHorizontalEndX(source, leftmost), origin.Y);
            holder.GlobalPosition = origin;
            holder.Visible = true;
        }));
        tween.TweenMethod(Callable.From<float>(p => holder.GlobalPosition = origin.Lerp(end, p)),
            0f, 1f, fast ? 0.12f : 0.16f);
        tween.TweenCallback(Callable.From(holder.QueueFree));
    }

    public static void PlayDeathSaucerFlight(Creature source, Creature target, bool reflected, bool resolving = false)
    {
        TryVisual(() => PlayDeathSaucerFlightInternal(source, target, reflected, resolving));
    }

    private static void PlayDeathSaucerFlightInternal(
        Creature source, Creature target, bool reflected, bool resolving)
    {
        Node? container = NCombatRoom.Instance?.CombatVfxContainer;
        NCreature? sourceNode = NCombatRoom.Instance?.GetCreatureNode(source);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (container == null || sourceNode == null || targetNode == null) return;
        PlaySfx(source, resolving ? "saucer_cut" : "saucer_spin", -6f);

        Sprite2D? sprite = GetStaticModel(source);
        Vector2 origin = source.Monster is FriezaBoss && sprite != null
            ? GetMuzzlePosition(sprite) : sourceNode.VfxSpawnPosition;
        Vector2 end = targetNode.VfxSpawnPosition + new Vector2(0f, -20f);
        Vector2 control = (origin + end) * 0.5f + new Vector2(0f, reflected ? 170f : -220f);
        Color color = reflected ? new Color(1f, 0.65f, 0.16f) : new Color(0.84f, 0.24f, 1f);
        float travel = resolving ? 0.10f : 0.46f;
        Node2D saucer = FriezaVfxKit.CreateField(container, "frieza_saucer", new Vector2(170f, 65f), out var materials);
        saucer.Name = "FriezaDeathSaucer";
        saucer.ZIndex = 39;
        foreach (var material in materials) material.SetShaderParameter("blade_color", color);
        if (resolving)
        {
            // The resolution damage has no windup; put the cut on the damage frame.
            FriezaVfxKit.PlaySquadImpact(target, color, 1.65f, "cut");
        }
        FriezaVfxKit.SpawnSaucerTrail(container,
            p => MathHelper.BezierCurve(origin, end, control, p), color, travel);
        void Update(float age)
        {
            float p = Mathf.Clamp(age / travel, 0f, 1f);
            saucer.GlobalPosition = MathHelper.BezierCurve(origin, end, control, p);
            Vector2 tangent = (control - origin) * (1f - p) + (end - control) * p;
            // Spin inside the projected disc instead of tumbling the whole oval.
            saucer.Rotation = tangent.Angle() + (end.X < origin.X ? Mathf.Pi : 0f);
            saucer.GlobalScale = new Vector2(1f, 0.88f + 0.12f * Mathf.Sin(age * 48f));
            saucer.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((age - travel) / 0.09f, 0f, 1f));
            foreach (var material in materials) material.SetShaderParameter("age", reflected ? -age : age);
        }
        Update(0f);
        Tween tween = saucer.CreateTween();
        tween.TweenMethod(Callable.From<float>(Update), 0f, travel + 0.09f, travel + 0.09f);
        tween.TweenCallback(Callable.From(saucer.QueueFree));
    }

    public static void PlayTargetPulse(
        IEnumerable<Creature> targets,
        Color color,
        float sizeMultiplier = 1f,
        string style = "bind")
    {
        TryVisual(() =>
        {
            string? clip = style switch
            {
                "heavy" => "recoome_heavy",
                "combo" => "ginyu_combo",
                "psychic" => "guldo_strike",
                _ => null,
            };
            if (clip != null)
            {
                Creature? target = targets.FirstOrDefault(static target => target.IsAlive);
                if (target != null) PlaySfx(target, clip, -6f);
            }
            foreach (Creature target in targets.Where(static target => target.IsAlive))
                FriezaVfxKit.PlaySquadImpact(target, color, sizeMultiplier, style);
        });
    }

    public static void PlayShockwave(Creature creature, Color color, float sizeMultiplier = 1f)
    {
        // 原本是两个放大的 dot.png。一个点再怎么放大也还是一个点。
        FriezaVfxKit.PlayShockwave(creature, color, sizeMultiplier);
        if (sizeMultiplier < 1.5f) TryVisual(() => PlaySfx(creature, "emperor_shockwave"));
    }

    public static void PlayHeavyWindup(Creature creature, bool strongest = false, string? sound = "heavy_impact")
    {
        TryVisual(() =>
        {
            if (sound != null) PlaySfx(creature, sound);
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

    internal static Sprite2D? GetStaticModel(Creature creature)
    {
        return NCombatRoom.Instance?
            .GetCreatureNode(creature)?
            .Visuals
            .GetNodeOrNull<Sprite2D>("Visuals/StaticModel");
    }

    internal static Vector2 GetMuzzlePosition(Sprite2D sprite, bool secondaryHand = false)
    {
        // Art-space hand anchors; ToGlobal includes breathing, attacks and aspect-ratio scaling.
        Vector2 local = sprite.Texture!.ResourcePath switch
        {
            "res://Kakarot/Images/Frieza/frieza_golden.png" => new Vector2(-469f, -342f),
            "res://Kakarot/Images/Frieza/frieza_black.png" => new Vector2(-471f, -365f),
            "res://Kakarot/Images/Frieza/guldo.png" => new Vector2(-386f, -202f),
            "res://Kakarot/Images/Frieza/recoome.png" => new Vector2(-302f, -318f),
            "res://Kakarot/Images/Frieza/captain_ginyu.png" => new Vector2(-377f, -417f),
            "res://Kakarot/Images/Frieza/burter_jeice.png" => new Vector2(-512f, -88.5f),
            _ => new Vector2(-495f, -475f),
        };
        if (secondaryHand && sprite.Texture.ResourcePath == "res://Kakarot/Images/Frieza/burter_jeice.png")
            local = new Vector2(-4f, -240.5f);
        if (sprite.FlipH) local.X = -local.X;
        if (sprite.FlipV) local.Y = -local.Y;
        return sprite.ToGlobal(local);
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

        Texture2D texture = KakarotCombatPresentation.CreateRingTexture(256, 0.045f,
            Colors.White, new Color(0.84f, 0.24f, 1f));

        var marker = new Sprite2D
        {
            Name = SaucerTargetNodeName,
            Texture = texture,
            Scale = new Vector2(0.70f, 0.70f),
            Modulate = new Color(1f, 1f, 1f, 0.9f),
            ZIndex = 40,
        };
        targetNode.AddChildSafely(marker);
        marker.GlobalPosition = targetNode.VfxSpawnPosition + new Vector2(0f, -35f);

        marker.AddChild(new Sprite2D
        {
            Texture = texture,
            Scale = Vector2.One * 1.12f,
            Modulate = new Color(0.06f, 0.015f, 0.08f, 0.9f),
            ZIndex = -1,
        });
        Tween pulse = marker.CreateTween().SetLoops();
        pulse.TweenMethod(Callable.From<float>(age =>
        {
            var power = boss.Creature.GetPower<KakarotMod.KakarotCode.Powers.FriezaDeathSaucerPower>();
            if (power == null || !targetNode.Entity.IsAlive || !boss.Creature.IsAlive)
            {
                marker.Visible = false;
                marker.QueueFree();
                return;
            }
            bool ready = power.Damage >= power.DynamicVars["Threshold"].IntValue;
            Color tint = ready ? new Color(1f, 0.75f, 0.30f) : Colors.White;
            tint.A = 0.75f + 0.15f * Mathf.Cos(age * Mathf.Tau);
            marker.Modulate = tint;
            marker.Rotation = age * Mathf.Tau * (ready ? -0.65f : 0.35f);
        }), 0f, 20f, 20f);
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
        catch (Exception ex)
        {
            // Presentation failures must not interrupt synchronized combat.
            GD.PrintErr($"[Kakarot][Frieza] presentation failed: {ex}");
        }
    }
}
