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

// 龙拳，以及神龙许愿共用的龙形。
//
// ⭐ 这条龙是静态贴图挂行波着色器（kakarot_dragon_warp.gdshader），
// 不是程序化生成的。程序化做 SDF 龙那条路试过，是死路：
// 形状控制不住，而且每帧重算的开销压不下来。
public static partial class KakarotCombatPresentation
{
    private const string DragonFistDragonTexturePath = "res://Kakarot/Images/Vfx/kakarot_dragon_fist_dragon.png";

    private const string DragonFistBurstCardId = "KAKAROTMOD-KAKAROT_DRAGON_FIST_BURST";

    // 龙头贴在画面右端，所以锚点放在龙头上、龙身向后拖。
    private const float DragonFistTargetHeight = 300f;

    private const float DragonFistFlightSeconds = 0.50f;

    // 静态贴图直接平移看着太呆。这个 shader 给它加行波扭动 + 从头到尾显形 + 流光亮带。
    // 建这个文件时是用 `godot --headless --import --path .` 补的 .uid —— 见文件头那条警告。
    private const string DragonWarpShaderPath = "res://Kakarot/Shaders/kakarot_dragon_warp.gdshader";

    // 横向拉长 2 倍：原始比例的龙 S 弯压得太紧，读起来是一团而不是一条长龙。
    // 龙头会跟着被拉扁，但试下来比「头正身短」更对味，所以不做去畸变。
    private const float DragonFistStretch = 2.0f;

    // 神龙许愿的攻击类卡复用龙拳这条龙。金色贴图乘上这个色 = 偏黄绿的神龙气。
    // 想换回纯金把它改成 Colors.White 即可。
    private static readonly Color ShenronWishDragonTint = new(0.45f, 1.25f, 0.55f, 1f);

    // 「我要药」是 Skill 类型但指定敌人且造成伤害，走技能分支单独接，见 OnCardPlayed。
    private const string ShenronWishPotionCardId = "KAKAROTMOD-KAKAROT_SHENRON_WISH_POTION";

    // 只有攻击类进这里；烧牌/抽牌那些技能卡放牌时选牌 UI 会盖住大半屏，做演出是白做。
    private static readonly HashSet<string> ShenronWishDragonCardIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "KAKAROTMOD-KAKAROT_SHENRON_WISH_CARD",
        "KAKAROTMOD-KAKAROT_SHENRON_WISH_WIN",
        "KAKAROTMOD-KAKAROT_DRAGON_BALL_WISH_MONEY",
    };

    // 沿飞行路径按时间撒光点，跟着龙头走，视觉上是拖尾。
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
            // 龙身横向 2 倍之后很长，只飞到最近的敌人会让尾巴还留在悟空身后。
            // 和龟波同一套：取「最远的敌人」再过冲一段，让整条龙铺满战场。
            Vector2 impactWorld;
            if (TryResolveFarthestBeamTargetX(player, staticModel, out float farthestX))
            {
                impactWorld = new Vector2(farthestX + KamehamehaBeamOvershoot * dir, originWorld.Y);
            }
            else if (target != null && TryGetCreatureChestWorld(target, out Vector2 chestWorld))
            {
                // 只取目标的横坐标，高度保持与出拳点齐平 —— 否则龙会斜着扎向敌人脚下。
                impactWorld = new Vector2(chestWorld.X + KamehamehaBeamOvershoot * dir, originWorld.Y);
            }
            else
            {
                impactWorld = originWorld + new Vector2(880f * dir, 0f);
            }

            // 打击反馈仍然落在真正的目标身上，不要跟着过冲跑到所有人右边去。
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
                // 锚点落在龙头，龙身向来路方向拖出去。
                Offset = new Vector2(-texW * 0.5f, 0f),
                Scale = new Vector2(fullScale * 0.35f * DragonFistStretch, fullScale * 0.35f),
                // 朝左发招时整条龙会上下颠倒，翻一次修正。
                FlipV = Mathf.Cos(angle) < 0f,
            };
            if (tint.HasValue)
            {
                sprite.Modulate = tint.Value;
            }
            var warpMaterial = AttachDragonWarp(sprite);
            holder.AddChild(sprite);

            // 出膛闪光：拳头位置炸一下白光，龙才像是"窜出来"的而不是凭空平移
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
                // 显形比飞行快一截，龙先整条窜出来再撞上去。
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

    // 给龙贴图挂上扭动 shader：行波扭动 + 从头到尾显形 + 流光亮带。
    // 加载失败就静默退化成原来的静态平移 —— 特效缺失不该把整张卡的表现打断。
    // 返回材质给调用方 tween progress，null 表示没挂上。
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
            // stretch 传 1 让重映射退化成恒等 —— 定稿选的是「不做龙头去畸变」那档。
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
