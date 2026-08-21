using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace KakarotMod.KakarotCode.Characters;

// 共享气命中层。
//
// 原版 vfx_attack_blunt / vfx_attack_slash 是「金属打击」的视觉语言，
// 而卡卡罗特整套招式讲的是「气」。这层特效替掉它们，参数只有三个：
// 形状（拳/掌/斩）、色相、大小 —— 一次实现，所有普通攻击牌共用。
//
// 挂点是 AttackCommand.WithHitVfxNode：引擎每段命中回调一次，
// 是多段攻击唯一可靠的同步点。返回的节点被挂进目标的 VfxContainer。
public enum KiHitStyle
{
    // 拳：紧凑、爆点靠前，替 vfx_attack_blunt
    Fist,

    // 掌：气压铺开的双环，替能量类攻击的 vfx_attack_blunt
    Palm,

    // 斩：一道弧光加溅射火星，替 vfx_attack_slash
    Slash,
}

public static partial class KakarotCombatPresentation
{
    // 卡卡罗特的基准气色：橙金。传别的色相就是别的角色/招式的气。
    private static readonly Color KiDefaultColor = new(1f, 0.62f, 0.16f);

    // 逐段递增，只用来让连续命中的角度/高度错开，纯表现，不进同步状态。
    private static int _kiHitIndex;

    private static ImageTexture _kiGlowTex;
    private static ImageTexture _kiRingTex;
    private static ImageTexture _kiShardTex;
    private static ImageTexture _kiCrescentTex;

    // 卡里这样用：.WithHitVfxNode(KakarotCombatPresentation.KiHit(KiHitStyle.Fist))
    // 同时要把原本的 .WithHitFx("vfx/vfx_attack_blunt") 去掉 —— AttackCommand
    // 只在 HitVfx != null 时播原版特效，不写这一行就等于不播，不需要传空串。
    // 音效走 WithHitFx 的第二/第三个参数，和 vfx 是独立字段，删 vfx 不会静音。
    // facing：+1 是攻击者朝右打（卡卡罗特），-1 是朝左打（弗利萨）。
    // 火星和气浪都顺着出手方向溅，不跟着翻就会朝攻击者自己飞。
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
            // 核心比气色更接近白 —— 命中瞬间是「烧穿」，不是「染色」。
            Color core = ki.Lerp(new Color(1f, 1f, 1f), 0.72f);
            int index = _kiHitIndex++;

            var holder = new Node2D { Name = "KakarotKiHitFx", ZIndex = 31 };

            // 引擎挂到哪个父节点不保证，(0,0) 会落在画布原点（表现为特效跑到屏幕左上角）。
            // 入树后再设全局坐标，此时父链才成立。
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

    // 拳：核闪 + 压扁的冲击环 + 前向溅射的火星。整体 0.22 秒收干净，
    // 和原版 blunt 的 0.33 秒节奏对齐但更利落。
    private static void BuildKiFist(Node2D holder, float size, Color core, Color ki, int index, float facing)
    {
        float away = facing < 0f ? Mathf.Pi : 0f;
        SpawnKiFlash(holder, size * 0.80f, core, 0.18f);
        SpawnKiRing(holder, size * 1.18f, 0.62f, ki, 0.17f, 0f);

        // 命中来自左侧，火星整体往右前方溅。逐段轻微换向，避免连打时四下一模一样。
        float baseAngle = away + Mathf.DegToRad(index % 2 == 0 ? -8f : 10f);
        SpawnKiSparks(holder, 6, size * 0.62f, core, baseAngle, Mathf.DegToRad(62f), 96f, 168f, 0.20f);
        // 手写的 6 道火星撑形状，粒子撑密度。带重力，读作被打飞的碎屑而不是烟花。
        SpawnKiMotes(holder, 30, size, core, ki, baseAngle, Mathf.DegToRad(80f), 140f, 420f, 0.36f, 260f);
    }

    // 掌：横向拉开的气浪 + 一道细环 + 前推的火星。
    // 试过双同心环，读出来是两个光圈套着敌人，像传送门不像气压，已废弃。
    private static void BuildKiPalm(Node2D holder, float size, Color core, Color ki, float facing)
    {
        float away = facing < 0f ? Mathf.Pi : 0f;
        SpawnKiFlash(holder, size * 0.92f, core, 0.24f, 1.45f);
        SpawnKiRing(holder, size * 1.55f, 0.66f, ki, 0.20f, 0f);
        // 窄锥、飞得远：气是往前顶出去的，不是四面炸开。
        SpawnKiSparks(holder, 7, size * 0.55f, core, away, Mathf.DegToRad(26f), 140f, 250f, 0.26f);
        // 掌是往前顶，重力给小一点，粒子飘得远，读作气浪把尘埃推出去。
        SpawnKiMotes(holder, 40, size, core, ki, away, Mathf.DegToRad(30f), 240f, 620f, 0.42f, 90f);
    }

    // 斩：一道弧光切过去，再沿刀路溅几点火星。角度逐段翻面，读作来回连斩。
    private static void BuildKiSlash(Node2D holder, float size, Color core, Color ki, int index, float facing)
    {
        float angle = Mathf.DegToRad(index % 2 == 0 ? -34f : 26f);
        // 朝左打时整道弧镜像过去，否则刀路是从背后划出来的。
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
            // 弧光的实体在贴图 +X 半径 0.84 处，不补偿的话整道弧会挂在目标斜上方，
            // 看起来是从旁边划过而不是划过身上。往反方向推回来让弧压在胸口。
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
        // 火星沿刀路（垂直于弧光的展开方向）往两侧甩，不做整圈散射。
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

    // squash: 纵向压扁比例。1 是正圆，越小越像贴着命中面铺开而不是一个气泡。
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

    // 火星沿 baseAngle 为中心、halfSpread 为半角的扇形飞出。
    // 碎片贴图 u=0 宽 u=1 尖，Sprite2D 的 +X 就是 Rotation 指向，
    // 所以 Rotation 直接设成飞出方向，尖端自然朝外。
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

    // 细密溅射。数量级上手写 tween 到不了这里——30 颗就是 30 个 Sprite2D + 30 条 tween。
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
            // ⚠ 贴图是 32px，倍率 0.35 只有 11px——在加法混合的强闪底下根本看不见。
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

    // 四张贴图全程序生成、进程内只生成一次。
    // 命中特效一场战斗要播几十次，每次重算 128×128 的循环是白烧 CPU。
    // 白色生成、播放时用 Modulate 上色 —— 换气色不用重新生成贴图。
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

    // 弧光：以 radius 为半径的环带，只保留 +X 方向 ±halfArc 的一段，两端收尖。
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

                // 与 +X 的夹角；超出 halfArc 的部分直接为 0，靠近两端平滑收掉。
                float ang = Mathf.Abs(Mathf.Atan2(dy, dx));
                float arc = Mathf.Clamp(1f - ang / halfArc, 0f, 1f);
                arc = Mathf.Pow(arc, 0.55f);

                img.SetPixel(x, y, new Color(1f, 1f, 1f, band * arc));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
