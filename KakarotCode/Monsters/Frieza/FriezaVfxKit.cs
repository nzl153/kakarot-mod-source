#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

// 弗利萨招式特效。按招式组合环、碎片、弧光、辉光和粒子，配色由形态传入。
internal static class FriezaVfxKit
{
    // 帝王震波：压扁冲击环、放射碎片和贴地尘。
    public static void PlayShockwave(Creature source, Color color, float size)
    {
        Run(() =>
        {
            if (!TryAnchor(source, out Node container, out Vector2 world))
            {
                return;
            }

            var holder = NewHolder(container, world + new Vector2(0f, 26f), "FriezaShockwave", 25);
            Color core = Lighten(color, 0.66f);

            // 两道环错开发射，形成连续推挤。
            SpawnRing(holder, core, color, 3.9f * size, 0.38f, 0f, 0.30f);
            SpawnRing(holder, core, color, 2.6f * size, 0.32f, 0.06f, 0.26f);

            SpawnShards(holder, 14, core, color, 0.9f * size, 220f, 520f, 0.34f, verticalSquash: 0.42f);

            // 贴地粒子向外扩散并受向下重力影响。
            holder.AddChild(KakarotCombatPresentation.CreateBurst(
                KakarotCombatPresentation.GetParticleDotTexture(),
                core, color,
                amount: 70, lifetime: 0.62f,
                speedMin: 160f, speedMax: 540f,
                scaleMin: 0.6f * size, scaleMax: 1.7f * size,
                baseAngle: 0f, halfSpread: Mathf.Pi,
                gravity: new Vector2(0f, 420f), damping: 0f));

            FreeAfter(holder, 1.1f);
        });
    }

    // Keep one horizontal volley regardless of the player count.
    public static void PlayBolts(
        Creature source,
        IEnumerable<Creature> targets,
        Color color,
        int hits,
        float size,
        float arcHeight)
    {
        Run(() =>
        {
            Node? container = NCombatRoom.Instance?.CombatVfxContainer;
            Sprite2D? sourceSprite = FriezaBossVisuals.GetStaticModel(source);
            if (container == null || sourceSprite == null)
            {
                return;
            }

            Color core = Lighten(color, 0.70f);
            int shots = Math.Clamp(hits, 1, 6);
            float leftmostTargetX = FriezaBossVisuals.GetMuzzlePosition(sourceSprite).X;
            foreach (Creature target in targets)
            {
                if (!target.IsAlive || !TryAnchor(target, out _, out Vector2 endWorld))
                {
                    continue;
                }
                leftmostTargetX = Mathf.Min(leftmostTargetX, endWorld.X);
            }
            PlayBeamCharge(container, sourceSprite, color, 0.035f);
            for (int i = 0; i < shots; i++)
            {
                SpawnBolt(container, sourceSprite, leftmostTargetX, core, color, size, i * 0.075f);
            }
        });
    }

