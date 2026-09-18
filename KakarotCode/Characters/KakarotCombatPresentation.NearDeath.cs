using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KakarotMod.KakarotCode.Characters;

// 濒死气场用于提示 SaiyanBlood 的 HP ≤ 20% 加成。
// 状态必须由 SaiyanBlood 的 near-death 状态驱动，避免未持有遗物时错误显示。
// 使用暗红配色以区分超三金色和超蓝蓝色气场；仅修改表现层，不参与同步。
public static partial class KakarotCombatPresentation
{
    private const string NearDeathAuraNodeName = "KakarotNearDeathAura";

    private static readonly Color NearDeathEmberStart = new(1.00f, 0.24f, 0.18f, 1f);
    private static readonly Color NearDeathEmberEnd = new(0.55f, 0.05f, 0.06f, 0f);

    // 环绕层使用更暗的收尾色，避免与余烬混在一起。
    private static readonly Color NearDeathOrbiterEnd = new(0.70f, 0.08f, 0.10f, 0f);

    // 常驻呼吸效果保持低幅度，避免持续闪烁干扰战斗。
    private static readonly Color NearDeathPulseTint = new(1.00f, 0.78f, 0.76f, 1f);
    private const float NearDeathPulseSeconds = 0.85f;

    private static Tween _nearDeathPulseTween;

