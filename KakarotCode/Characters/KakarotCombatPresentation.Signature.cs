using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace KakarotMod.KakarotCode.Characters;

// 招牌招式的专属特效。
//
// 这几张原本挂的是共享气命中层——那一层的定位是「让没有专属动画的牌不难看」，
// 不是「让招牌招式好看」。自爆是自己炸自己，天贯拳是往上贯穿，八臂拳是同时多手，
// 这三件事通用的拳/掌/斩都表达不了。
public static partial class KakarotCombatPresentation
{
    private const string SelfDestructCardId = "KAKAROTMOD-KAKAROT_SELF_DESTRUCT";

    // 赛亚人的气是白金色，和弗利萨的紫、破坏的紫分得开。
    private static readonly Color SelfDestructCore = new(1f, 0.94f, 0.68f);
    private static readonly Color SelfDestructEdge = new(1f, 0.52f, 0.10f);

    // ── 自爆 ──────────────────────────────────────────────────
    // 炸点在自己身上，不在敌人身上。这是全场唯一一张「以自己为圆心」的攻击牌。
    private static void TryPlaySelfDestructVfx(Player player, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            Vector2 center = staticModel.ToGlobal(staticModel.GetRect().GetCenter());

            var holder = new Node2D { Name = "KakarotSelfDestructFx", ZIndex = 30 };
            anchor.AddChild(holder);
            holder.GlobalPosition = center;
            // 挂在角色 visuals 下必须归一，否则世界尺寸被父级缩放吃掉。
            holder.GlobalScale = Vector2.One;

            var ball = new Sprite2D
            {
                // 内芯降饱和：加法混合下大面积用纯亮色会直接烧成白，读不出是火。
                Texture = CreateRadialGlowTexture(128, new Color(1f, 0.80f, 0.42f), SelfDestructEdge),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.2f, 0.2f),
                ZIndex = 1,
            };
            holder.AddChild(ball);

