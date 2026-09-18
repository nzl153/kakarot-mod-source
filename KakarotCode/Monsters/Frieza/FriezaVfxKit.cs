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

    // 气弹齐射：辉光弹头与锥形拖尾沿弧线飞向目标。
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
            if (!TryAnchor(source, out Node container, out Vector2 originWorld))
            {
                return;
            }

            Color core = Lighten(color, 0.70f);
            Vector2 muzzle = originWorld + new Vector2(-46f, -26f);
            int shots = Math.Clamp(hits, 1, 6);

            foreach (Creature target in targets)
            {
                if (target == null || !target.IsAlive || !TryAnchor(target, out _, out Vector2 endWorld))
                {
                    continue;
                }

                for (int i = 0; i < shots; i++)
                {
                    SpawnBolt(container, muzzle, endWorld, core, color, size, arcHeight, i * 0.075f, i);
                }
            }
        });
    }

    private static void SpawnBolt(
        Node container,
        Vector2 from,
        Vector2 to,
        Color core,
        Color edge,
        float size,
        float arcHeight,
        float delay,
        int index)
    {
        var holder = NewHolder(container, from, "FriezaBolt", 33);

        // 拖尾先绘制，弹头覆盖在前方。
        var tail = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateShardTexture(72, 18, core, edge),
            Centered = true,
            Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
            Scale = new Vector2(2.3f * size, 1.25f * size),
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        holder.AddChild(tail);

        var head = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateRadialGlowTexture(64, core, edge),
            Centered = true,
            Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
            // 弹头尺寸需与拖尾保持可辨识比例。
            Scale = new Vector2(1.05f * size, 1.05f * size),
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        holder.AddChild(head);

        // 同批弹道交替弧高，避免多发完全重叠。
        float lift = arcHeight * (index % 2 == 0 ? 1f : -0.55f) * Mathf.Lerp(0.7f, 1.15f, GD.Randf());
        Vector2 mid = (from + to) * 0.5f + new Vector2(0f, -lift);
        float travel = 0.24f;

        // 拖尾方向使用起点到终点的近似方向。
        float angle = (to - from).Angle();
        tail.Rotation = angle;
        tail.Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (34f * size);

        var tween = holder.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(head))
            {
                return;
            }

            head.Modulate = new Color(1f, 1f, 1f, 1f);
            tail.Modulate = new Color(1f, 1f, 1f, 0.9f);
        }));

        // 两段缓动近似弧形轨迹。
        tween.TweenProperty(holder, "global_position", mid, travel * 0.5)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(holder, "global_position", to, travel * 0.5)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(holder))
            {
                return;
            }

            KakarotCombatPresentation.FreeIfValid(tail);
            SpawnBoltImpact(holder, core, edge, size);
        }));
        tween.TweenProperty(head, "scale", new Vector2(2.4f * size, 2.4f * size), 0.10)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(head, "modulate:a", 0f, 0.16);
        tween.TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(holder)));
    }

    private static void SpawnBoltImpact(Node2D holder, Color core, Color edge, float size)
    {
        // 小型命中环限制尺寸，避免覆盖目标主体。
        SpawnRing(holder, core, edge, 1.3f * size, 0.62f, 0f, 0.16f);

        holder.AddChild(KakarotCombatPresentation.CreateBurst(
            KakarotCombatPresentation.GetParticleDotTexture(),
            core, edge,
            amount: 22, lifetime: 0.34f,
            speedMin: 120f, speedMax: 380f,
            scaleMin: 0.5f * size, scaleMax: 1.2f * size,
            baseAngle: 0f, halfSpread: Mathf.Pi,
            gravity: new Vector2(0f, 300f), damping: 0f));
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

            // 加法混合无法压暗背景，因此用暗红辉光与后续白芯的明度反差表现蓄势。
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

            // 径向模糊强化重击反馈。
            NCombatRoom.Instance?.RadialBlur(VfxPosition.Center);

            var tween = holder.CreateTween();
            // 蓄势。
            tween.TweenProperty(dark, "modulate:a", 1f, 0.08);
            tween.Parallel().TweenProperty(dark, "scale", new Vector2(1.5f, 1.5f), 0.12)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            // 爆闪。
            tween.TweenCallback(Callable.From(() =>
            {
                SpawnCross(holder, core, color);
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

    // 十字光条为爆闪提供方向结构。
    private static void SpawnCross(Node2D holder, Color core, Color edge)
    {
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.Pi * 0.5f * i + Mathf.DegToRad(12f);
            var bar = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateShardTexture(96, 14, core, edge),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Rotation = angle,
                Scale = new Vector2(0.4f, 1.6f),
                Modulate = new Color(1f, 1f, 1f, 1f),
                ZIndex = 3,
            };
            holder.AddChild(bar);
            bar.Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 40f;

            var tw = bar.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(bar, "scale", new Vector2(3.4f, 0.55f), 0.22)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(bar, "position",
                bar.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 130f, 0.22)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(bar, "modulate:a", 0f, 0.24)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(bar)));
        }
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

    // 指尖蓄力点在光束射出前提供明确前摇。
    public static void PlayBeamCharge(Node container, Vector2 muzzle, Color color, float seconds)
    {
        Run(() =>
        {
            var holder = NewHolder(container, muzzle, "FriezaBeamCharge", 35);
            Color core = Lighten(color, 0.72f);

            var dot = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(64, core, color),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.12f, 0.12f),
            };
            holder.AddChild(dot);

            var tw = dot.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(dot, "modulate:a", 1f, seconds * 0.6f);
            tw.TweenProperty(dot, "scale", new Vector2(0.62f, 0.62f), seconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            // 射出时收缩并淡出，使蓄力点自然过渡到光束。
            tw.Chain().TweenProperty(dot, "scale", new Vector2(0.2f, 0.2f), 0.05)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            tw.Parallel().TweenProperty(dot, "modulate:a", 0f, 0.06);
            tw.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(dot)));

            holder.AddChild(KakarotCombatPresentation.CreateStream(
                KakarotCombatPresentation.GetParticleDotTexture(),
                core, color,
                amount: 46, lifetime: seconds * 0.8f, duration: seconds,
                radialVelocity: -260f, emissionRadius: 120f,
                scaleMin: 0.3f, scaleMax: 0.8f,
                gravity: Vector2.Zero));

            FreeAfter(holder, seconds + 0.6f);
        });
    }

    // 飞盘路径上生成分段辉光作为拖尾。
    public static void SpawnSaucerTrail(Node container, Func<float, Vector2> pathAt, Color color, float flightSeconds)
    {
        Run(() =>
        {
            const int segments = 30;
            Color core = Lighten(color, 0.6f);
            var tex = KakarotCombatPresentation.CreateRadialGlowTexture(64, core, color);

            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;
                Vector2 at = pathAt(t);
                Vector2 next = pathAt(Mathf.Min(1f, t + 0.02f));
                float angle = (next - at).Angle();
                float width = Mathf.Lerp(0.10f, 0.26f, t);

                var seg = new Sprite2D
                {
                    Texture = tex,
                    Centered = true,
                    Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                    Rotation = angle,
                    Modulate = new Color(1f, 1f, 1f, 0f),
                    ZIndex = 38,
                };
                container.AddChildSafely(seg);
                seg.GlobalPosition = at;
                seg.GlobalScale = new Vector2(width * 2.2f, width);

                var tw = seg.CreateTween();
                tw.TweenInterval(t * flightSeconds);
                tw.TweenProperty(seg, "modulate:a", 0.8f, 0.05);
                tw.TweenInterval(0.08);
                tw.TweenProperty(seg, "modulate:a", 0f, 0.26)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
                tw.TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(seg)));
            }
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
