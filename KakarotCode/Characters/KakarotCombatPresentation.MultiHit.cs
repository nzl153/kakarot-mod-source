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

// 残像拳与狼牙风风拳的命中特效。
//
// 这两张牌都是多段，而且每一段要长得不一样（残像是位移残影，狼牙是兽首扑击），
// 所以各自带一个命中序号计数器，由卡自己调用 Create*HitVfx 逐段取。
public static partial class KakarotCombatPresentation
{
    private const string AfterimageFistCardId = "KAKAROTMOD-KAKAROT_AFTERIMAGE_FIST";

    private static int _afterimageHitIndex;

    // 残像拳段数由 energyX 决定，打牌时算不出总时长。
    // 记住施法者，每段命中把攻击立绘的保持时间往后顶一次。
    private static Creature _afterimageCaster;

    private static Sprite2D _afterimageCasterModel;

    private const string WolfFangHeadTexturePath = "res://Kakarot/Images/Vfx/kakarot_wolf_fang_head.png";

    // 残影观感：很淡、偏冷灰。数值故意压得低，宁可不够也不要糊成一团。
    private static readonly Color WolfFangTint = new(0.78f, 0.80f, 0.86f, 0.42f);

    private const float WolfFangHeadHeight = 260f;

    // 狼牙每段命中的白光强度。调这一个数就行，0 就是完全没有白光。
    private const float WolfFangFlashAlpha = 0.34f;

    // 每段命中位置递进，靠这个计数器实现（纯表现层，不进同步状态）。
    private static int _wolfFangHitIndex;

    // 残像拳：每段命中留一道拳风。段数由 energyX 决定，只能走逐段回调。
    public static Node2D CreateAfterimageHitVfx(Creature enemy)
    {
        try
        {
            int index = _afterimageHitIndex++;

            // 每段命中把攻击姿势续期，打完最后一段后 0.45 秒才切回站姿。
            if (_afterimageCaster != null && GodotObject.IsInstanceValid(_afterimageCasterModel))
            {
                TrySwapToPose(
                    _afterimageCaster,
                    _afterimageCasterModel,
                    KakarotFormVisuals.ResolveAttackPosePath(_afterimageCaster),
                    0.45f);
            }

            var holder = new Node2D { Name = "KakarotAfterimageFx", ZIndex = 31 };

            if (TryGetCreatureChestWorld(enemy, out Vector2 chestWorld))
            {
                holder.TreeEntered += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.GlobalPosition = chestWorld;
                    }
                };
            }

            // 角度与高度逐段交替，读起来像连打而不是同一道风重复播放。
            float angle = Mathf.DegToRad(index % 2 == 0 ? -22f : 18f);
            float offsetY = -30f + (index % 3) * 26f;

            var streak = new Sprite2D
            {
                Texture = CreateStreakTexture(),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(0.92f, 0.96f, 1f, 0.75f),
                Scale = new Vector2(1.5f, 0.9f),
                Position = new Vector2(-40f, offsetY),
                Rotation = angle,
            };
            holder.AddChild(streak);

            var tween = holder.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(streak, "position", new Vector2(46f, offsetY), 0.16)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(streak, "modulate:a", 0f, 0.18);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));

            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Afterimage vfx failed: {ex}");
            return null;
        }
    }

    // 狼牙风风拳每一段命中生成一个狼头残影。
    // 由引擎的 WithHitVfxNode 逐段调用 —— 这是唯一能与真实段数同步的挂点，
    // 在 OnPlay 里播 4 次会因为多段共用一条 AttackCommand 而对不上。
    public static Node2D CreateWolfFangHitVfx(Creature enemy)
    {
        try
        {
            if (!ResourceLoader.Exists(WolfFangHeadTexturePath))
            {
                GD.PrintErr($"[Kakarot][Vfx] wolf head missing: {WolfFangHeadTexturePath}");
                return null;
            }

            var texture = ResourceLoader.Load<Texture2D>(WolfFangHeadTexturePath);
            if (texture == null)
            {
                return null;
            }

            int index = _wolfFangHitIndex++ & 3;

            var holder = new Node2D { Name = "KakarotWolfFangFx", ZIndex = 31 };

            // 引擎会把这个节点挂到自己选的父节点上，不一定是敌人。
            // 不主动定位的话 (0,0) 会落在画布原点——表现就是特效跑到屏幕左上角。
            // 入树后再设全局坐标，此时父链才成立。
            if (TryGetCreatureChestWorld(enemy, out Vector2 enemyChestWorld))
            {
                holder.TreeEntered += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.GlobalPosition = enemyChestWorld;
                    }
                };
            }

            // 原版 vfx_attack_slash 的白爆亮度改不了，4 段 × 全体敌人会叠成一片，
            // 把狼头盖住。改成自己画一层，亮度在这一个数上。
            var flash = new Sprite2D
            {
                Texture = GetKiGlowTexture(),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(0.86f, 0.92f, 1f, WolfFangFlashAlpha),
                Scale = new Vector2(0.55f, 0.55f),
                ZIndex = -1,
            };
            holder.AddChild(flash);
            var flashTween = flash.CreateTween();
            flashTween.SetParallel(true);
            flashTween.TweenProperty(flash, "scale", new Vector2(1.15f, 0.95f), 0.16)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            flashTween.TweenProperty(flash, "modulate:a", 0f, 0.20)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            flashTween.Chain().TweenCallback(Callable.From(() => FreeIfValid(flash)));

            float scale = WolfFangHeadHeight / Math.Max(texture.GetHeight(), 1);
            var sprite = new Sprite2D
            {
                Texture = texture,
                Centered = true,
                Modulate = WolfFangTint,
                Scale = new Vector2(scale * 0.82f, scale * 0.82f),
                // 四段沿斜向递进，读起来像连续扑咬而不是原地闪四下。
                Position = new Vector2(-70f + index * 34f, -46f + index * 24f),
                Rotation = Mathf.DegToRad(-10f + index * 6f),
            };
            holder.AddChild(sprite);

            var tween = holder.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(sprite, "scale", new Vector2(scale * 1.12f, scale * 1.12f), 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sprite, "position", sprite.Position + new Vector2(38f, -8f), 0.20)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sprite, "modulate:a", 0f, 0.24).SetDelay(0.06);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(holder))
                {
                    holder.QueueFree();
                }
            }));

            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Wolf fang vfx failed: {ex}");
            return null;
        }
    }
}
