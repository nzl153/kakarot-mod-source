using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KakarotMod.KakarotCode.Characters;

// 濒死气场（血量 ≤ 20%）。
//
// 这一层存在的理由不是「好看」，是**把一个隐形机制变成看得见的**：
// 赛亚人之血在 HP ≤ 20% 时给 +2 力量 +2 灵敏，但反馈只有遗物图标闪一下，太容易错过。
//
// 🔴 开关必须由 SaiyanBlood 的 near-death 状态驱动，**绝不能自己去判血量百分比**。
// 那个加成挂在遗物上，没拿到遗物的人血量低了不该看见气场——
// 否则等于用视觉承诺了一个玩家根本没有的加成，联机时队友看着更乱。
// 唯二的开关点是 SaiyanBlood.UpdateNearDeathBoost 里 _nearDeathBoostApplied 翻转的两个分支。
//
// 配色：暗红上升余烬。**不能用金色或蓝色**——超三本来就是金气场、超蓝是蓝气场，
// 同色叠上去等于没加。暗红和现有的形态气场都不撞。
//
// 纯表现层，不碰任何同步状态，联机确定性无风险。
public static partial class KakarotCombatPresentation
{
    private const string NearDeathAuraNodeName = "KakarotNearDeathAura";

    private static readonly Color NearDeathEmberStart = new(1.00f, 0.24f, 0.18f, 1f);
    private static readonly Color NearDeathEmberEnd = new(0.55f, 0.05f, 0.06f, 0f);

    // 环绕层收尾偏暗红，不像余烬那样烧到发白。
    private static readonly Color NearDeathOrbiterEnd = new(0.70f, 0.08f, 0.10f, 0f);

    // 立绘的红色呼吸脉动：幅度必须很轻。这是常驻效果，
    // 一整场战斗都在闪的话，几分钟后玩家会开始烦。
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
                // 静默返回会让「特效没出来」完全无法定位，留一行日志。
                GD.PrintErr("[Kakarot][Vfx] near-death aura: StaticModel not found.");
                return;
            }

            // 传标准静止缩放，别用 staticModel.Scale：
            // 呼吸动画一直在改它，而且战斗开始时它可能还停在死亡缩放上。
            SetNearDeathAura(staticModel, active, KakarotFormVisuals.GetRestTransform(creature).Scale);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] near-death aura failed: {ex}");
        }
    }

    // 死亡路径只拿得到 Sprite2D（此时 Creature 已经是 dead 状态），所以拆一个重载。
    internal static void SetNearDeathAura(Sprite2D staticModel, bool active, Vector2? restScale = null)
    {
        try
        {
            if (staticModel == null || !GodotObject.IsInstanceValid(staticModel))
            {
                return;
            }

            // 两层粒子收进一个容器，清理逻辑不用维护两份。
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
                            // 先停发射再等粒子自然烧完，直接 QueueFree 会让满屏粒子瞬间消失。
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
                // 🚨 立绘贴图是 2048×2048，但显示缩放只有 0.30（超三形态 0.176）。
                // 粒子作为它的子节点，所有尺寸都会被乘一遍这个缩放 ——
                // 第一版按 rect.Size 的比例算，结果每颗粒子在屏幕上只有 2~7 像素、
                // 还撒在 500 多像素宽的范围里，表现就是「闪一下就没了」。
                // 解法：容器反向抵消模型缩放，内部一律按**屏幕像素**写尺寸，
                // 顺带换形态时也自适应（0.30 和 0.176 都对）。
                var holder = new Node2D { Name = NearDeathAuraNodeName };
                staticModel.AddChild(holder);
                var baseScale = restScale ?? staticModel.Scale;
                holder.Scale = new Vector2(
                    1f / Math.Max(Math.Abs(baseScale.X), 0.001f),
                    1f / Math.Max(Math.Abs(baseScale.Y), 0.001f));

                // ① 上升余烬：主体，读作「烧起来了」。数值单位 = 屏幕像素。
                var embers = CreateAmbientStream(
                    GetParticleDotTexture(),
                    NearDeathEmberStart,
                    NearDeathEmberEnd,
                    amount: 46,
                    lifetime: 1.25f,
                    // 贴图 2048 里绝大部分是透明留白，不能拿它算范围。
                    // 悟空实际占屏约 200×420，这里比身体略宽让余烬从两侧透出来。
                    areaExtents: new Vector2(130f, 150f),
                    drift: new Vector2(0f, -58f),
                    scaleMin: 1.1f,
                    scaleMax: 2.4f);
                embers.Name = "Embers";
                // 子节点的 z 默认相对父级：-1 = 立绘背后，+1 = 立绘前面。
                embers.ZIndex = -1;
                // 从身体中下部往上飘，从脚边升起来才像「烧起来了」。
                embers.Position = new Vector2(0f, 110f);
                holder.AddChild(embers);

                // ② 环绕红点
                var orbiters = CreateNearDeathOrbiters();
                orbiters.Name = "Orbiters";
                // 环绕层放到立绘**前面**：它本来就稀疏又淡，再放背后就等于没有。
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

    // 环绕身体转圈的稀疏红点。靠 ParticleProcessMaterial 的 orbit_velocity 实现，
    // 这个属性只在 2D（ParticleFlagDisableZ = true）下有效。
    private static GpuParticles2D CreateNearDeathOrbiters()
    {
        // 屏幕像素。悟空占屏约 200 宽，环略大于身体才读得出「围绕」。
        const float radius = 155f;

        var process = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true,

            // 从一个环上生成，粒子一出生就已经在「绕着身体」的位置上。
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingAxis = new Vector3(0f, 0f, 1f),
            EmissionRingRadius = radius,
            EmissionRingInnerRadius = radius * 0.62f,
            EmissionRingHeight = 0f,

            // 自身速度压到接近零，位移几乎全部来自 orbit —— 否则会飘走而不是绕。
            Direction = new Vector3(0f, -1f, 0f),
            Spread = 180f,
            InitialVelocityMin = 0f,
            InitialVelocityMax = 6f,
            Gravity = Vector3.Zero,

            // 单位是「圈/秒」。0.10 大约十秒一圈，慢到不抢眼。
            OrbitVelocityMin = 0.06f,
            OrbitVelocityMax = 0.14f,

            ScaleMin = 0.55f,
            ScaleMax = 1.25f,
            ScaleCurve = CreateScaleRamp(t =>
                Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, t * 4f)) * Mathf.Lerp(1f, 0.5f, t)),

            // 峰值透明度压到 0.5 以下：知乐要的是「注意看才发现」。
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
            // 一出场就是「已经绕了一会儿」的状态，不然刚触发时环上是空的。
            Preprocess = 1.8f,
            FixedFps = 30,
            LocalCoords = true,
            Emitting = true,
            // 外部会覆盖成 +1（立绘前面），这里的值只是占位。
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

        // 循环 tween 被杀时停在任意一帧，不还原的话立绘会一直挂着淡红。
        if (GodotObject.IsInstanceValid(staticModel))
        {
            staticModel.Modulate = Colors.White;
        }
    }
}
