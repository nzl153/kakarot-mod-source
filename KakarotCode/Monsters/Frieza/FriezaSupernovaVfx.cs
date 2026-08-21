#nullable enable
using System;
using Godot;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

// 超新星 —— 全程程序生成，不用贴图。
//
// 原来蓄力和引爆各是一张静态 PNG（supernova_charge / supernova_vfx）。
// 静态图做能量球天生动不起来：不会脉动、不会翻滚、不会有东西往里汇聚。
// 这里换成「辉光球 + 环 + 碎片 + 粒子」的组合，全部按参数生成。
//
// 形状复用破坏（Hakai）那一套：球体膨胀 → 塌缩 → 冲击波环 + 放射碎片。
// 破坏是紫色向内抹除，超新星是橙红向外炸开，同一套图元换配色换方向。
internal static class FriezaSupernovaVfx
{
    // 弗利萨的能量是橙红，核心接近白热。
    private static readonly Color CoreColor = new(1f, 0.86f, 0.52f);
    private static readonly Color EdgeColor = new(1f, 0.30f, 0.05f);
    private static readonly Color DeepColor = new(0.62f, 0.06f, 0.01f);

    private const int ShardCount = 22;
    private const int EmberCount = 90;

    // 蓄力：手上托起一颗会脉动的球，四周持续有能量往里汇聚。
    // level 1 是第一回合，2 是第二回合（更大更亮）。
    public static void PlayCharge(Creature creature, int level)
    {
        Run(() =>
        {
            if (!TryAnchor(creature, out Node container, out Vector2 world))
            {
                return;
            }

            Vector2 handWorld = world + new Vector2(-20f, -180f);
            float size = level >= 2 ? 1.35f : 1f;

            var holder = new Node2D { Name = "FriezaSupernovaCharge", ZIndex = 22 };
            container.AddChildSafely(holder);
            holder.GlobalPosition = handWorld;
            holder.GlobalScale = Vector2.One;

            var orb = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(128, CoreColor, EdgeColor),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.15f, 0.15f),
            };
            holder.AddChild(orb);

            float peak = 0.95f * size;
            var tween = orb.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(orb, "modulate:a", 0.92f, 0.35);
            tween.TweenProperty(orb, "scale", new Vector2(peak, peak), 0.55)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            // 脉动：静态图做不到这个，它是「这颗球是活的」的全部来源。
            tween.Chain().TweenProperty(orb, "scale", new Vector2(peak * 0.86f, peak * 0.86f), 0.32)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Chain().TweenProperty(orb, "scale", new Vector2(peak * 1.04f, peak * 1.04f), 0.30)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Chain().TweenProperty(orb, "modulate:a", 0f, 0.35);
            tween.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(holder)));

            // 持续向心汇聚。负的径向速度配环形发射面 —— 手写 tween 做不出这个。
            holder.AddChild(KakarotCombatPresentation.CreateStream(
                KakarotCombatPresentation.GetParticleDotTexture(),
                CoreColor,
                EdgeColor,
                amount: 140,
                lifetime: 0.85f,
                duration: 1.25f,
                radialVelocity: -300f * size,
                emissionRadius: 260f * size,
                scaleMin: 0.5f,
                scaleMax: 1.3f,
                gravity: Vector2.Zero));
        });
    }

    // 引爆：球体撑开 → 一瞬白热 → 冲击波环 + 放射碎片 + 大量余烬。
    //
    // 🔴 传的是「被炸的人」，不是弗利萨。蓄力的球在他手上，
    // 炸开的位置在挨打的人身上——挂错边会读成「他自己炸了自己」。
    public static void PlayDetonate(Creature creature)
    {
        Run(() =>
        {
            if (!TryAnchor(creature, out Node container, out Vector2 world))
            {
                return;
            }

            Vector2 center = world;

            // 屏幕级反应：蓄了两回合的一下，屏幕得跟着动。
            NCombatRoom.Instance?.RadialBlur(VfxPosition.Center);

            var holder = new Node2D { Name = "FriezaSupernovaDetonate", ZIndex = 26 };
            container.AddChildSafely(holder);
            holder.GlobalPosition = center;
            holder.GlobalScale = Vector2.One;

            var ball = new Sprite2D
            {
                // 内芯不用 CoreColor：加法混合下大面积会直接烧成白，火球读不出橙红。
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(
                    128, new Color(1f, 0.70f, 0.30f), DeepColor),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0.95f),
                Scale = new Vector2(0.9f, 0.9f),
                ZIndex = 1,
            };
            holder.AddChild(ball);

            var flash = new Sprite2D
            {
                Texture = KakarotCombatPresentation.CreateRadialGlowTexture(
                    128, new Color(1f, 0.97f, 0.88f), CoreColor),
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.4f, 0.4f),
                ZIndex = 2,
            };
            holder.AddChild(flash);

            var tween = holder.CreateTween();

            // 先憋一下再炸。少了这 0.12 秒，引爆读起来像「突然出现一团火」。
            tween.TweenProperty(ball, "scale", new Vector2(0.62f, 0.62f), 0.12)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            tween.TweenCallback(Callable.From(() =>
            {
                SpawnShockRing(holder);
                SpawnShards(holder);
                SpawnEmbers(holder);
            }));

            tween.Parallel().TweenProperty(flash, "modulate:a", 1f, 0.06);
            tween.Parallel().TweenProperty(flash, "scale", new Vector2(5.0f, 5.0f), 0.26)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(ball, "scale", new Vector2(3.4f, 3.4f), 0.34)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            // 火球边胀边变稀薄。alpha 一路吃满到最后，大面积加法混合就是一团白雾，
            // 把双方角色全糊掉——上午龟波和破坏都栽在这上面。
            tween.Parallel().TweenProperty(ball, "modulate:a", 0.48f, 0.30);

            tween.Chain().TweenProperty(flash, "modulate:a", 0f, 0.26)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(ball, "modulate:a", 0f, 0.32)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.Chain().TweenCallback(Callable.From(
                () => KakarotCombatPresentation.FreeIfValid(holder)));
        });
    }

    // 冲击波环。纵向压扁，读作贴地铺开而不是一个正圆气泡。
    private static void SpawnShockRing(Node2D holder)
    {
        var ring = new Sprite2D
        {
            Texture = KakarotCombatPresentation.CreateRingTexture(
                160, 0.06f, new Color(1f, 0.94f, 0.82f), EdgeColor),
            Centered = true,
            Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
            Modulate = new Color(1f, 1f, 1f, 0.95f),
            ZIndex = 3,
        };
        holder.AddChild(ring);
        ring.Scale = new Vector2(0.4f, 0.4f * 0.46f);

        var tw = ring.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ring, "scale", new Vector2(5.8f, 5.8f * 0.46f), 0.42)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ring, "modulate:a", 0f, 0.42)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(ring)));
    }

    // 放射碎片撑「炸开」的形状；密度交给粒子。
    private static void SpawnShards(Node2D holder)
    {
        var tex = KakarotCombatPresentation.CreateShardTexture(64, 16, CoreColor, DeepColor);

        for (int i = 0; i < ShardCount; i++)
        {
            float angle = Mathf.Tau * (i / (float)ShardCount) + GD.Randf() * 0.26f;
            float speed = Mathf.Lerp(260f, 640f, GD.Randf());
            float size = Mathf.Lerp(0.7f, 1.7f, GD.Randf());

            var shard = new Sprite2D
            {
                Texture = tex,
                Centered = true,
                Material = KakarotCombatPresentation.CreateAdditiveMaterial(),
                Rotation = angle,
                Scale = new Vector2(size * 1.2f, size),
                ZIndex = 3,
            };
            holder.AddChild(shard);

            var to = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.74f) * speed;

            var tw = shard.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(shard, "position", to, 0.40)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(shard, "modulate:a", 0f, 0.40)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(shard)));
        }
    }

    // 90 颗余烬。手写 tween 到不了这个数量级 —— 那是 90 个 Sprite2D 加 90 条 tween。
    private static void SpawnEmbers(Node2D holder)
    {
        holder.AddChild(KakarotCombatPresentation.CreateBurst(
            KakarotCombatPresentation.GetParticleDotTexture(),
            CoreColor,
            DeepColor,
            amount: EmberCount,
            lifetime: 0.85f,
            speedMin: 200f,
            speedMax: 780f,
            scaleMin: 0.7f,
            scaleMax: 2.0f,
            baseAngle: 0f,
            halfSpread: Mathf.Pi,
            gravity: new Vector2(0f, 180f),
            damping: 0f));
    }

    private static bool TryAnchor(Creature creature, out Node container, out Vector2 world)
    {
        // CombatVfxContainer 是 Control 不是 Node2D，只能按 Node 收；
        // 挂进去的 Node2D 自己的 GlobalPosition 照常可用。
        container = null!;
        world = Vector2.Zero;

        var room = NCombatRoom.Instance;
        var vfxContainer = room?.CombatVfxContainer;
        var creatureNode = room?.GetCreatureNode(creature);
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
            GD.PrintErr($"[Kakarot][Frieza] supernova vfx failed: {ex}");
        }
    }
}
