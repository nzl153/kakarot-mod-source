using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace KakarotMod.KakarotCode.Characters;

// 共享气命中特效，统一拳、掌、斩三种形状与配色。
// 挂接 AttackCommand.WithHitVfxNode，可在多段攻击每次命中时生成独立特效。
public enum KiHitStyle
{
    Fist,
    Palm,
    Slash,
}

public static partial class KakarotCombatPresentation
{
    // 卡卡罗特的基准气色；其他招式可传入独立色相。
    private static readonly Color KiDefaultColor = new(1f, 0.62f, 0.16f);

    // 仅用于让连续命中的角度和高度错开，不参与同步状态。
    private static int _kiHitIndex;

    private static ImageTexture _kiGlowTex;
    private static ImageTexture _kiRingTex;
    private static ImageTexture _kiShardTex;
    private static ImageTexture _kiCrescentTex;

    // WithHitVfxNode 负责视觉；原版 WithHitFx 的视觉字段需移除，音效参数可保留。
    // facing 控制溅射方向，+1 向右，-1 向左。
    public static Func<Creature, Node2D> KiHit(
        KiHitStyle style, float size = 1f, Color? ki = null, float facing = 1f)
    {
        Color color = ki ?? KiDefaultColor;
        return enemy => BuildKiHit(enemy, style, size, color, facing);
    }

    private static Node2D BuildKiHit(Creature enemy, KiHitStyle style, float size, Color ki, float facing)
    {
        try
        {
            // 核心色向白色偏移以突出命中瞬间。
            Color core = ki.Lerp(new Color(1f, 1f, 1f), 0.72f);
            int index = _kiHitIndex++;

            var holder = new Node2D { Name = "KakarotKiHitFx", ZIndex = 31 };

            // 入树后设置全局坐标，确保父链已建立。
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

            switch (style)
            {
                case KiHitStyle.Palm:
                    BuildKiPalm(holder, size, core, ki, facing);
                    break;
                case KiHitStyle.Slash:
                    BuildKiSlash(holder, size, core, ki, index, facing);
                    break;
                default:
                    BuildKiFist(holder, size, core, ki, index, facing);
                    break;
            }

            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Ki hit vfx failed: {ex}");
            return null;
        }
    }

    // 拳：核闪、压扁冲击环和前向溅射。
    private static void BuildKiFist(Node2D holder, float size, Color core, Color ki, int index, float facing)
    {
        float away = facing < 0f ? Mathf.Pi : 0f;
        SpawnKiFlash(holder, size * 0.80f, core, 0.18f);
        SpawnKiRing(holder, size * 1.18f, 0.62f, ki, 0.17f, 0f);

        // 连续命中轻微错开方向，避免完全重叠。
        float baseAngle = away + Mathf.DegToRad(index % 2 == 0 ? -8f : 10f);
        SpawnKiSparks(holder, 6, size * 0.62f, core, baseAngle, Mathf.DegToRad(62f), 96f, 168f, 0.20f);
        // 粒子补充细密碎屑与重力感。
        SpawnKiMotes(holder, 30, size, core, ki, baseAngle, Mathf.DegToRad(80f), 140f, 420f, 0.36f, 260f);
    }

    // 掌：横向气浪、细环和前向溅射。
    private static void BuildKiPalm(Node2D holder, float size, Color core, Color ki, float facing)
    {
        float away = facing < 0f ? Mathf.Pi : 0f;
        SpawnKiFlash(holder, size * 0.92f, core, 0.24f, 1.45f);
        SpawnKiRing(holder, size * 1.55f, 0.66f, ki, 0.20f, 0f);
        // 窄锥使溅射主要沿攻击方向传播。
        SpawnKiSparks(holder, 7, size * 0.55f, core, away, Mathf.DegToRad(26f), 140f, 250f, 0.26f);
        // 较小重力让掌击粒子保持更远的前向位移。
        SpawnKiMotes(holder, 40, size, core, ki, away, Mathf.DegToRad(30f), 240f, 620f, 0.42f, 90f);
    }