    private static void SpawnBolt(
        Node container,
        Sprite2D sourceSprite,
        float leftmostTargetX,
        Color core,
        Color edge,
        float size,
        float delay)
    {
        Vector2 from = FriezaBossVisuals.GetMuzzlePosition(sourceSprite);
        var holder = NewHolder(container, from, "FriezaBolt", 33);
        holder.Visible = false;

        // Ordinary blending preserves the silhouette against bright terrain.
        var tailShade = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateShardTexture(72, 18, Colors.Black, Colors.Black),
            Scale = new Vector2(2.6f * size, 1.8f * size),
            Position = new Vector2(82.8f * size, 0f),
        };
        holder.AddChild(tailShade);
        var headShade = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateRadialGlowTexture(64, Colors.Black, Colors.Black),
            Scale = Vector2.One * 1.65f * size,
        };
        holder.AddChild(headShade);

        // 拖尾先绘制，弹头覆盖在前方。
        var tail = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateShardTexture(72, 18, core, edge),
            Centered = true,
            Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
            Scale = new Vector2(2.3f * size, 1.25f * size),
            Position = new Vector2(82.8f * size, 0f),
            Modulate = new Color(1f, 1f, 1f, 0.9f),
        };
        holder.AddChild(tail);

        var head = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateRadialGlowTexture(64, core, edge),
            Centered = true,
            Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
            // 弹头尺寸需与拖尾保持可辨识比例。
            Scale = new Vector2(1.05f * size, 1.05f * size),
        };
        holder.AddChild(head);

        Vector2 to = from;
        const float travel = 0.16f;

        var tween = holder.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(sourceSprite))
            {
                tween.Kill();
                holder.QueueFree();
                return;
            }

            from = FriezaBossVisuals.GetMuzzlePosition(sourceSprite);
            to = new Vector2(FriezaBossVisuals.GetHorizontalEndX(sourceSprite, leftmostTargetX), from.Y);
            holder.Visible = true;
            FriezaBossVisuals.PlaySfx(sourceSprite, "energy_bolt", -7f);
        }));

        tween.TweenMethod(Callable.From<float>(progress =>
        {
            holder.GlobalPosition = from.Lerp(to, progress);
        }), 0f, 1f, travel).SetTrans(Tween.TransitionType.Linear);
        tween.TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(holder)));
    }

    // 第三形态开场重击。
    public static void PlayBlackFlash(Creature target, Color color)
    {
        Run(() =>
        {
            if (!TryAnchor(target, out Node container, out Vector2 world))
            {
                return;
            }

            var holder = NewHolder(container, world, "FriezaBlackFlash", 34);
            Color core = Lighten(color, 0.80f);

            // Keep the brief darkening behind the creature instead of tinting its silhouette.
            var shade = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(128, Colors.Black, Colors.Black),
                Modulate = new Color(1f, 1f, 1f, 0f),
            };
            NCombatRoom.Instance!.BackCombatVfxContainer.AddChild(shade);
            shade.GlobalPosition = world;
            shade.GlobalScale = new Vector2(2f, 2f);
            var shadeTween = shade.CreateTween();
            shadeTween.TweenProperty(shade, "modulate:a", 0.32f, 0.08);
            shadeTween.TweenInterval(0.04);
            shadeTween.TweenProperty(shade, "modulate:a", 0f, 0.06);
            shadeTween.TweenCallback(Callable.From(shade.QueueFree));

            var dark = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(
                    128, new Color(0.85f, 0.06f, 0.16f), new Color(0.20f, 0f, 0.04f)),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Scale = new Vector2(0.5f, 0.5f),
                Modulate = new Color(1f, 1f, 1f, 0f),
                ZIndex = 0,
            };
            holder.AddChild(dark);

            var burst = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(128, core, color),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Scale = new Vector2(0.2f, 0.2f),
                Modulate = new Color(1f, 1f, 1f, 0f),
                ZIndex = 2,
            };
            holder.AddChild(burst);

            var tween = holder.CreateTween();
            // 蓄势。
            tween.TweenProperty(dark, "modulate:a", 1f, 0.08);
            tween.Parallel().TweenProperty(dark, "scale", new Vector2(1.5f, 1.5f), 0.12)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            // 爆闪。
            tween.TweenCallback(Callable.From(() =>
            {
                SpawnRing(holder, core, color, 3.4f, 0.85f, 0f, 0.24f);
                SpawnRing(holder, core, color, 2.5f, 0.60f, 0.035f, 0.26f);
                SpawnShards(holder, 20, core, color, 1.35f, 300f, 720f, 0.36f, verticalSquash: 0.85f);
                holder.AddChild(KakarotCombatPresentation.CreateBurst(
                    KakarotCombatPresentation.GetParticleDotTexture(),
                    core, color,
                    amount: 80, lifetime: 0.7f,
                    speedMin: 220f, speedMax: 760f,
                    scaleMin: 0.7f, scaleMax: 2.0f,
                    baseAngle: 0f, halfSpread: Mathf.Pi,
                    gravity: new Vector2(0f, 260f), damping: 0f));
            }));
            tween.TweenProperty(burst, "modulate:a", 1f, 0.05);
            tween.Parallel().TweenProperty(burst, "scale", new Vector2(3.2f, 3.2f), 0.22)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(dark, "modulate:a", 0f, 0.18);
            tween.TweenProperty(burst, "modulate:a", 0f, 0.18)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(holder)));

            FreeAfter(holder, 1.4f);
        });
    }

    // 变身闪光：脚下光环、上升粒子和白芯爆闪。
    public static void PlayTransformFlash(Creature creature, Color color)
    {
        Run(() =>
        {
            if (!TryAnchor(creature, out Node container, out Vector2 world))
            {
                return;
            }

            var holder = NewHolder(container, world, "FriezaTransformFlash", 24);
            Color core = Lighten(color, 0.74f);

            SpawnRing(holder, core, color, 3.0f, 0.34f, 0f, 0.32f);
            SpawnRing(holder, core, color, 2.0f, 0.30f, 0.10f, 0.28f);

            var flash = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(128, core, color),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Scale = new Vector2(0.3f, 0.3f),
                Modulate = new Color(1f, 1f, 1f, 0f),
                ZIndex = 2,
            };
            holder.AddChild(flash);

            var tween = flash.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(flash, "modulate:a", 0.78f, 0.10);
            tween.TweenProperty(flash, "scale", new Vector2(2.2f, 2.8f), 0.30)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.Chain().TweenProperty(flash, "modulate:a", 0f, 0.34)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(flash)));

            // 窄锥向上发射并叠加负重力，形成上升能量柱。
            holder.AddChild(KakarotCombatPresentation.CreateBurst(
                KakarotCombatPresentation.GetParticleDotTexture(),
                core, color,
                amount: 110, lifetime: 0.95f,
                speedMin: 240f, speedMax: 620f,
                scaleMin: 0.6f, scaleMax: 1.9f,
                baseAngle: -Mathf.Pi * 0.5f, halfSpread: Mathf.DegToRad(20f),
                gravity: new Vector2(0f, -140f), damping: 0f));

            FreeAfter(holder, 1.6f);
        });
    }

    // 小兵出场：向心粒子收束后扩散一道环。
    public static void PlaySummonSwirl(Creature creature, Color color)
    {
        Run(() =>
        {
            if (!TryAnchor(creature, out Node container, out Vector2 world))
            {
                return;
            }

            var holder = NewHolder(container, world, "FriezaSummonSwirl", 23);
            Color core = Lighten(color, 0.68f);

            holder.AddChild(KakarotCombatPresentation.CreateStream(
                KakarotCombatPresentation.GetParticleDotTexture(),
                core, color,
                amount: 90, lifetime: 0.55f, duration: 0.55f,
                radialVelocity: -420f, emissionRadius: 200f,
                scaleMin: 0.5f, scaleMax: 1.4f,
                gravity: Vector2.Zero));

            SpawnRing(holder, core, color, 2.0f, 0.40f, 0.42f, 0.26f);
            FreeAfter(holder, 1.5f);
        });
    }

    // 黑金形态常驻气场。
    private const string DarkAuraNodeName = "FriezaDarkAura";

    public static void EnsureDarkAura(Creature creature, bool on, Color color)
    {
        Run(() =>
        {
            NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
            if (creatureNode?.Visuals is not Node2D visuals)
            {
                return;
            }

            Node2D? existing = visuals.GetNodeOrNull<Node2D>(DarkAuraNodeName);

            if (!on)
            {
                KakarotCombatPresentation.FreeIfValid(existing);
                return;
            }

            // 避免形态刷新时重复创建。
            if (existing != null)
            {
                return;
            }

            var holder = new Node2D { Name = DarkAuraNodeName, ZIndex = -1 };
            visuals.AddChild(holder);
            // 挂在角色 visuals 下时归一世界缩放，保持特效像素尺寸。
            holder.GlobalScale = Vector2.One;

            var sprite = visuals.GetNodeOrNull<Sprite2D>("StaticModel");
            if (sprite?.Texture != null)
            {
                Rect2 rect = sprite.GetRect();
                holder.GlobalPosition = sprite.ToGlobal(rect.GetCenter());
            }
            else
            {
                holder.GlobalPosition = visuals.GlobalPosition;
            }

            holder.AddChild(KakarotCombatPresentation.CreateAmbientStream(
                KakarotCombatPresentation.GetParticleDotTexture(),
                Lighten(color, 0.42f),
                color,
                amount: 34,
                lifetime: 1.5f,
                areaExtents: new Vector2(58f, 96f),
                drift: new Vector2(0f, -26f),
                scaleMin: 0.35f,
                scaleMax: 1.1f));
        });
    }

    public static void PlayBeamCharge(Node container, Sprite2D sourceSprite, Color color, float seconds,
        bool secondaryHand = false)
    {
        Run(() =>
        {
            var holder = NewHolder(container, FriezaBossVisuals.GetMuzzlePosition(sourceSprite, secondaryHand), "FriezaBeamCharge", 35);
            Color core = Lighten(color, 0.72f);

            var shade = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(128, Colors.Black, Colors.Black),
                Scale = Vector2.One * 0.8f,
            };
            holder.AddChild(shade);

            var dot = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(64, core, color),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Scale = Vector2.One * 0.28f,
            };
            holder.AddChild(dot);

            var pressure = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRingTexture(160, 0.035f, core, color),
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Scale = new Vector2(0.14f, 0.7f),
            };
            holder.AddChild(pressure);
            var ringTween = pressure.CreateTween().SetParallel(true);
            ringTween.TweenProperty(pressure, "scale", new Vector2(0.45f, 1.9f), 0.16f)
                .SetDelay(seconds).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            ringTween.TweenProperty(pressure, "modulate:a", 0f, 0.16f).SetDelay(seconds);

            var tw = dot.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(dot, "scale", Vector2.One * 0.85f, seconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.Chain().TweenProperty(dot, "scale", new Vector2(2.4f, 1.3f), 0.035f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.Chain().TweenProperty(dot, "scale", Vector2.One * 0.35f, 0.16f);
            tw.Parallel().TweenProperty(dot, "modulate:a", 0f, 0.16f);
            tw.Parallel().TweenProperty(shade, "modulate:a", 0f, 0.16f);

            // Both the charge and beam follow the same moving fingertip.
            var follow = holder.CreateTween();
            follow.TweenMethod(Callable.From<float>(_ =>
            {
                if (!GodotObject.IsInstanceValid(sourceSprite))
                {
                    holder.QueueFree();
                    return;
                }
                holder.GlobalPosition = FriezaBossVisuals.GetMuzzlePosition(sourceSprite, secondaryHand);
            }), 0f, 1f, seconds + 0.195f);
            follow.TweenCallback(Callable.From(holder.QueueFree));
        });
    }

    internal static Node2D CreateField(Node parent, string shaderName, Vector2 size, out ShaderMaterial[] materials)
    {
        var holder = new Node2D();
        parent.AddChild(holder);
        holder.GlobalScale = Vector2.One;
        var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        var texture = ImageTexture.CreateFromImage(image);
        materials = new ShaderMaterial[2];
        for (int i = 0; i < 2; i++)
        {
            string path = $"res://Kakarot/Shaders/{shaderName}{(i == 0 ? "_base" : "")}.gdshader";
            Shader? shader = ResourceLoader.Load<Shader>(path);
            if (shader == null)
            {
                holder.QueueFree();
                throw new InvalidOperationException($"Missing Frieza shader: {path}");
            }
            var material = new ShaderMaterial { Shader = shader };
            materials[i] = material;
            holder.AddChild(new Sprite2D { Texture = texture, Scale = size / 8f, Material = material });
        }
        return holder;
    }

    public static void PlaySquadImpact(Creature target, Color color, float size, string style)
    {
        Run(() =>
        {
            if (!TryAnchor(target, out Node container, out Vector2 world)) return;
            var holder = NewHolder(container, world, "FriezaSquadImpact", 35);
            Color core = Lighten(color, 0.7f);
            bool cut = style == "cut";
            int strokes = cut ? 1 : style == "combo" ? 2 : style == "heavy" ? 7 : 4;
            for (int i = 0; i < strokes; i++)
            {
                Vector2[] points;
                if (cut)
                {
                    points = new Vector2[17];
                    for (int j = 0; j < points.Length; j++)
                    {
                        float x = Mathf.Lerp(-82f, 82f, j / 16f);
                        points[j] = new Vector2(x, -22f * (1f - x * x / (82f * 82f)));
                    }
                }
                else if (style == "combo")
                {
                    points = new Vector2[25];
                    Vector2 offset = i == 0 ? new(-32f, -20f) : new(34f, 24f);
                    for (int j = 0; j < points.Length; j++)
                    {
                        float angle = Mathf.Tau * j / 24f;
                        points[j] = offset + new Vector2(Mathf.Cos(angle) * 26f, Mathf.Sin(angle) * 18f);
                    }
                }
                else if (style == "heavy")
                {
                    Vector2 dir = Vector2.FromAngle(Mathf.Tau * i / strokes);
                    points = [dir * 16f, dir * 70f, dir * 90f];
                }
                else
                {
                    float a = Mathf.Pi * 0.5f * i;
                    points = new Vector2[9];
                    for (int j = 0; j < points.Length; j++)
                    {
                        float angle = a + Mathf.Lerp(0.20f, 1.37f, j / 8f);
                        points[j] = new Vector2(Mathf.Cos(angle) * 70f, Mathf.Sin(angle) * 60f);
                    }
                }
                for (int layer = 0; layer < 2; layer++)
                {
                    holder.AddChild(new Line2D
                    {
                        Points = points,
                        Width = layer == 0 ? (cut ? 16f : 12f) : (cut ? 4f : 3f),
                        DefaultColor = layer == 0 ? new Color(0.035f, 0.018f, 0.025f, 0.9f) : core,
                        Material = layer == 1 ? KakarotCombatPresentation.CreateAdditiveMaterial() : null,
                        Antialiased = true,
                    });
                }
            }
            if (style == "psychic") holder.Rotation = Mathf.Pi * 0.25f;
            holder.GlobalScale = Vector2.One * size;
            var tween = holder.CreateTween();
            bool bind = style is "bind" or "psychic";
            tween.TweenProperty(holder, "global_scale", Vector2.One * size * (bind ? 0.68f : 1.3f), 0.085f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(holder, "global_scale", Vector2.One * size * (bind ? 1.02f : 1.5f), 0.22f);
            tween.Parallel().TweenProperty(holder, "modulate:a", 0f, 0.22f);
            tween.TweenCallback(Callable.From(holder.QueueFree));
        });
    }

    public static void SpawnSaucerTrail(Node container, Func<float, Vector2> pathAt, Color color, float flightSeconds)
    {
        Run(() =>
        {
            var holder = NewHolder(container, Vector2.Zero, "FriezaSaucerWake", 38);
            var lines = new Line2D[2];
            for (int i = 0; i < 2; i++)
            {
                lines[i] = new Line2D
                {
                    Width = i == 0 ? 13f : 3.5f,
                    DefaultColor = i == 0 ? new Color(0.055f, 0.015f, 0.065f, 0.8f) : color,
                    Material = i == 1 ? KakarotCombatPresentation.CreateAdditiveMaterial() : null,
                    Antialiased = true,
                };
                holder.AddChild(lines[i]);
            }
            var tween = holder.CreateTween();
            tween.TweenMethod(Callable.From<float>(age =>
            {
                float head = Mathf.Min(1f, age / flightSeconds);
                float tail = Mathf.Max(0f, (age - 0.10f) / flightSeconds);
                var points = new Vector2[16];
                for (int i = 0; i < points.Length; i++)
                    points[i] = pathAt(Mathf.Lerp(tail, head, i / 15f));
                foreach (var line in lines) line.Points = points;
            }), 0f, flightSeconds + 0.10f, flightSeconds + 0.10f);
            tween.TweenCallback(Callable.From(holder.QueueFree));
        });
    }

    // 冲击环保持短寿命、低透明度和薄轮廓，避免形成稳定的大圆环。
    private static void SpawnRing(
        Node2D holder, Color core, Color edge, float peak, float squash, float delay, float life)
    {
        var ring = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateRingTexture(160, 0.042f, core, edge),
            Centered = true,
            Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
            Modulate = new Color(1f, 1f, 1f, 0.68f),
            Scale = new Vector2(peak * 0.16f, peak * 0.16f * squash),
            ZIndex = 1,
        };
        holder.AddChild(ring);

        float fast = life * 0.68f;
        var tw = ring.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ring, "scale", new Vector2(peak, peak * squash), fast)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out).SetDelay(delay);
        // 前段快速降低透明度，避免尾段残留稳定轮廓。
        tw.TweenProperty(ring, "modulate:a", 0f, fast)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In).SetDelay(delay);
        tw.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(ring)));
    }

    private static void SpawnShards(
        Node2D holder, int count, Color core, Color edge, float size,
        float speedMin, float speedMax, float life, float verticalSquash)
    {
        var tex = KakarotCombatPresentation.CreateShardTexture(64, 16, core, edge);

        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Tau * (i / (float)count) + GD.Randf() * 0.3f;
            float speed = Mathf.Lerp(speedMin, speedMax, GD.Randf());
            float scale = size * Mathf.Lerp(0.65f, 1.4f, GD.Randf());

            var shard = new Sprite2D
            {
                Texture = tex,
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Rotation = angle,
                Scale = new Vector2(scale * 1.15f, scale * 0.9f),
                ZIndex = 2,
            };
            holder.AddChild(shard);

            var to = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * verticalSquash) * speed;

            var tw = shard.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(shard, "position", to, life)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(shard, "modulate:a", 0f, life)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(shard)));
        }
    }

    private static Node2D NewHolder(Node container, Vector2 world, string name, int z)
    {
        var holder = new Node2D { Name = name, ZIndex = z };
        container.AddChildSafely(holder);
        holder.GlobalPosition = world;
        // 归一容器缩放，保持世界像素尺寸。
        holder.GlobalScale = Vector2.One;
        return holder;
    }

    private static void FreeAfter(Node2D holder, float seconds)
    {
        var tree = holder.GetTree();
        if (tree == null)
        {
            return;
        }

        tree.CreateTimer(seconds).Timeout += () => KakarotCombatPresentation.FreeIfValid(holder);
    }

    private static Color Lighten(Color c, float t)
    {
        Color r = c.Lerp(new Color(1f, 1f, 1f), t);
        r.A = 1f;
        return r;
    }

    // 战斗特效容器是 Control，因此按 Node 接收。
    private static bool TryAnchor(Creature creature, out Node container, out Vector2 world)
    {
        container = null!;
        world = Vector2.Zero;

        NCombatRoom? room = NCombatRoom.Instance;
        Node? vfxContainer = room?.CombatVfxContainer;
        NCreature? creatureNode = room?.GetCreatureNode(creature);
        if (vfxContainer == null || creatureNode == null)
        {
            return false;
        }

        container = vfxContainer;
        world = creatureNode.VfxSpawnPosition;
        return true;
    }

    // 表现层异常不应中断 Boss 回合。
    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Frieza] vfx failed: {ex}");
        }
    }
}
