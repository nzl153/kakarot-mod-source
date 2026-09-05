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

// 通用能量投射物：光束引擎 + 气弹弹道。
//
// SpawnEnergyBeam 不是龟波专用的——弗利萨的死亡光线走的也是这套（FriezaBossVisuals）。
// 所以它和调用方分开放：谁想要一条会长、会停、会淡的光束，配好颜色和着色器路径就行。
public static partial class KakarotCombatPresentation
{
    // 暗色变体：blend_mix，能真正遮住背景。加法混合画不出黑。
    private const string DarkBeamShaderPath = "res://Kakarot/Shaders/kakarot_energy_beam_dark.gdshader";

    // 龙拳爆发：金龙从施法者拳头窜出，龙头咬向目标。
    // ── 气功弹 ────────────────────────────────────────────────
    // 悟空手上射出去的一颗气弹，飞到敌人身上。
    // 结构和龟波的手心气团同一套（软晕 + 边轮廓 + 白芯），保证两个招式看起来是同一个角色的气。
    //
    // 由卡里主动调用而不是走卡牌派发表：这张牌一次结算可能打两发（上一张是技能时），
    // 派发表只在出牌时触发一次，喂不出第二发。
    internal const float KiBlastFlightSeconds = 0.18f;

    // 弹丸直径（像素）。第一版做成 96 贴图缩到 0.4 约 38px，是个小点，完全没有体积感。
    private const float KiBlastDiameter = 132f;

    // 回响那发（技能触发的第二发）的放大倍数。
    private const float KiBlastEchoScale = 1.32f;

    public static void PlayKiBlastProjectile(Player player, CardPlay cardPlay, bool echo)
    {
        try
        {
            if (player?.Creature == null)
            {
                return;
            }

            var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
            if (creatureNode?.Visuals is not Node2D anchor)
            {
                return;
            }

            var staticModel = anchor.GetNodeOrNull<Sprite2D>("StaticModel");
            if (staticModel == null)
            {
                return;
            }

            float dir = staticModel.FlipH ? -1f : 1f;

            // 起点和龟波共用同一套「按形态查手心位置」的计算。
            // 以前这里写死 (74, -66)，变身之后手心位置变了就对不上。
            Vector2 originWorld = ComputeKamehamehaBeamOriginWorld(staticModel, player.Creature);

            Vector2 impactWorld = ResolveKiBlastImpactWorld(cardPlay.Target, originWorld, dir);

            Color ki = KiDefaultColor;
            Color core = ki.Lerp(new Color(1f, 1f, 1f), echo ? 0.80f : 0.62f);
            float dia = KiBlastDiameter * (echo ? KiBlastEchoScale : 1f);

            var holder = new Node2D { Name = echo ? "KakarotKiBlastEchoShot" : "KakarotKiBlastShot", ZIndex = 26 };
            anchor.AddChild(holder);
            // anchor 自带缩放，不归一的话弹丸会被角色缩放二次放大。
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = originWorld;
            holder.GlobalRotation = (impactWorld - originWorld).Angle();

            var fadedKi = new Color(ki.R, ki.G, ki.B, 0f);

            // 外晕
            var halo = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(128, ki, fadedKi, 0f, 1.60f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0.85f),
                Scale = new Vector2(dia * 1.55f / 128f, dia * 1.40f / 128f),
                ZIndex = -1,
            };
            holder.AddChild(halo);

            // 边轮廓：让它是一颗球而不是一团光
            var rim = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(112, ki, fadedKi, 0.34f, 1.40f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Scale = new Vector2(dia * 1.10f / 112f, dia / 112f),
                ZIndex = 0,
            };
            holder.AddChild(rim);

            // 白芯
            var ball = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(96, new Color(1f, 1f, 1f), core, 0.50f, 2.0f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Scale = new Vector2(dia * 0.62f / 96f, dia * 0.58f / 96f),
                ZIndex = 1,
            };
            holder.AddChild(ball);

            // 拖尾：速度朝来路方向，飞行途中一路发射
            holder.AddChild(CreateStream(
                GetParticleDotTexture(), core, ki,
                amount: echo ? 64 : 44, lifetime: 0.24f, duration: KiBlastFlightSeconds,
                radialVelocity: -190f, emissionRadius: dia * 0.20f,
                scaleMin: 0.5f, scaleMax: 1.7f * (echo ? KiBlastEchoScale : 1f),
                gravity: Vector2.Zero));

            // 出膛闪光：手心炸一下，弹丸才像是被推出去的
            var muzzle = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(128, new Color(1f, 1f, 1f), ki, 0.45f, 1.9f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0.95f),
                Scale = new Vector2(dia * 0.35f / 128f, dia * 0.35f / 128f),
                ZIndex = 27,
            };
            anchor.AddChild(muzzle);
            muzzle.GlobalScale = Vector2.One * (dia * 0.35f / 128f);
            muzzle.GlobalPosition = originWorld;
            var muzzleTw = muzzle.CreateTween();
            muzzleTw.SetParallel(true);
            muzzleTw.TweenProperty(muzzle, "scale", new Vector2(dia * 1.15f / 128f, dia * 1.15f / 128f), 0.14)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            muzzleTw.TweenProperty(muzzle, "modulate:a", 0f, 0.18);
            muzzleTw.Chain().TweenCallback(Callable.From(() => FreeIfValid(muzzle)));

