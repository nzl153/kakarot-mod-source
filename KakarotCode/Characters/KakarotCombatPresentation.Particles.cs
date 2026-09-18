using System;
using Godot;

namespace KakarotMod.KakarotCode.Characters;

// 通用粒子工厂。使用 GpuParticles2D 统一处理爆发、持续和常驻粒子。
// 渐变使用同步生成的 ImageTexture，避免异步纹理未就绪时采样退化。
// scale_curve 的 RGB 分别参与轴向缩放，因此三个通道必须写入相同值；只写红通道会使 Y 轴缩放为 0。
public static partial class KakarotCombatPresentation
{
    private static ImageTexture _particleDotTex;

    // 一次性爆发型发射器；baseAngle/halfSpread 使用弧度，Godot 2D 的正 Y 向下。
    internal static GpuParticles2D CreateBurst(
        Texture2D texture,
        Color startColor,
        Color endColor,
        int amount,
        float lifetime,
        float speedMin,
        float speedMax,
        float scaleMin,
        float scaleMax,
        float baseAngle,
        float halfSpread,
        Vector2 gravity,
        float damping)
    {
        var process = new ParticleProcessMaterial
        {
            // 2D 粒子禁用 Z 轴扩散。
            ParticleFlagDisableZ = true,
            ParticleFlagDampingAsFriction = true,

            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 6f,

            Direction = new Vector3(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle), 0f),
            Spread = Mathf.RadToDeg(halfSpread),

            InitialVelocityMin = speedMin,
            InitialVelocityMax = speedMax,
            Gravity = new Vector3(gravity.X, gravity.Y, 0f),
            // 阻尼使用范围值避免粒子同时减速；默认由重力负责减速。
            Damping = new Vector2(damping * 0.65f, damping),

            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            ScaleCurve = CreateScaleRamp(t => Mathf.Lerp(1f, 0.18f, Mathf.Pow(t, 1.4f))),

            // 先亮后衰减，避免线性淡出。
            ColorRamp = CreateRampTexture(t => startColor
                .Lerp(endColor, Mathf.Pow(t, 0.7f)) with { A = Mathf.Pow(1f - t, 1.6f) }),

            LifetimeRandomness = 0.3f,
        };

        var node = new GpuParticles2D
        {
            Texture = texture,
            Material = CreateAdditiveMaterial(),
            ProcessMaterial = process,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = true,
            // 接近 1 表示集中爆发。
            Explosiveness = 0.95f,
            FixedFps = 60,
            LocalCoords = true,
            Emitting = true,
            ZIndex = 31,

            // 放大发射器可见区域，避免高速粒子被过早裁剪。
            VisibilityRect = new Rect2(-800f, -800f, 1600f, 1600f),
        };

        // one_shot 的 Finished 可能在首次发射前触发，因此使用定时器延迟清理。
        node.TreeEntered += () =>
        {
            if (!GodotObject.IsInstanceValid(node))
            {
                return;
            }

            node.Restart();

            var tree = node.GetTree();
            if (tree == null)
            {
                return;
            }

            tree.CreateTimer(lifetime * 1.8f + 0.2f).Timeout += () => FreeIfValid(node);
        };

