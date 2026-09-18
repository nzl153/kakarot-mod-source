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

// 通用能量投射物：光束引擎与气弹弹道。
// SpawnEnergyBeam 同时供龟波和弗利萨死亡光线使用，因此与具体招式逻辑分离。
public static partial class KakarotCombatPresentation
{
    // 暗色变体使用普通混合，以便真正压暗背景。
    private const string DarkBeamShaderPath = "res://Kakarot/Shaders/kakarot_energy_beam_dark.gdshader";

    // 气功弹复用龟波的软晕、轮廓和白芯结构。
    // 由卡牌主动调用，因为一次结算可能生成两发，而出牌派发表只触发一次。
    internal const float KiBlastFlightSeconds = 0.18f;

    // 弹丸直径使用屏幕像素，确保实体感。
    private const float KiBlastDiameter = 132f;

    // 第二发气弹的缩放倍数。
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

            // 起点按当前形态计算，避免变身后固定偏移失效。
            Vector2 originWorld = ComputeKamehamehaBeamOriginWorld(staticModel, player.Creature);

            Vector2 impactWorld = ResolveKiBlastImpactWorld(cardPlay.Target, originWorld, dir);

            Color ki = KiDefaultColor;
            Color core = ki.Lerp(new Color(1f, 1f, 1f), echo ? 0.80f : 0.62f);
            float dia = KiBlastDiameter * (echo ? KiBlastEchoScale : 1f);

            var holder = new Node2D { Name = echo ? "KakarotKiBlastEchoShot" : "KakarotKiBlastShot", ZIndex = 26 };
            anchor.AddChild(holder);
            // 归一父级缩放，避免弹丸被角色缩放再次影响。
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = originWorld;
            holder.GlobalRotation = (impactWorld - originWorld).Angle();

            var fadedKi = new Color(ki.R, ki.G, ki.B, 0f);

            // 外晕。
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

            // 边轮廓用于保持球体边界。
            var rim = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(112, ki, fadedKi, 0.34f, 1.40f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Scale = new Vector2(dia * 1.10f / 112f, dia / 112f),
                ZIndex = 0,
            };
            holder.AddChild(rim);

            // 白芯。
            var ball = new Sprite2D
            {
                Texture = CreateSoftGlowTexture(96, new Color(1f, 1f, 1f), core, 0.50f, 2.0f),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Scale = new Vector2(dia * 0.62f / 96f, dia * 0.58f / 96f),
                ZIndex = 1,
            };
            holder.AddChild(ball);

            // 拖尾沿飞行路径持续发射。
            holder.AddChild(CreateStream(
                GetParticleDotTexture(), core, ki,
                amount: echo ? 64 : 44, lifetime: 0.24f, duration: KiBlastFlightSeconds,
                radialVelocity: -190f, emissionRadius: dia * 0.20f,
                scaleMin: 0.5f, scaleMax: 1.7f * (echo ? KiBlastEchoScale : 1f),
                gravity: Vector2.Zero));

            // 出膛闪光强化发射反馈。
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
            // 末段沿运动方向拉长以强化速度感。
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

    // 目标贴图中心对部分体型偏低，因此按贴图高度上移落点，并保留胸口定位作为回退。
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
                // 按可见高度上移，并限制最大偏移。
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

    // 程序化能量束，龟波与弗利萨死亡光线共用；container 使用 Node 以兼容 Control 特效容器。
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
        // 输入为世界坐标，先归一 holder 缩放以避免继承角色缩放。
        holder.GlobalScale = Vector2.One;
        holder.GlobalRotation = (endWorld - originWorld).Angle();

        // 使用同步生成的 ImageTexture 作为 UV 画布，避免异步纹理未就绪时 UV 退化。
        const int canvasWidth = 256;
        const int canvasHeight = 64;
        var canvasImage = Image.CreateEmpty(canvasWidth, canvasHeight, false, Image.Format.Rgba8);
        canvasImage.Fill(Colors.White);
        var canvas = ImageTexture.CreateFromImage(canvasImage);

        float distance = Math.Max(originWorld.DistanceTo(endWorld), 1f);
        // 画布末端预留辉光空间，并修正 progress，使波头落在真实终点且不被边界裁切。
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

        // 根部光球提供发射端体积，固定在 holder 原点，不改变光束几何。
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

    // 根部光球随光束完成出现、保持和淡出。
    // 尺寸相对根部宽度计算；蓝色轮廓大于白芯并略作椭圆拉伸，使球体与主干自然衔接。
    private static void SpawnBeamMuzzleOrb(
        Node2D holder, Color beamColor, Color coreColor,
        float thicknessPixels, float orbScale,
        float growSeconds, float holdSeconds, float fadeSeconds)
    {
        try
        {
            // 光束根部实际像素宽度 = neck_width * thickness * 画布高度。
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

            // 蓝色轮廓略大于白芯。
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

            // 白芯小于轮廓层以保留蓝边。
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

            // 保持阶段轻微呼吸，避免静态贴图感。
            tw.Chain().TweenProperty(rim, "scale", rimPeak * 0.90f, holdSeconds * 0.5)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tw.Chain().TweenProperty(rim, "scale", rimPeak * 1.02f, holdSeconds * 0.5)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

            // 与光束同步淡出。
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

    // 可调径向辉光：coreK 控制中心实心核，falloff 控制外圈衰减。
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