            var tw = holder.CreateTween();
            tw.TweenProperty(holder, "global_position", impactWorld, KiBlastFlightSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            // 飞行末段沿运动方向拉长：速度感来自形变，不只是位移
            tw.Parallel().TweenProperty(ball, "scale",
                    new Vector2(dia * 0.88f / 96f, dia * 0.46f / 96f), KiBlastFlightSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.Parallel().TweenProperty(rim, "scale",
                    new Vector2(dia * 1.45f / 112f, dia * 0.82f / 112f), KiBlastFlightSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(() => FreeIfValid(holder)));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Ki blast projectile failed: {ex}");
        }
    }

    // 气弹的落点。
    // 不能直接用 TryGetCreatureChestWorld：那个取的是贴图矩形正中心，
    // 对矮胖敌人来说「正中心」已经接近肚子，气弹看起来就是贴着地面平飞过去打脚下。
    // 这里按贴图高度往上抬一截，落在上半身。
    private static Vector2 ResolveKiBlastImpactWorld(Creature target, Vector2 originWorld, float dir)
    {
        var targetNode = target == null ? null : NCombatRoom.Instance?.GetCreatureNode(target);
        if (targetNode?.Visuals is Node2D targetVisuals)
        {
            var targetStatic = targetVisuals.GetNodeOrNull<Sprite2D>("StaticModel");
            if (targetStatic?.Texture != null)
            {
                Rect2 rect = targetStatic.GetRect();
                Vector2 center = targetStatic.ToGlobal(rect.GetCenter());
                // 矩形高度的 18% 往上，高矮自适应；再限制一下别抬出头顶。
                float lift = Math.Min(rect.Size.Y * 0.18f * Math.Abs(targetStatic.GlobalScale.Y), 90f);
                return center - new Vector2(0f, lift);
            }
        }

        if (TryGetCreatureChestWorld(target, out Vector2 chest))
        {
            return chest;
        }

        return originWorld + new Vector2(560f * dir, 0f);
    }