            var flash = new Sprite2D
            {
                Texture = CreateRadialGlowTexture(128, new Color(1f, 0.98f, 0.90f), SelfDestructCore),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.3f, 0.3f),
                ZIndex = 2,
            };
            holder.AddChild(flash);

            // 起爆前把气往身上收，读作「在攒」而不是「突然炸了」。
            holder.AddChild(CreateStream(
                GetParticleDotTexture(), SelfDestructCore, SelfDestructEdge,
                amount: 90, lifetime: 0.34f, duration: 0.22f,
                radialVelocity: -520f, emissionRadius: 230f,
                scaleMin: 0.5f, scaleMax: 1.4f,
                gravity: Vector2.Zero));

            var tween = holder.CreateTween();
            tween.TweenProperty(ball, "modulate:a", 0.9f, 0.12);
            tween.Parallel().TweenProperty(ball, "scale", new Vector2(1.1f, 1.1f), 0.20)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            // 起爆前一缩：没有这 0.06 秒，炸开会读成「体积一路变大」而不是「爆」。
            tween.TweenProperty(ball, "scale", new Vector2(0.72f, 0.72f), 0.06)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            tween.TweenCallback(Callable.From(() =>
            {
                PlayImpactFeedback(ShakeStrength.Strong, ShakeDuration.Normal, hitStop: true);
                // 8.6 倍的环直接跑出画面了，收到 5.6。
                SpawnBlastRing(holder, SelfDestructCore, SelfDestructEdge, 5.6f, 0.44f, 0.42f);
                SpawnBlastShards(holder, 26, SelfDestructCore, SelfDestructEdge, 1.4f, 320f, 820f, 0.42f);
                holder.AddChild(CreateBurst(
                    GetParticleDotTexture(), SelfDestructCore, SelfDestructEdge,
                    amount: 130, lifetime: 0.9f,
                    speedMin: 240f, speedMax: 880f,
                    scaleMin: 0.7f, scaleMax: 2.2f,
                    baseAngle: 0f, halfSpread: Mathf.Pi,
                    gravity: new Vector2(0f, 220f), damping: 0f));
            }));

            tween.TweenProperty(flash, "modulate:a", 1f, 0.05);
            tween.Parallel().TweenProperty(flash, "scale", new Vector2(4.4f, 4.4f), 0.26)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(ball, "scale", new Vector2(3.2f, 3.2f), 0.32)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            // 边胀边变稀薄，否则大面积加法混合糊成一团白雾。
            tween.Parallel().TweenProperty(ball, "modulate:a", 0.45f, 0.26);

            tween.Chain().TweenProperty(flash, "modulate:a", 0f, 0.30)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(ball, "modulate:a", 0f, 0.34)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tween.Chain().TweenCallback(Callable.From(() => FreeIfValid(holder)));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Self destruct failed: {ex}");
        }
    }

    // ── 天贯拳 ────────────────────────────────────────────────
    // 「贯穿上天」——力的方向是垂直向上，用通用的横向溅射读不出来。
    public static Node2D CreateHeavenPiercingHitVfx(Creature enemy)
    {
        try
        {
            var holder = new Node2D { Name = "KakarotHeavenPiercingFx", ZIndex = 31 };
            if (TryGetCreatureChestWorld(enemy, out Vector2 chest))
            {
                holder.TreeEntered += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.GlobalPosition = chest;
                    }
                };
            }

            Color core = KiDefaultColor.Lerp(new Color(1f, 1f, 1f), 0.74f);

            // 竖直光柱：把辉光横向压扁、纵向拉长。
            var pillar = new Sprite2D
            {
                Texture = CreateRadialGlowTexture(128, core, KiDefaultColor),
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Modulate = new Color(1f, 1f, 1f, 0f),
                Scale = new Vector2(0.55f, 0.4f),
                Position = new Vector2(0f, 30f),
                ZIndex = 1,
            };
            holder.AddChild(pillar);

            var tw = pillar.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(pillar, "modulate:a", 1f, 0.05);
            // 只往上长，所以位置和高度一起动，底端留在原地。
            tw.TweenProperty(pillar, "scale", new Vector2(0.62f, 4.4f), 0.20)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(pillar, "position", new Vector2(0f, -190f), 0.20)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.Chain().TweenProperty(pillar, "modulate:a", 0f, 0.16)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(pillar)));

            // 地面一圈薄环，给光柱一个「从这里冲出去」的落点。
            SpawnBlastRing(holder, core, KiDefaultColor, 1.5f, 0.34f, 0.22f);

            // 碎片和粒子都往上，形成贯穿感。
            SpawnKiSparks(holder, 6, 0.62f, core,
                -Mathf.Pi * 0.5f, Mathf.DegToRad(24f), 200f, 430f, 0.24f);
            holder.AddChild(CreateBurst(
                GetParticleDotTexture(), core, KiDefaultColor,
                amount: 40, lifetime: 0.5f,
                speedMin: 280f, speedMax: 640f,
                scaleMin: 0.6f, scaleMax: 1.6f,
                baseAngle: -Mathf.Pi * 0.5f, halfSpread: Mathf.DegToRad(20f),
                gravity: new Vector2(0f, 260f), damping: 0f));

            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Heaven piercing failed: {ex}");
            return null;
        }
    }

    // ── 八臂拳 ────────────────────────────────────────────────
    // 「同时八只手」——重点是同一瞬间多个落点，不是连续打很多下。
    // 通用命中层每段只有一个爆点，读起来和普通连击没区别。
    private static int _eightArmedHitIndex;

    public static Node2D CreateEightArmedHitVfx(Creature enemy)
    {
        try
        {
            int index = _eightArmedHitIndex++;

            var holder = new Node2D { Name = "KakarotEightArmedFx", ZIndex = 31 };
            if (TryGetCreatureChestWorld(enemy, out Vector2 chest))
            {
                holder.TreeEntered += () =>
                {
                    if (GodotObject.IsInstanceValid(holder))
                    {
                        holder.GlobalPosition = chest;
                    }
                };
            }

            Color core = KiDefaultColor.Lerp(new Color(1f, 1f, 1f), 0.72f);

            // 一段命中同时开三个落点，沿一段弧排开，逐段换边。
            const int fists = 3;
            float side = index % 2 == 0 ? 1f : -1f;

            for (int i = 0; i < fists; i++)
            {
                float t = fists <= 1 ? 0.5f : i / (float)(fists - 1);
                float spread = Mathf.Lerp(-56f, 56f, t) * side;
                // 半径 18 的横向分量太小，三个落点几乎叠成一条竖线，
                // 读不出「多只手从不同方向来」。拉开成一段真正的弧。
                var at = new Vector2(
                    Mathf.Cos(Mathf.DegToRad(spread)) * 52f - 34f,
                    Mathf.Sin(Mathf.DegToRad(spread)) * 66f);

                var hit = new Sprite2D
                {
                    Texture = GetKiGlowTexture(),
                    Centered = true,
                    Material = CreateAdditiveMaterial(),
                    Modulate = new Color(core.R, core.G, core.B, 1f),
                    Scale = new Vector2(0.16f, 0.16f),
                    Position = at,
                    ZIndex = 2,
                };
                holder.AddChild(hit);

                // 3ms 级的错开：读作「几乎同时」，不是「连着打三下」。
                float delay = i * 0.028f;
                var tw = hit.CreateTween();
                tw.TweenInterval(delay);
                tw.TweenProperty(hit, "scale", new Vector2(0.95f, 0.95f), 0.07)
                    .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(hit, "modulate:a", 0f, 0.16)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                tw.TweenCallback(Callable.From(() => FreeIfValid(hit)));

                SpawnKiSparks(holder, 3, 0.42f, core,
                    Mathf.DegToRad(spread * 0.5f), Mathf.DegToRad(30f), 90f, 190f, 0.18f);
            }

            holder.AddChild(CreateBurst(
                GetParticleDotTexture(), core, KiDefaultColor,
                amount: 26, lifetime: 0.32f,
                speedMin: 130f, speedMax: 400f,
                scaleMin: 0.5f, scaleMax: 1.3f,
                baseAngle: 0f, halfSpread: Mathf.DegToRad(90f),
                gravity: new Vector2(0f, 300f), damping: 0f));

            return holder;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Eight armed failed: {ex}");
            return null;
        }
    }

    // ── 共用图元 ──────────────────────────────────────────────
    private static void SpawnBlastRing(
        Node2D holder, Color core, Color edge, float peak, float squash, float life)
    {
        var ring = new Sprite2D
        {
            Texture = CreateRingTexture(160, 0.045f, core, edge),
            Centered = true,
            Material = CreateAdditiveMaterial(),
            Modulate = new Color(1f, 1f, 1f, 0.72f),
            Scale = new Vector2(peak * 0.16f, peak * 0.16f * squash),
            ZIndex = 3,
        };
        holder.AddChild(ring);

        var tw = ring.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ring, "scale", new Vector2(peak, peak * squash), life)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ring, "modulate:a", 0f, life)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(ring)));
    }

    private static void SpawnBlastShards(
        Node2D holder, int count, Color core, Color edge,
        float size, float speedMin, float speedMax, float life)
    {
        var tex = CreateShardTexture(64, 16, core, edge);

        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Tau * (i / (float)count) + GD.Randf() * 0.28f;
            float speed = Mathf.Lerp(speedMin, speedMax, GD.Randf());
            float scale = size * Mathf.Lerp(0.6f, 1.4f, GD.Randf());

            var shard = new Sprite2D
            {
                Texture = tex,
                Centered = true,
                Material = CreateAdditiveMaterial(),
                Rotation = angle,
                Scale = new Vector2(scale * 1.2f, scale * 0.9f),
                ZIndex = 3,
            };
            holder.AddChild(shard);

            var to = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.78f) * speed;

            var tw = shard.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(shard, "position", to, life)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(shard, "modulate:a", 0f, life)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.Chain().TweenCallback(Callable.From(() => FreeIfValid(shard)));
        }
    }
}
