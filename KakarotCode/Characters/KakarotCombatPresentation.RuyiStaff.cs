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

// 如意棒。
//
// 棒长不是写死的，要按场上最远的敌人算跨度——否则单个敌人时棒子会捅出屏幕，
// 三个敌人时又够不着最后一个。
public static partial class KakarotCombatPresentation
{
    private const string RuyiStaffCardId = "KAKAROTMOD-KAKAROT_RUYI_STAFF";

    private const float RuyiStaffThickness = 15f;

    private const float RuyiStaffCapThickness = 21f;

    // 龙珠的如意棒是纯红一根，两端没有金箍（有金箍的是西游记的金箍棒）。
    // 端箍实现保留着，想换回去把这里改 true 即可。
    // 金箍造型做好了但龙珠原作的如意棒是纯红无箍，按原作走。
    // 用 static readonly 而不是 const：const 会被常量折叠，让下面整块变成不可达代码触发 CS0162。
    private static readonly bool RuyiStaffShowCaps = false;

    // 量出存活敌人的横向跨度，让如意棒横跨整排而不是只压住一只。
    private static float ResolveRuyiStaffSpan(Player player, out float centerX)
    {
        const float baseLength = 460f;
        const float maxLength = 980f;
        const float padding = 150f;

        centerX = 0f;
        var combatState = player?.Creature?.CombatState;
        var room = NCombatRoom.Instance;
        if (combatState == null || room == null)
        {
            return baseLength;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (Creature enemy in combatState.HittableEnemies)
        {
            if (enemy is not { CurrentHp: > 0 })
            {
                continue;
            }

            var node = room.GetCreatureNode(enemy);
            if (node == null)
            {
                continue;
            }

            minX = Math.Min(minX, node.GlobalPosition.X);
            maxX = Math.Max(maxX, node.GlobalPosition.X);
        }

        if (minX > maxX)
        {
            return baseLength;
        }

        centerX = (minX + maxX) * 0.5f;
        return Math.Clamp(maxX - minX + padding, baseLength, maxLength);
    }

    // 如意棒：在敌人正前方横空出现一根长棍，高高扬起后劈下，最终砸成水平。
    // 🔴 holder 必须做 GlobalScale 归一：长度/粗细都是世界像素，
    // 而父节点（角色 visuals）带缩放，不归一会被缩成一根牙签。
    private static void TryPlayRuyiStaffVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            Creature target = ResolvePrimaryBeamTarget(player, cardPlay);
            if (target == null || !TryGetCreatureChestWorld(target, out Vector2 chestWorld))
            {
                return;
            }

            // 棍长随敌人阵型走：单只时是基础长度，一群时横跨整排。
            float staffLength = ResolveRuyiStaffSpan(player, out float spanCenterX);

            var holder = new Node2D { Name = "KakarotRuyiStaffFx", ZIndex = 40 };
            anchor.AddChild(holder);
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = new Vector2(spanCenterX, chestWorld.Y - 26f);
            holder.GlobalRotation = Mathf.DegToRad(-78f);

            // 棍身沿局部 X 铺开 —— 旋转 0 度就是水平，正好是"劈到水平"的终点姿态。
            const int shadeRes = 48;
            var shaftTex = CreateCylinderTextureH(
                shadeRes,
                new Color(0.20f, 0.03f, 0.04f),
                new Color(0.80f, 0.12f, 0.11f),
                new Color(1.0f, 0.66f, 0.58f),
                0.36f);

            ImageTexture capTex = RuyiStaffShowCaps
                ? CreateCylinderTextureH(
                    shadeRes,
                    new Color(0.28f, 0.17f, 0.03f),
                    new Color(0.95f, 0.74f, 0.22f),
                    new Color(1.0f, 0.98f, 0.80f),
                    0.36f)
                : null;

            var shaft = new Sprite2D
            {
                Texture = shaftTex,
                Centered = true,
                // 贴图宽 4 高 shadeRes：X 铺长度，Y 铺粗细，不用旋转
                Scale = new Vector2(staffLength / 4f, RuyiStaffThickness / shadeRes),
            };
            holder.AddChild(shaft);

            Sprite2D capLeft = null;
            Sprite2D capRight = null;
            if (RuyiStaffShowCaps)
            {
                Sprite2D MakeCap(float x) => new()
                {
                    Texture = capTex,
                    Centered = true,
                    Scale = new Vector2(14f / 4f, RuyiStaffCapThickness / shadeRes),
                    Position = new Vector2(x, 0f),
                };

                capLeft = MakeCap(-staffLength * 0.5f + 7f);
                holder.AddChild(capLeft);

                capRight = MakeCap(staffLength * 0.5f - 7f);
                holder.AddChild(capRight);
            }

            // 出现时从零长度弹出，避免凭空贴一根棍子上去
            shaft.Scale = new Vector2(0.5f, RuyiStaffThickness / shadeRes);

            var tween = holder.CreateTween();
            tween.SetParallel(false);

            // ① 成形：棍身横向拉满
            tween.TweenProperty(shaft, "scale", new Vector2(staffLength / 4f, RuyiStaffThickness / shadeRes), 0.14)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);

            // ② 扬起蓄力
            tween.TweenProperty(holder, "rotation", Mathf.DegToRad(-96f), 0.10)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);

            // ③ 劈下，砸成水平
            tween.TweenProperty(holder, "rotation", 0f, 0.08)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.In);

            tween.TweenCallback(Callable.From(() =>
            {
                SpawnRuyiSwingArc(anchor, chestWorld, staffLength);
                PlayImpactFeedback(ShakeStrength.Medium, ShakeDuration.Short, hitStop: true);
                SpawnRadialBurst(anchor, chestWorld, 2.4f, 0.30f);
            }));

            // ④ 砸实后的余震，再淡出
            tween.TweenProperty(holder, "rotation", Mathf.DegToRad(4f), 0.06)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tween.TweenInterval(0.08);
            tween.TweenProperty(shaft, "modulate:a", 0f, 0.18);
            if (capLeft != null)
            {
                tween.Parallel().TweenProperty(capLeft, "modulate:a", 0f, 0.18);
            }

            if (capRight != null)
            {
                tween.Parallel().TweenProperty(capRight, "modulate:a", 0f, 0.18);
            }
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
            GD.PrintErr($"[Kakarot][Vfx] Ruyi staff failed: {ex}");
        }
    }

    // 劈下瞬间在落点甩一道弧形残影，读作"挥过去的轨迹"。
    private static void SpawnRuyiSwingArc(Node2D anchor, Vector2 atWorld, float staffLength)
    {
        for (int i = 0; i < 3; i++)
        {
            var ghost = new Sprite2D
            {
                Texture = CreateStreakTexture(),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 0.55f, 0.45f, 0.34f - i * 0.09f),
                Scale = new Vector2(staffLength / 128f * 0.9f, 0.55f),
                Rotation = Mathf.DegToRad(-52f + i * 22f),
                ZIndex = 32,
            };
            anchor.AddChild(ghost);
            ghost.GlobalPosition = atWorld + new Vector2(0f, -staffLength * 0.22f);

            var tw = ghost.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(ghost, "modulate:a", 0f, 0.18 + i * 0.03);
            tw.TweenProperty(ghost, "rotation", Mathf.DegToRad(6f + i * 10f), 0.14);
            tw.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(ghost))
                {
                    ghost.QueueFree();
                }
            }));
        }
    }
}
