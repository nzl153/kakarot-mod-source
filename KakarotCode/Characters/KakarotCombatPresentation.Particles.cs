using System;
using Godot;

namespace KakarotMod.KakarotCode.Characters;

// 粒子层。
//
// 在这之前所有「粒子」都是手写 tween 一个个补的：元气弹 22 颗、破坏 16 片碎片。
// 这个做法在 20 个元素以内没问题，到 200 个就崩了——代码量线性增长，
// 而且做不出持续发射的拖尾、灰尘、余烬流。
//
// GpuParticles2D 把发射率、生命周期、重力、颜色渐变全变成配置，
// 几百上千颗零额外代码。原版自己在 149 个战斗特效场景里用的就是这套
// （one_shot + explosiveness + local_coords 是它的惯例，这里照抄）。
//
// 🔴 所有渐变贴图一律 ImageTexture 自己画，不用 GradientTexture1D / CurveTexture。
// 理由和龟波光束那次一样：那些是异步生成的，就绪前采样会退化。
//
// 🚨 但 scale_curve 有个致命细节，2026-08-24 才查出来：
// **引擎把采样出来的 RGB 当成三个轴的缩放**，不是「只读红通道」。
// 以前这里写的是 new Color(v, 0, 0, 1) —— Y 轴缩放为 0，
// 粒子被压成零高度，三个工厂的粒子**全都是不可见的**，而且编译期毫无征兆。
// 一律走 CreateScaleRamp，别再手写 new Color(v, 0, 0, 1)。
public static partial class KakarotCombatPresentation
{
    private static ImageTexture _particleDotTex;

    // 一次性爆发型发射器。调用方拿到节点后挂进树里即可，自己会在放完后释放。
    //
    // baseAngle/halfSpread 用弧度；gravity 正 Y 是往下（Godot 2D 的 Y 轴朝下）。
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
            // 2D 必开，否则粒子会在 Z 轴上散开，表现为大小忽大忽小
            ParticleFlagDisableZ = true,
            ParticleFlagDampingAsFriction = true,

            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 6f,

            Direction = new Vector3(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle), 0f),
            Spread = Mathf.RadToDeg(halfSpread),

            InitialVelocityMin = speedMin,
            InitialVelocityMax = speedMax,
            Gravity = new Vector3(gravity.X, gravity.Y, 0f),
            // Damping 是 Vector2(min, max)，不是单个 float。给个范围，
            // 全部粒子同一个阻尼会同时刹停，一看就是程序生成的。
            // ⚠ 配 ParticleFlagDampingAsFriction 时数值很敏感：42 就足以让粒子
            // 几乎原地不动，读作贴在身上的一撮碎屑。减速交给重力，这里默认给 0。
            Damping = new Vector2(damping * 0.65f, damping),

            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            ScaleCurve = CreateScaleRamp(t => Mathf.Lerp(1f, 0.18f, Mathf.Pow(t, 1.4f))),

            // 命中特效的关键是「亮起来再烧尽」，不是线性淡出。
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
            // 0.95 = 几乎同时全部射出。0 是均匀持续发射，那是拖尾/余烬流才要的。
            Explosiveness = 0.95f,
            FixedFps = 60,
            LocalCoords = true,
            Emitting = true,
            ZIndex = 31,

            // 默认只有 Rect2(-100,-100,200,200)。粒子会飞出 200px，
            // 这个框太小会让整个发射器被判定为不需要激活。
            VisibilityRect = new Rect2(-800f, -800f, 1600f, 1600f),
        };

        // 🔴 不用 Finished 信号收尾。one_shot 在还没真正开始发射时就可能
        // 触发一次 finished，那样节点会在发出任何粒子之前被 QueueFree，
        // 表现为「特效完全看不见」。改成入树后挂一个定时器，和光束那边一致。
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

    // 持续发射型。这是手写 tween 根本做不到的那一类：
    // 蓄力时一直有东西往里汇聚、爆炸后一直有余烬在飘。
    // radialVelocity 取负值就是向心汇聚（配环形发射面），正值是向外扩散。
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

            // 环形发射面 + 向心径向速度 = 从四面八方汇聚到一点。
            // 手写要给每颗粒子算起点和插值，这里是四个属性。
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

            ColorRamp = CreateRampTexture(t => startColor
                .Lerp(endColor, Mathf.Pow(t, 0.7f)) with
            {
                // 汇聚型要「淡入-亮-烧尽」，直接从 1 开始会在环上凭空出现一圈。
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
            // 0 = 均匀持续发射。这正是和爆发型的唯一区别。
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

            // 先停止发射，再等最后一批粒子活完，避免整片突然消失。
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

    // 常驻型：一直发射，不自己收尾，随宿主节点一起释放。
    // 「一直在冒东西」这件事只有持续发射做得到，这是和贴图特效最大的分野。
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

            // 从一个包住身体的矩形里冒，而不是从一个点——常驻气场要贴着轮廓。
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

            // 两头都淡：常驻效果最忌讳粒子「凭空出现、凭空消失」。
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

    // scale_curve 专用：把同一个值写进 RGB 三通道。
    // 见文件头那条 🚨 —— 只写红通道会让粒子 Y 轴缩放变 0，整层不可见。
    internal static ImageTexture CreateScaleRamp(Func<float, float> sample, int width = 64)
    {
        return CreateRampTexture(t =>
        {
            float v = sample(t);
            return new Color(v, v, v, 1f);
        }, width);
    }

    // N×1 的渐变条，同步生成。给 color_ramp 用时四个通道都有意义，
    // 给 scale_curve 用时引擎只读红通道。
    internal static ImageTexture CreateRampTexture(Func<float, Color> sample, int width = 64)
    {
        var img = Image.CreateEmpty(width, 1, false, Image.Format.Rgba8);
        for (int x = 0; x < width; x++)
        {
            img.SetPixel(x, 0, sample(width <= 1 ? 0f : x / (float)(width - 1)));
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 粒子用的小圆点。命中一次要发几十颗，用 128 的辉光图是白烧填充率。
    internal static ImageTexture GetParticleDotTexture()
    {
        return _particleDotTex ??= CreateRadialGlowTexture(
            32, new Color(1f, 1f, 1f), new Color(1f, 1f, 1f));
    }
}
