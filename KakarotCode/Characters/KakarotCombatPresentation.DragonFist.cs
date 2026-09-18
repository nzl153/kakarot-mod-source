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

// 龙拳与神龙许愿共用静态龙贴图和行波着色器；程序化生成难以稳定控制形状且逐帧开销更高。
public static partial class KakarotCombatPresentation
{
    private const string DragonFistDragonTexturePath = "res://Kakarot/Images/Vfx/kakarot_dragon_fist_dragon.png";

    private const string DragonFistBurstCardId = "KAKAROTMOD-KAKAROT_DRAGON_FIST_BURST";

    // 龙头作为锚点，龙身向后延伸。
    private const float DragonFistTargetHeight = 300f;

    private const float DragonFistFlightSeconds = 0.50f;

    // 着色器负责行波扭动、逐段显形和流光。
    private const string DragonWarpShaderPath = "res://Kakarot/Shaders/kakarot_dragon_warp.gdshader";

    // 横向拉伸用于保持长龙轮廓。
    private const float DragonFistStretch = 2.0f;

    // 神龙许愿攻击复用龙拳龙形，通过色调区分。
    private static readonly Color ShenronWishDragonTint = new(0.45f, 1.25f, 0.55f, 1f);

    // 该技能类型卡也造成伤害，因此在 OnCardPlayed 中单独接入龙形演出。
    private const string ShenronWishPotionCardId = "KAKAROTMOD-KAKAROT_SHENRON_WISH_POTION";

