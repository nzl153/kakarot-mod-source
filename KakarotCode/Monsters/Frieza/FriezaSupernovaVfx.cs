#nullable enable
using System;
using Godot;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace KakarotMod.KakarotCode.Monsters.Frieza;

// 超新星由辉光球、冲击环、碎片和粒子组合生成，以支持脉动、汇聚和爆发等动态表现。
internal static class FriezaSupernovaVfx
{
    // 弗利萨的能量是橙红，核心接近白热。
    private static readonly Color CoreColor = new(1f, 0.86f, 0.52f);
    private static readonly Color EdgeColor = new(1f, 0.30f, 0.05f);
    private static readonly Color DeepColor = new(0.62f, 0.06f, 0.01f);

    private const int ShardCount = 22;
    private const int EmberCount = 90;

    // level 2 使用更大的蓄力球，以区分第二回合蓄力强度。
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
            // 轻微脉动用于表现持续蓄能。
            tween.Chain().TweenProperty(orb, "scale", new Vector2(peak * 0.86f, peak * 0.86f), 0.32)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Chain().TweenProperty(orb, "scale", new Vector2(peak * 1.04f, peak * 1.04f), 0.30)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.Chain().TweenProperty(orb, "modulate:a", 0f, 0.35);
            tween.Chain().TweenCallback(Callable.From(() => KakarotCombatPresentation.FreeIfValid(holder)));

            // 负径向速度配合环形发射面，使粒子持续向中心汇聚。
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

    // 引爆位置使用受击者而不是施法者，确保爆炸视觉与伤害目标一致。
    public static void PlayDetonate(Creature creature)
    {
        Run(() =>
        {
            if (!TryAnchor(creature, out Node container, out Vector2 world))
            {
                return;
            }

            Vector2 center = world;

            // 径向模糊强化两回合蓄力后的爆发反馈。
            NCombatRoom.Instance?.RadialBlur(VfxPosition.Center);

            var holder = new Node2D { Name = "FriezaSupernovaDetonate", ZIndex = 26 };
            container.AddChildSafely(holder);
            holder.GlobalPosition = center;
            holder.GlobalScale = Vector2.One;

            var ball = new Sprite2D
            {
                // 内芯降低白色占比，避免大面积加法混合把橙红色烧成纯白。
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

            // 爆发前短暂收缩，使膨胀阶段有清晰的起点。
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
            // 膨胀阶段同步降低透明度，避免大面积加法混合遮蔽角色。
            tween.Parallel().TweenProperty(ball, "modulate:a", 0.48f, 0.30);

            tween.Chain().TweenProperty(flash, "modulate:a", 0f, 0.26)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(ball, "modulate:a", 0f, 0.32)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.Chain().TweenCallback(Callable.From(
                () => KakarotCombatPresentation.FreeIfValid(holder)));
        });
    }

    // 冲击环纵向压扁，使扩散方向更接近贴地冲击波。
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

    // 放射碎片用于建立爆炸方向，细密部分由粒子补足。
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

    // 大量余烬交由粒子系统生成，避免为每个粒子维护独立节点和 tween。
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
        // CombatVfxContainer 是 Control，因此按 Node 接收；子 Node2D 仍可使用 GlobalPosition。
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

    // 表现层异常不能中断 Boss 回合。
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