    // 程序化能量束。龟波和弗利萨的死亡光线共用这一份实现——
    // 两套分叉的结果就是修好一边另一边还是墙。粗细与三段时长是唯一区别。
    // container 收 Node 而不是 Node2D：战斗特效容器是 Control。
    internal static bool SpawnEnergyBeam(
        Node container,
        Vector2 originWorld,
        Vector2 endWorld,
        Color beamColor,
        Color coreColor,
        float thicknessPixels,
        float growSeconds,
        float holdSeconds,
        float fadeSeconds,
        bool dark = false,
        float muzzleOrbScale = 0f)
    {
        string shaderPath = dark ? DarkBeamShaderPath : KamehamehaBeamShaderPath;
        if (!ResourceLoader.Exists(shaderPath))
        {
            GD.PrintErr($"[Kakarot][Vfx] beam shader missing: {shaderPath}");
            return false;
        }

        var shader = ResourceLoader.Load<Shader>(shaderPath);
        if (shader == null)
        {
            GD.PrintErr("[Kakarot][Vfx] beam shader failed to load.");
            return false;
        }

        var noise = new NoiseTexture2D
        {
            Width = 256,
            Height = 256,
            Seamless = true,
            Noise = new FastNoiseLite
            {
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                Frequency = 0.014f,
                FractalOctaves = 3,
            },
        };

        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("noise_tex", noise);
        material.SetShaderParameter("beam_color", beamColor);
        material.SetShaderParameter("core_color", coreColor);
        material.SetShaderParameter("progress", 0f);
        material.SetShaderParameter("intensity", 1f);

        var holder = new Node2D { Name = "KakarotEnergyBeamFx", ZIndex = 24 };
        container.AddChild(holder);
        holder.GlobalPosition = originWorld;
        // originWorld / endWorld 是世界坐标，而 Scale 走的是局部坐标。
        // anchor 自身带缩放时两者不等价，光束会又细又长，所以先把继承缩放归一。
        holder.GlobalScale = Vector2.One;
        holder.GlobalRotation = (endWorld - originWorld).Angle();

        // 画布只提供 UV 网格，颜色全部由 shader 覆写。
        // 🔴 必须用 ImageTexture：GradientTexture2D 是异步生成的，贴图就绪前 UV 不会正确
        // 铺满 0~1，整块面积会退化成一条线。这个坑排查过，别换回去。
        const int canvasWidth = 256;
        const int canvasHeight = 64;
        var canvasImage = Image.CreateEmpty(canvasWidth, canvasHeight, false, Image.Format.Rgba8);
        canvasImage.Fill(Colors.White);
        var canvas = ImageTexture.CreateFromImage(canvasImage);

        float distance = Math.Max(originWorld.DistanceTo(endWorld), 1f);
        // 精灵比实际距离长一截，末尾留空画布兜住波头的辉光；
        // 对应地 progress 只推到 1/headroom，波头才落在真正的终点上。
        // 不留这段余量的话，波头会被精灵右边界切出一条竖直硬边。
        float headroom = dark ? 1f : KamehamehaBeamLengthHeadroom;
        float progressTarget = 1f / headroom;
        var sprite = new Sprite2D
        {
            Texture = canvas,
            Centered = true,
            Offset = new Vector2(canvasWidth * 0.5f, 0f),
            Material = material,
            Scale = new Vector2(distance * headroom / canvasWidth, thicknessPixels / canvasHeight),
        };
        holder.AddChild(sprite);

        // 手心光球。龟波「大气」的来源不是光束本身，是光束根部那颗被推出去的气团 ——
        // 没有它，一条从手里直接伸出去的带子只会读成激光。
        // 光球钉在光束原点（holder 的局部零点），不改光束几何，所以不需要重调手臂位置。
        if (muzzleOrbScale > 0f)
        {
            SpawnBeamMuzzleOrb(holder, beamColor, coreColor, thicknessPixels, muzzleOrbScale,
                growSeconds, holdSeconds, fadeSeconds);
        }

        var tween = holder.CreateTween();
        tween.TweenProperty(material, "shader_parameter/progress", progressTarget, growSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(holdSeconds);
        tween.TweenProperty(material, "shader_parameter/intensity", 0f, fadeSeconds);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(holder))
            {
                holder.QueueFree();
            }
        };