    // 仅攻击类愿望卡使用龙形，避免选牌 UI 遮挡技能演出。
    private static readonly HashSet<string> ShenronWishDragonCardIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_SHENRON_WISH_CARD",
        "KAKAROTMOD-KAKAROT_SHENRON_WISH_WIN",
        "KAKAROTMOD-KAKAROT_DRAGON_BALL_WISH_MONEY",
    };

    // 沿飞行路径按时间生成光点作为拖尾。
    private static void SpawnDragonFistTrail(Node2D anchor, Vector2 originWorld, Vector2 impactWorld)
    {
        const int trailCount = 10;
        for (int i = 0; i < trailCount; i++)
        {
            float t = (i + 1f) / (trailCount + 1f);
            Vector2 at = originWorld.Lerp(impactWorld, t) + new Vector2(0f, Mathf.Sin(t * 6.28318f) * 18f);

            var mote = CreateRadialGlowSprite(0.9f, 0f);
            float size = Mathf.Lerp(0.10f, 0.20f, 1f - t);
            mote.Scale = new Vector2(size, size);
            mote.Modulate = new Color(1f, 0.82f, 0.35f, 0f);
            mote.ZIndex = 25;
            anchor.AddChild(mote);
            mote.GlobalPosition = at;

            var tw = mote.CreateTween();
            tw.SetParallel(false);
            tw.TweenInterval(t * DragonFistFlightSeconds);
            tw.TweenProperty(mote, "modulate:a", 0.85f, 0.05);
            tw.TweenProperty(mote, "modulate:a", 0f, 0.30);
            tw.Parallel().TweenProperty(mote, "scale", new Vector2(size * 0.3f, size * 0.3f), 0.30);
            tw.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(mote))
                {
                    mote.QueueFree();
                }
            }));
        }
    }

    private static void TryPlayDragonFistVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel, Color? tint = null)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            if (!ResourceLoader.Exists(DragonFistDragonTexturePath))
            {
                GD.PrintErr($"[Kakarot][Vfx] dragon texture missing: {DragonFistDragonTexturePath}");
                return;
            }

            var texture = ResourceLoader.Load<Texture2D>(DragonFistDragonTexturePath);
            if (texture == null)
            {
                GD.PrintErr("[Kakarot][Vfx] dragon texture failed to load.");
                return;
            }

            Creature target = ResolvePrimaryBeamTarget(player, cardPlay);
            if (cardPlay.Target != null)
            {
                KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
            }

            float dir = staticModel.FlipH ? -1f : 1f;
            Vector2 originWorld = staticModel.GlobalPosition + new Vector2(70f * dir, -70f);
            // 取最远敌人并追加过冲距离，确保拉伸后的龙身能覆盖整排敌人。
            Vector2 impactWorld;
            if (TryResolveFarthestBeamTargetX(player, staticModel, out float farthestX))
            {
                impactWorld = new Vector2(farthestX + KamehamehaBeamOvershoot * dir, originWorld.Y);
            }
            else if (target != null && TryGetCreatureChestWorld(target, out Vector2 chestWorld))
            {
                // 保持出拳高度，避免龙形斜向目标脚部。
                impactWorld = new Vector2(chestWorld.X + KamehamehaBeamOvershoot * dir, originWorld.Y);
            }
            else
            {
                impactWorld = originWorld + new Vector2(880f * dir, 0f);
            }

            // 打击反馈仍定位在真实目标，避免随过冲终点偏离战场。
            Vector2 feedbackWorld = impactWorld;
            if (target != null && TryGetCreatureChestWorld(target, out Vector2 hitChest))
            {
                feedbackWorld = new Vector2(hitChest.X, originWorld.Y);
            }

            var holder = new Node2D { Name = "KakarotDragonFistFx", ZIndex = 26 };
            anchor.AddChild(holder);
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = originWorld;

            float angle = (impactWorld - originWorld).Angle();
            holder.GlobalRotation = angle;

            int texW = Math.Max(texture.GetWidth(), 1);
            int texH = Math.Max(texture.GetHeight(), 1);
            float fullScale = DragonFistTargetHeight / texH;

            var sprite = new Sprite2D
            {
                Texture = texture,
                Centered = true,
                // 锚点落在龙头，龙身向来路方向延伸。
                Offset = new Vector2(-texW * 0.5f, 0f),
                Scale = new Vector2(fullScale * 0.35f * DragonFistStretch, fullScale * 0.35f),
                // 朝左发招时翻转纵向，保持龙形朝向正确。
                FlipV = Mathf.Cos(angle) < 0f,
            };
            if (tint.HasValue)
            {
                sprite.Modulate = tint.Value;
            }
            var warpMaterial = AttachDragonWarp(sprite);
            holder.AddChild(sprite);

            // 出膛闪光用于强化龙形从拳头位置生成的反馈。
            var muzzle = CreateRadialGlowSprite(0.95f, 0f);
            muzzle.Scale = new Vector2(0.25f, 0.25f);
            muzzle.ZIndex = 27;
            anchor.AddChild(muzzle);
            muzzle.GlobalPosition = originWorld;
            var muzzleTween = muzzle.CreateTween();
            muzzleTween.SetParallel(true);
            muzzleTween.TweenProperty(muzzle, "scale", new Vector2(1.15f, 1.15f), 0.20)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            muzzleTween.TweenProperty(muzzle, "modulate:a", 0f, 0.24);
            muzzleTween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(muzzle))
                {
                    muzzle.QueueFree();
                }
            }));

            if (warpMaterial != null)
            {
                // 显形快于飞行，使完整龙形在命中前形成。
                var revealTween = sprite.CreateTween();
                revealTween.TweenProperty(warpMaterial, "shader_parameter/progress", 1f, DragonFistFlightSeconds * 0.75f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.Out);
            }

            var tween = holder.CreateTween();
            tween.SetParallel(false);
            tween.TweenProperty(holder, "global_position", impactWorld, DragonFistFlightSeconds)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(sprite, "scale", new Vector2(fullScale * DragonFistStretch, fullScale), DragonFistFlightSeconds)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

            SpawnDragonFistTrail(anchor, originWorld, impactWorld);

            tween.TweenCallback(Callable.From(() =>
            {
                PlayImpactFeedback(ShakeStrength.Strong, ShakeDuration.Normal, hitStop: true);
                SpawnRadialBurst(anchor, feedbackWorld, 3.4f, 0.38f);
            }));
            tween.TweenProperty(sprite, "modulate:a", 0f, 0.22);
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
            GD.PrintErr($"[Kakarot][Vfx] Dragon fist failed: {ex}");
        }
    }

    // 挂载扭动着色器；加载失败时保留静态贴图，避免表现层异常影响卡牌流程。
    // 返回材质供调用方驱动 progress，null 表示未挂载。
    private static ShaderMaterial AttachDragonWarp(Sprite2D sprite)
    {
        try
        {
            if (!ResourceLoader.Exists(DragonWarpShaderPath))
            {
                GD.PrintErr($"[Kakarot][Vfx] dragon warp shader missing: {DragonWarpShaderPath}");
                return null;
            }

            var shader = ResourceLoader.Load<Shader>(DragonWarpShaderPath);
            if (shader == null)
            {
                return null;
            }

            var noise = new NoiseTexture2D
            {
                Width = 256,
                Height = 256,
                Seamless = true,
                Noise = new FastNoiseLite
                {
                    NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                    Frequency = 0.020f,
                    FractalOctaves = 3,
                },
            };

            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("noise_tex", noise);
            // stretch=1 保持贴图横向映射不变形。
            material.SetShaderParameter("stretch", 1f);
            material.SetShaderParameter("progress", 0f);
            sprite.Material = material;
            return material;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] dragon warp shader failed: {ex}");
            return null;
        }
    }
}