    // 斩：弧光与沿刀路溅射；连续命中交替角度。
    private static void BuildKiSlash(Node2D holder, float size, Color core, Color ki, int index, float facing)
    {
        float angle = Mathf.DegToRad(index % 2 == 0 ? -34f : 26f);
        // 朝左攻击时镜像弧光方向。
        if (facing < 0f)
        {
            angle = Mathf.Pi - angle;
        }

        var crescent = new Sprite2D
        {
            Texture = GetKiCrescentTexture(),
            Centered = true,
            Material = CreateAdditiveMaterial(),
            Modulate = new Color(core.R, core.G, core.B, 0f),
            Rotation = angle,
            Scale = new Vector2(size * 0.62f, size * 1.00f),
            // 根据贴图实体偏移将弧光中心压回目标胸口。
            Position = new Vector2(-Mathf.Cos(angle), -Mathf.Sin(angle)) * (size * 62f),
            ZIndex = 1,
        };
        holder.AddChild(crescent);

        var tw = crescent.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(crescent, "modulate:a", 1f, 0.05);
        tw.TweenProperty(crescent, "scale", new Vector2(size * 1.70f, size * 1.28f), 0.14)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.Chain().TweenProperty(crescent, "modulate:a", 0f, 0.09)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(crescent)));

        SpawnKiFlash(holder, size * 0.58f, core, 0.16f);
        // 火星沿刀路附近向外甩出。
        SpawnKiSparks(holder, 5, size * 0.5f, core, angle, Mathf.DegToRad(28f), 110f, 190f, 0.20f);
        SpawnKiMotes(holder, 24, size, core, ki, angle, Mathf.DegToRad(34f), 200f, 500f, 0.32f, 220f);
    }

    private static void SpawnKiFlash(Node2D holder, float peak, Color core, float life, float stretch = 1f)
    {
        var flash = new Sprite2D
        {
            Texture = GetKiGlowTexture(),
            Centered = true,
            Material = CreateAdditiveMaterial(),
            Modulate = new Color(core.R, core.G, core.B, 1f),
            Scale = new Vector2(peak * 0.3f * stretch, peak * 0.3f),
            ZIndex = 2,
        };
        holder.AddChild(flash);

        var tw = flash.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(flash, "scale", new Vector2(peak * stretch, peak), life * 0.45f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(flash, "modulate:a", 0f, life)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(flash)));
    }

    // squash 控制纵向压扁比例，较小值更接近命中面扩散。
    private static void SpawnKiRing(
        Node2D holder, float peak, float squash, Color ki, float life, float delay)
    {
        var ring = new Sprite2D
        {
            Texture = GetKiRingTexture(),
            Centered = true,
            Material = CreateAdditiveMaterial(),
            Modulate = new Color(ki.R, ki.G, ki.B, 0.95f),
            Scale = new Vector2(peak * 0.22f, peak * 0.22f * squash),
            ZIndex = 0,
        };
        holder.AddChild(ring);

        var tw = ring.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ring, "scale", new Vector2(peak, peak * squash), life)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out)
            .SetDelay(delay);
        tw.TweenProperty(ring, "modulate:a", 0f, life)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In)
            .SetDelay(delay);
        tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(ring)));
    }

    // 火星沿 baseAngle 为中心、halfSpread 为半角的扇形飞出，贴图尖端与运动方向一致。
    private static void SpawnKiSparks(
        Node2D holder, int count, float scale, Color core,
        float baseAngle, float halfSpread, float minSpeed, float maxSpeed, float life)
    {
        var tex = GetKiShardTexture();

        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float angle = baseAngle + Mathf.Lerp(-halfSpread, halfSpread, t) + (GD.Randf() - 0.5f) * 0.18f;
            float speed = Mathf.Lerp(minSpeed, maxSpeed, GD.Randf());
            float sz = scale * Mathf.Lerp(0.7f, 1.25f, GD.Randf());

            var spark = new Sprite2D
            {
                Texture = tex,
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(core.R, core.G, core.B, 1f),
                Rotation = angle,
                Scale = new Vector2(sz, sz * 0.8f),
                ZIndex = 1,
            };
            holder.AddChild(spark);

            var to = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.78f) * speed;

            var tw = spark.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(spark, "position", to, life)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(spark, "modulate:a", 0f, life)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(spark)));
        }
    }

    // 使用粒子发射器生成细密溅射。
    private static void SpawnKiMotes(
        Node2D holder, int amount, float size, Color core, Color ki,
        float baseAngle, float halfSpread, float speedMin, float speedMax, float life, float gravityY)
    {
        holder.AddChild(CreateBurst(
            GetParticleDotTexture(),
            core,
            ki,
            amount,
            life,
            speedMin,
            speedMax,
            // 32px 贴图需保持足够缩放，避免被命中闪光淹没。
            size * 0.9f,
            size * 1.8f,
            baseAngle,
            halfSpread,
            new Vector2(0f, gravityY),
            0f));
    }

    internal static void FreeIfValid(Node node)
    {
        if (GodotObject.IsInstanceValid(node))
        {
            node.QueueFree();
        }
    }

    // 程序纹理按进程缓存，避免战斗中重复生成。
    private static ImageTexture GetKiGlowTexture()
    {
        return _kiGlowTex ??= CreateRadialGlowTexture(
            128, new Color(1f, 1f, 1f), new Color(1f, 1f, 1f));
    }

    private static ImageTexture GetKiRingTexture()
    {
        return _kiRingTex ??= CreateRingTexture(
            128, 0.052f, new Color(1f, 1f, 1f), new Color(1f, 1f, 1f));
    }

    private static ImageTexture GetKiShardTexture()
    {
        return _kiShardTex ??= CreateShardTexture(
            48, 12, new Color(1f, 1f, 1f), new Color(1f, 1f, 1f));
    }

    private static ImageTexture GetKiCrescentTexture()
    {
        return _kiCrescentTex ??= CreateCrescentTexture(128, 0.84f, 0.045f, 0.75f);
    }

    // 生成以 radius 为半径、仅保留 +X 方向指定弧度范围的弧光贴图。
    private static ImageTexture CreateCrescentTexture(int size, float radius, float thickness, float halfArc)
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

                if (d >= 1f || d < 0.02f)
                {
                    img.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }

                float band = Mathf.Exp(-Mathf.Pow((d - radius) / thickness, 2f));

                float ang = Mathf.Abs(Mathf.Atan2(dy, dx));
                float arc = Mathf.Clamp(1f - ang / halfArc, 0f, 1f);
                arc = Mathf.Pow(arc, 0.55f);

                img.SetPixel(x, y, new Color(1f, 1f, 1f, band * arc));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