        return true;
    }

    // 光束根部的气团。三段跟着光束走：起手炸开 -> 保持时轻微呼吸 -> 随光束一起淡出。
    // holder 已按发射方向旋转过，径向辉光各向同性，不用反向补偿。
    //
    // 这里踩过的坑（离屏渲染逐轮验出来的，别再走回头路）：
    //   · 球径必须绑定「光束根部宽度」而不是画布。只比根部粗一点点的话，
    //     白芯一软化就被主干吞掉，看起来根本没有球。
    //   · 纯白硬核 + 正圆 = 「卡了一个球」。要的是球面渐变 + 蓝边轮廓，
    //     靠「比主干粗」读出球形，不是靠「比主干白」。
    //   · 椭圆 + 往前压进主干 + 根部收口放大，三个一起用接缝才藏得住。
    private static void SpawnBeamMuzzleOrb(
        Node2D holder, Color beamColor, Color coreColor,
        float thicknessPixels, float orbScale,
        float growSeconds, float holdSeconds, float fadeSeconds)
    {
        try
        {
            // 光束根部实际像素宽度 = neck_width * thickness * 画布高度
            float rootPx = KamehamehaBeamNeckWidth * KamehamehaBeamThicknessRatio * thicknessPixels;
            float dia = rootPx * orbScale;
            float ex = KamehamehaMuzzleOrbStretch;
            float push = dia * KamehamehaMuzzleOrbPush;

            var fadedBeam = new Color(beamColor.R, beamColor.G, beamColor.B, 0f);

            var halo = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(160, beamColor, fadedBeam, 0f, 1.55f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.2f, 0.2f),
                Position = new Vector2(push, 0f),
                ZIndex = -1,
            };
            holder.AddChild(halo);

            // 蓝边层：比白芯大一圈的纯蓝球，球的轮廓靠它读出来
            var rim = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(144, beamColor, fadedBeam, 0.30f, 1.32f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.15f, 0.15f),
                Position = new Vector2(push, 0f),
                ZIndex = 0,
            };
            holder.AddChild(rim);

            // 白芯：明显小于蓝边层，蓝边才露得出来
            var core = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(128, new Color(1f, 1f, 1f), coreColor, 0.45f, 1.86f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.1f, 0.1f),
                Position = new Vector2(push, 0f),
                ZIndex = 1,
            };
            holder.AddChild(core);

            var haloPeak = new Vector2(dia * 1.30f * ex / 160f, dia * 1.18f / 160f);
            var rimPeak = new Vector2(dia * ex / 144f, dia / 144f);
            var corePeak = new Vector2(dia * 0.58f * ex / 128f, dia * 0.58f / 128f);

            var tw = holder.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(halo, "modulate:a", 0.85f, growSeconds * 0.45)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(halo, "scale", haloPeak, growSeconds * 0.7)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(rim, "modulate:a", 1f, growSeconds * 0.40)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(rim, "scale", rimPeak, growSeconds * 0.65)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(core, "modulate:a", 0.95f, growSeconds * 0.35)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(core, "scale", corePeak, growSeconds * 0.6)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);

            // 保持段轻微收缩再回弹：静止不动的球会读成贴图
            tw.Chain().TweenProperty(rim, "scale", rimPeak * 0.90f, holdSeconds * 0.5)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tw.Chain().TweenProperty(rim, "scale", rimPeak * 1.02f, holdSeconds * 0.5)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

            // 和光束同时收，别让球比光束活得久
            tw.Chain().TweenProperty(halo, "modulate:a", 0f, fadeSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Parallel().TweenProperty(rim, "modulate:a", 0f, fadeSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Parallel().TweenProperty(core, "modulate:a", 0f, fadeSeconds * 0.8)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Beam muzzle orb failed: {ex}");
        }
    }

    // CreateRadialGlowTexture 的可调版：coreK = 中心实心核强度（0 = 纯软晕），
    // falloff = 外圈衰减指数（越小越软）。原函数把这两个写死成 0.55 / 3.2，
    // 那组值画出来是「硬白球」，和程序化光束拼不到一起。
    internal static ImageTexture CreateSoftGlowTexture(
        int size, Color inner, Color outer, float coreK, float falloff)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d >= 1f)
                {
                    img.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }

                Color c = inner.Lerp(outer, Mathf.Pow(d, 0.85f));
                float k = coreK * Mathf.Exp(-((d / 0.17f) * (d / 0.17f)));
                c.A = Mathf.Min(1f, Mathf.Pow(1f - d, falloff) + k);
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