    public static void SetNearDeathAura(Creature creature, bool active)
    {
        try
        {
            var staticModel = NCombatRoom.Instance?
                .GetCreatureNode(creature)?
                .Visuals?
                .GetNodeOrNull<Sprite2D>("StaticModel");

            if (staticModel == null)
            {
                // 缺少 StaticModel 时记录日志，便于定位特效未生成的问题。
                GD.PrintErr("[Kakarot][Vfx] near-death aura: StaticModel not found.");
                return;
            }

            // 使用标准静止缩放；staticModel.Scale 会被呼吸和死亡动画临时修改。
            SetNearDeathAura(staticModel, active, KakarotFormVisuals.GetRestTransform(creature).Scale);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] near-death aura failed: {ex}");
        }
    }

    // 死亡路径只有 Sprite2D 可用，因此保留此重载。
    internal static void SetNearDeathAura(Sprite2D staticModel, bool active, Vector2? restScale = null)
    {
        try
        {
            if (staticModel == null || !GodotObject.IsInstanceValid(staticModel))
            {
                return;
            }

            // 两层粒子共用容器，便于统一停止和清理。
            var existing = staticModel.GetNodeOrNull<Node2D>(NearDeathAuraNodeName);

            if (!active)
            {
                StopNearDeathPulse(staticModel);
                if (existing != null)
                {
                    float longest = 0f;
                    foreach (var child in existing.GetChildren())
                    {
                        if (child is GpuParticles2D ps)
                        {
                            // 先停止发射并等待现有粒子结束，避免瞬间消失。
                            ps.Emitting = false;
                            longest = Math.Max(longest, (float)ps.Lifetime);
                        }
                    }

                    var cleanup = existing.CreateTween();
                    cleanup.TweenInterval(longest);
                    cleanup.TweenCallback(Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(existing))
                        {
                            existing.QueueFree();
                        }
                    }));
                }

                return;
            }

            if (existing == null)
            {
                // 子粒子会继承立绘缩放，因此反向抵消父级缩放，并统一按屏幕像素设置尺寸。
                var holder = new Node2D { Name = NearDeathAuraNodeName };
                staticModel.AddChild(holder);
                var baseScale = restScale ?? staticModel.Scale;
                holder.Scale = new Vector2(
                    1f / Math.Max(Math.Abs(baseScale.X), 0.001f),
                    1f / Math.Max(Math.Abs(baseScale.Y), 0.001f));

                // 上升余烬使用屏幕像素尺寸。
                var embers = CreateAmbientStream(
                    GetParticleDotTexture(),
                    NearDeathEmberStart,
                    NearDeathEmberEnd,
                    amount: 46,
                    lifetime: 1.25f,
                    // 贴图包含大量透明区域，发射范围按角色实际可见尺寸设置。
                    areaExtents: new Vector2(130f, 150f),
                    drift: new Vector2(0f, -58f),
                    scaleMin: 1.1f,
                    scaleMax: 2.4f);
                embers.Name = "Embers";
                // 子节点 ZIndex 相对父级，余烬放在立绘后方。
                embers.ZIndex = -1;
                // 从身体中下部向上发射。
                embers.Position = new Vector2(0f, 110f);
                holder.AddChild(embers);

                // 环绕红点。
                var orbiters = CreateNearDeathOrbiters();
                orbiters.Name = "Orbiters";
                // 环绕层较淡，放在立绘前方确保可见。
                orbiters.ZIndex = 1;
                holder.AddChild(orbiters);
            }
            else
            {
                foreach (var child in existing.GetChildren())
                {
                    if (child is GpuParticles2D ps)
                    {
                        ps.Emitting = true;
                    }
                }
            }

            StartNearDeathPulse(staticModel);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] near-death aura failed: {ex}");
        }
    }

    // 环绕红点依赖 2D orbit_velocity，因此 ParticleFlagDisableZ 必须开启。
    private static GpuParticles2D CreateNearDeathOrbiters()
    {
        // 环半径略大于角色可见宽度。
        const float radius = 155f;

        var process = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true,

            // 环形发射使粒子出生时已分布在角色周围。
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingAxis = new Vector3(0f, 0f, 1f),
            EmissionRingRadius = radius,
            EmissionRingInnerRadius = radius * 0.62f,
            EmissionRingHeight = 0f,

            // 初速度接近零，主要位移由 orbit_velocity 提供。
            Direction = new Vector3(0f, -1f, 0f),
            Spread = 180f,
            InitialVelocityMin = 0f,
            InitialVelocityMax = 6f,
            Gravity = Vector3.Zero,

            // orbit_velocity 单位为圈/秒。
            OrbitVelocityMin = 0.06f,
            OrbitVelocityMax = 0.14f,

            ScaleMin = 0.55f,
            ScaleMax = 1.25f,
            ScaleCurve = CreateScaleRamp(t =>
                Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, t * 4f)) * Mathf.Lerp(1f, 0.5f, t)),

            // 峰值透明度保持较低，仅作为轻微状态提示。
            ColorRamp = CreateRampTexture(t => NearDeathEmberStart.Lerp(NearDeathOrbiterEnd, Mathf.Pow(t, 0.8f)) with
            {
                A = Mathf.Min(1f, t * 5f) * Mathf.Pow(1f - t, 0.9f) * 1.35f,
            }),

            LifetimeRandomness = 0.55f,
        };

        return new GpuParticles2D
        {
            Texture = GetParticleDotTexture(),
            Material = CreateAdditiveMaterial(),
            ProcessMaterial = process,
            Amount = 14,
            Lifetime = 2.6f,
            OneShot = false,
            Explosiveness = 0f,
            // 预热粒子，避免气场刚触发时环形区域为空。
            Preprocess = 1.8f,
            FixedFps = 30,
            LocalCoords = true,
            Emitting = true,
            ZIndex = -1,
            VisibilityRect = new Rect2(-500f, -700f, 1000f, 1200f),
        };
    }

    private static void StartNearDeathPulse(Sprite2D staticModel)
    {
        StopNearDeathPulse(staticModel);

        _nearDeathPulseTween = staticModel.CreateTween();
        _nearDeathPulseTween.SetLoops();
        _nearDeathPulseTween.TweenProperty(staticModel, "modulate", NearDeathPulseTint, NearDeathPulseSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _nearDeathPulseTween.TweenProperty(staticModel, "modulate", Colors.White, NearDeathPulseSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private static void StopNearDeathPulse(Sprite2D staticModel)
    {
        if (_nearDeathPulseTween != null && _nearDeathPulseTween.IsValid())
        {
            _nearDeathPulseTween.Kill();
        }

        _nearDeathPulseTween = null;

        // 停止循环后恢复立绘颜色，避免残留淡红调制。
        if (GodotObject.IsInstanceValid(staticModel))
        {
            staticModel.Modulate = Colors.White;
        }
    }
}