        return node;
    }

    // 持续发射型；负 radialVelocity 向心汇聚，正值向外扩散。
    internal static GpuParticles2D CreateStream(
        Texture2D texture,
        Color startColor,
        Color endColor,
        int amount,
        float lifetime,
        float duration,
        float radialVelocity,
        float emissionRadius,
        float scaleMin,
        float scaleMax,
        Vector2 gravity)
    {
        var process = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true,

            // 环形发射面配合径向速度实现汇聚或扩散。
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingAxis = new Vector3(0f, 0f, 1f),
            EmissionRingHeight = 1f,
            EmissionRingRadius = emissionRadius,
            EmissionRingInnerRadius = emissionRadius * 0.72f,

            RadialVelocityMin = radialVelocity * 0.7f,
            RadialVelocityMax = radialVelocity,

            InitialVelocityMin = 0f,
            InitialVelocityMax = 0f,
            Gravity = new Vector3(gravity.X, gravity.Y, 0f),

            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            ScaleCurve = CreateScaleRamp(t =>
                Mathf.Lerp(0.35f, 1f, Mathf.Min(1f, t * 4f)) * Mathf.Lerp(1f, 0.5f, t)),

            // 汇聚粒子先淡入再衰减，避免在发射环上突现。
            ColorRamp = CreateRampTexture(t => startColor
                .Lerp(endColor, Mathf.Pow(t, 0.7f)) with
            {
                A = Mathf.Min(1f, t * 5f) * Mathf.Pow(1f - t, 1.2f) * 2.2f,
            }),

            LifetimeRandomness = 0.35f,
        };

        var node = new GpuParticles2D
        {
            Texture = texture,
            Material = CreateAdditiveMaterial(),
            ProcessMaterial = process,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = false,
            // 0 表示均匀持续发射。
            Explosiveness = 0f,
            FixedFps = 60,
            LocalCoords = true,
            Emitting = true,
            ZIndex = 21,
            VisibilityRect = new Rect2(-900f, -900f, 1800f, 1800f),
        };

        node.TreeEntered += () =>
        {
            if (!GodotObject.IsInstanceValid(node))
            {
                return;
            }

            node.Restart();

            var tree = node.GetTree();
            if (tree == null)
            {
                return;
            }

            // 先停止发射，再等待最后一批粒子结束。
            tree.CreateTimer(duration).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(node))
                {
                    node.Emitting = false;
                }
            };
            tree.CreateTimer(duration + lifetime * 1.4f).Timeout += () => FreeIfValid(node);
        };

        return node;
    }

    // 常驻型发射器，不主动清理，生命周期随宿主节点。
    internal static GpuParticles2D CreateAmbientStream(
        Texture2D texture,
        Color startColor,
        Color endColor,
        int amount,
        float lifetime,
        Vector2 areaExtents,
        Vector2 drift,
        float scaleMin,
        float scaleMax)
    {
        var process = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true,

            // 从覆盖角色轮廓的矩形区域发射。
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(areaExtents.X, areaExtents.Y, 1f),

            Direction = new Vector3(0f, -1f, 0f),
            Spread = 32f,
            InitialVelocityMin = 12f,
            InitialVelocityMax = 46f,
            Gravity = new Vector3(drift.X, drift.Y, 0f),

            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            ScaleCurve = CreateScaleRamp(t =>
                Mathf.Lerp(0.5f, 1f, Mathf.Min(1f, t * 3f)) * Mathf.Lerp(1f, 0.35f, t)),

            // 头尾淡入淡出，避免粒子突现或突消。
            ColorRamp = CreateRampTexture(t => startColor.Lerp(endColor, Mathf.Pow(t, 0.7f)) with
            {
                A = Mathf.Min(1f, t * 6f) * Mathf.Pow(1f - t, 1.1f) * 2.0f,
            }),

            LifetimeRandomness = 0.45f,
        };

        return new GpuParticles2D
        {
            Texture = texture,
            Material = CreateAdditiveMaterial(),
            ProcessMaterial = process,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = false,
            Explosiveness = 0f,
            FixedFps = 30,
            LocalCoords = true,
            Emitting = true,
            ZIndex = -1,
            VisibilityRect = new Rect2(-500f, -700f, 1000f, 1200f),
        };
    }

    // scale_curve 使用 RGB 三通道表示轴缩放，因此写入相同值。
    internal static ImageTexture CreateScaleRamp(Func<float, float> sample, int width = 64)
    {
        return CreateRampTexture(t =>
        {
            float v = sample(t);
            return new Color(v, v, v, 1f);
        }, width);
    }

    // N×1 同步渐变条；color_ramp 使用 RGBA，scale_curve 使用 RGB 轴缩放。
    internal static ImageTexture CreateRampTexture(Func<float, Color> sample, int width = 64)
    {
        var img = Image.CreateEmpty(width, 1, false, Image.Format.Rgba8);
        for (int x = 0; x < width; x++)
        {
            img.SetPixel(x, 0, sample(width <= 1 ? 0f : x / (float)(width - 1)));
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 粒子使用较小的圆点纹理以降低重复播放成本。
    internal static ImageTexture GetParticleDotTexture()
    {
        return _particleDotTex ??= CreateRadialGlowTexture(
            32, new Color(1f, 1f, 1f), new Color(1f, 1f, 1f));
    }
}
