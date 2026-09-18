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

// 破坏。
//
// 紫色是这张牌的身份色，和弗利萨的紫刻意错开（这边偏品红，弗利萨偏冷紫）。
// 抹消染色直接改敌人 modulate，是全场唯一一处「特效碰了敌人节点」的地方。
public static partial class KakarotCombatPresentation
{
    private const string DestructionCardId = "KAKAROTMOD-KAKAROT_DESTRUCTION";

    // 破坏（破壊）：紫色的「抹除」。动作仍是普通攻击，特效落在目标那一侧。
    private const int DestructionMoteCount = 20;

    private const int DestructionEmberCount = 14;

    private const int DestructionShardCount = 16;

    private static readonly Color DestructionCoreColor = new(0.78f, 0.42f, 1f);

    private static readonly Color DestructionEdgeColor = new(0.34f, 0.03f, 0.52f);

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
}
