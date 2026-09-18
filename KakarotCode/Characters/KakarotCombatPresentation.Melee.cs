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

// 近战：冲击反馈、拳风、连击调度，以及太阳拳。
//
// 连击的节奏常量（起手 0.06 / 接触 0.115 / 循环 0.21 秒）是对着实机逐帧调出来的，
// 改任何一个都会让多段拳的音画错位。
public static partial class KakarotCombatPresentation
{
    private const string SolarFistCardId = "KAKAROTMOD-KAKAROT_SOLAR_FIST";

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

    // 打击反馈统一走原版 NGame —— 它会读玩家的震屏强度偏好（无障碍设置），
    // 自己写 tween 抖相机会无视这个选项。
    private static void PlayImpactFeedback(ShakeStrength strength, ShakeDuration duration, bool hitStop)
    {
        try
        {
            var game = NGame.Instance;
            if (game == null)
            {
                return;
            }

            game.ScreenShake(strength, duration);
            if (hitStop)
            {
                game.DoHitStop(strength, duration);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] impact feedback failed: {ex.Message}");
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
                    PlayImpactFeedback(ShakeStrength.VeryWeak, ShakeDuration.Short, hitStop: false);
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
}
