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

// 弗利萨的招式特效。
//
// 原来这几套全是同一张 res://images/vfx/dot.png 在缩放：
// 震波 = 两个放大的点，气弹 = 拉长的点，变身闪光 = 点 + 一张静态图。
// 这不是参数问题，是「只有一种图元」的问题——再怎么调都还是一个点在变大。
//
// 这里给每一招配它该有的形状：环、锥形碎片、弧光、辉光头、粒子。
// 图元全部复用 KakarotCombatPresentation 里那套程序生成的，
// 配色由调用方按形态传入（白/紫 → 黄金 → 黑/洋红）。
internal static class FriezaVfxKit
{
    // ── 冲击波 ────────────────────────────────────────────────
    // 帝王震波。一圈压扁的薄环贴地推出去 + 放射碎片 + 扬起的尘。
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

            // 两道环错开发出，读作一次推挤而不是一个圆圈在放大。
            SpawnRing(holder, core, color, 3.9f * size, 0.38f, 0f, 0.30f);
            SpawnRing(holder, core, color, 2.6f * size, 0.32f, 0.06f, 0.26f);

            SpawnShards(holder, 14, core, color, 0.9f * size, 220f, 520f, 0.34f, verticalSquash: 0.42f);

            // 贴地扬尘：往外飞、被重力压回去。
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

    // ── 气弹齐射 ──────────────────────────────────────────────
    // 黄金连击 / 黑爆。每发是「辉光弹头 + 锥形拖尾」，沿抛物线飞到目标再炸开。
    // 原来是把一个点拉长成一根线，没有头也没有尾，读不出「打出去了一发」。
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

        // 拖尾在前、弹头在后画，弹头才压得住。
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
            // 0.5 倍的 64px 弹头只有 32px，读成「牙签插着一个点」。
            Scale = new Vector2(1.05f * size, 1.05f * size),
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        holder.AddChild(head);

        // 抛物线：同一批弹道错开高度，否则几发叠成一条线。
        float lift = arcHeight * (index % 2 == 0 ? 1f : -0.55f) * Mathf.Lerp(0.7f, 1.15f, GD.Randf());
        Vector2 mid = (from + to) * 0.5f + new Vector2(0f, -lift);
        float travel = 0.24f;

        // 拖尾指向飞行方向。整体角度用起点到终点近似，足够读。
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

        // 两段直线逼近贝塞尔：够用，而且比自己按帧插值省事。
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
        // 一发小气弹的命中环，2.4 倍会大到盖住半个角色。
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

    // ── 黑闪 ──────────────────────────────────────────────────
    // 第三形态的开场，40 点伤害，全场最重的一拳。
    // 原本挂原版 vfx_giant_horizontal_slash，一张通用横斩贴图，谁用都一样。
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

            // 🔴 一开始想做「先暗一下再炸白」——加法混合根本画不出变暗，
            // 只能加光，渲出来那一段几乎不存在。改成暗红的浓重预兆：
            // 仍然是亮的，但色相压到深红，和随后的白芯拉开明度差，
            // 「暗 → 炸」的读法靠对比得到，不靠真的变暗。
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

            // 屏幕级反应：这一招是第三形态开场，40 伤害，全场最重的一下，
            // 屏幕不动一下撑不住这个分量。
            NCombatRoom.Instance?.RadialBlur(VfxPosition.Center);

            var tween = holder.CreateTween();
            // ① 暗涌 0.10s
            tween.TweenProperty(dark, "modulate:a", 1f, 0.08);
            tween.Parallel().TweenProperty(dark, "scale", new Vector2(1.5f, 1.5f), 0.12)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            // ② 炸白
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

    // 四道十字光条。撑「一瞬间的爆闪」，比单纯放大一个圆点有骨架。
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

    // ── 变身闪光 ──────────────────────────────────────────────
    // 原来是 PlayPulse + supernova_charge.png，一个点加一张静态图。
    // 改成：脚下推开的光环 + 一柱上升的余烬 + 白芯爆闪。
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

            // 从脚下往上冲的能量柱。窄锥 + 负重力，读作「气在往上顶」。
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

    // ── 召唤 ──────────────────────────────────────────────────
    // 小兵出场：一圈向内汇聚的能量收束成形，再撑开一道环。
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

    // ── 常驻暗气 ──────────────────────────────────────────────
    // 黑金形态贴身缠绕的气。这是全场唯一一个「一直在」的特效——
    // 之前所有东西都是放一下就没了，缺的正是这种持续存在感。
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

            // 已经挂着就不要再挂一个——形态刷新会重复调用。
            if (existing != null)
            {
                return;
            }

            var holder = new Node2D { Name = DarkAuraNodeName, ZIndex = -1 };
            visuals.AddChild(holder);
            // 挂在角色 visuals 下必须归一，否则世界尺寸被父级缩放吃掉。
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

    // ── 蓄力前摇 ──────────────────────────────────────────────
    // 指尖先攒一个点再射出去。没有这一下，光线是「凭空出现一道光」，
    // 有了才读成「他抬手、蓄力、发招」。
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
            // 射出瞬间一缩：蓄力被「抽走」变成光束。
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

    // ── 飞盘拖尾 ──────────────────────────────────────────────
    // 飞盘本身旋转和弧线都有了，缺的是「划过一道」。
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

    // ── 图元 ──────────────────────────────────────────────────
    // 🔴 冲击波环要「快、薄、淡」——一闪而过。
    // 第一版是 alpha 0.95、活 0.42 秒、胀到 6.4 倍，渲出来是一个粉色大椭圆
    // 稳稳地横穿全屏在慢慢放大，读作呼啦圈不是冲击波。
    // 环的说服力来自「你没看清它就没了」，不是来自看得清。
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
        // 透明度用 Quad.In：前段掉得快，尾巴不会拖成一个稳定轮廓。
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

    // ── 杂务 ──────────────────────────────────────────────────
    private static Node2D NewHolder(Node container, Vector2 world, string name, int z)
    {
        var holder = new Node2D { Name = name, ZIndex = z };
        container.AddChildSafely(holder);
        holder.GlobalPosition = world;
        // 容器带缩放时不归一，世界像素尺寸会被吃掉，特效缩成一根牙签。
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

    // 战斗特效容器是 Control，只能按 Node 收。
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

    // 表现层出错绝不能把 Boss 的回合带崩。
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
