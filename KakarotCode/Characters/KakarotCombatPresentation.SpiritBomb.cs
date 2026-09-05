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

// 元气弹。
//
// 唯一一张有「举手聚气 → 投掷」两段立绘切换的牌，所以姿势切换和光点聚拢
// 必须对齐同一条时间轴，慢了会出现手已经放下、光点还在飞的穿帮。
public static partial class KakarotCombatPresentation
{
    private const string SpiritBombCardId = "KAKAROTMOD-KAKAROT_SPIRIT_BOMB";

    private const string SpiritBombRaisePosePath = "res://Kakarot/Images/Charui/kakarot_combat_model_spirit_bomb_raise_pose.png";

    private const string SpiritBombThrowPosePath = "res://Kakarot/Images/Charui/kakarot_combat_model_spirit_bomb_throw_pose.png";

    // 元气弹演出：聚 → 成型 → 投掷 → 散。
    // 汇聚过程是这一招的辨识度所在（"把大家的元气分给我"），静态球体浪费了这张牌。
    private const int SpiritBombMoteCount = 22;

    private const float SpiritBombGatherSeconds = 1.05f;

    private const float SpiritBombMoteMinRadius = 620f;

    private const float SpiritBombMoteMaxRadius = 1250f;

    private static void PlaySpiritBombVfx(Player player, CardPlay cardPlay, Node visualsRoot, Sprite2D staticModel)
    {
        try
        {
            if (visualsRoot is not Node2D anchor || staticModel == null)
            {
                return;
            }

            var target = ResolvePrimaryBeamTarget(player, cardPlay);
            if (cardPlay.Target != null)
            {
                KakarotFormVisuals.RefreshFacingToTarget(player.Creature, cardPlay.Target);
            }

            Vector2 impactWorld;
            if (target == null || !TryGetCreatureChestWorld(target, out impactWorld))
            {
                float dirFallback = staticModel.FlipH ? -1f : 1f;
                impactWorld = staticModel.GlobalPosition + new Vector2(420f * dirFallback, -40f);
            }

            // 球在举手的悟空头顶成型，而不是敌人头顶。
            var gatherWorld = staticModel.GlobalPosition + new Vector2(0f, -300f);

            var holder = new Node2D { Name = "KakarotSpiritBombFx", ZIndex = 30 };
            anchor.AddChild(holder);
            holder.GlobalScale = Vector2.One;
            holder.GlobalPosition = gatherWorld;

            var glow = CreateRadialGlowSprite(0.55f, 0f);
            glow.Scale = new Vector2(0.06f, 0.06f);
            holder.AddChild(glow);

            var core = CreateRadialGlowSprite(0.95f, 0f);
            core.Scale = new Vector2(0.03f, 0.03f);
            holder.AddChild(core);

            SpawnSpiritBombMotes(anchor, gatherWorld);

            // 举手贯穿「汇聚+成型」，投掷瞬间换成下挥，整段结束才复原成站姿。
            const float spiritBombPoseSeconds =
                SpiritBombGatherSeconds + 0.18f + 0.26f + 0.45f;
            TrySwapToPose(player.Creature, staticModel, SpiritBombRaisePosePath, spiritBombPoseSeconds);

            var tween = holder.CreateTween();
            tween.SetParallel(false);
            tween.SetPauseMode(Tween.TweenPauseMode.Process);

            // ① 汇聚：球随着元气飞入而长大
            tween.TweenProperty(glow, "scale", new Vector2(3.0f, 3.0f), SpiritBombGatherSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(core, "scale", new Vector2(1.7f, 1.7f), SpiritBombGatherSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

            // ② 成型停顿 + 预备动作：身体先后拉蓄力，再甩出去
            tween.TweenCallback(Callable.From(() => PlaySpiritBombThrowMotion(player.Creature, staticModel)));
            tween.TweenInterval(0.18);

            // ③ 投掷
            tween.TweenProperty(holder, "global_position", impactWorld, 0.26)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            // ④ 扩散
            tween.TweenCallback(Callable.From(() =>
            {
                // 音效跟着落点走，不再跟出拳那一下绑定。
                PlaySfx(visualsRoot, SpiritBombSfxNodeName, HitSfxPath, -2f, true);
                PlayImpactFeedback(ShakeStrength.Strong, ShakeDuration.Normal, hitStop: true);
                SpawnRadialBurst(anchor, impactWorld, 5.0f, 0.45f);
            }));
            tween.TweenInterval(0.45);
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
            GD.PrintErr($"[Kakarot][Vfx] Spirit bomb failed: {ex}");
        }
    }

    // 甩出去的力量感：后拉蓄力 → 前冲甩出 → 回稳。贴图切换卡在"甩出"那一帧，
    // 这样人眼看到的是一个连续动作，而不是一次生硬的换图。
    private static void PlaySpiritBombThrowMotion(Creature creature, Sprite2D staticModel)
    {
        try
        {
            if (creature == null || !GodotObject.IsInstanceValid(staticModel))
            {
                return;
            }

            var rest = KakarotFormVisuals.GetRestTransform(creature);
            float dir = staticModel.FlipH ? -1f : 1f;

            var motion = staticModel.CreateTween();
            motion.SetParallel(false);

            // 后拉：跟成型停顿同长，玩家看到球定住的同时人在蓄力
            motion.TweenProperty(staticModel, "position", rest.Pos + new Vector2(-14f * dir, 0f), 0.18)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);

            motion.TweenCallback(Callable.From(() =>
            {
                var throwTex = ResourceLoader.Load<Texture2D>(SpiritBombThrowPosePath);
                if (throwTex != null && GodotObject.IsInstanceValid(staticModel))
                {
                    // 只换贴图，不动复原簿记——原始站姿已由 TrySwapToPose 记下。
                    CrossfadeTexture(staticModel, throwTex, PoseCrossfadeSeconds);
                }
            }));

            // 甩出：快、带一点过冲
            motion.TweenProperty(staticModel, "position", rest.Pos + new Vector2(30f * dir, 0f), 0.09)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);

            // 回稳：慢，收住余韵
            motion.TweenProperty(staticModel, "position", rest.Pos, 0.34)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot][Vfx] Spirit bomb throw motion failed: {ex.Message}");
        }
    }

    private static void SpawnSpiritBombMotes(Node2D anchor, Vector2 gatherWorld)
    {
        for (int i = 0; i < SpiritBombMoteCount; i++)
        {
            float angle = Mathf.Pi * (0.08f + 0.84f * GD.Randf());
            float radius = Mathf.Lerp(SpiritBombMoteMinRadius, SpiritBombMoteMaxRadius, GD.Randf());
            var from = gatherWorld + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            var mote = CreateRadialGlowSprite(0.9f, 0f);
            float size = Mathf.Lerp(0.035f, 0.075f, GD.Randf());
            mote.Scale = new Vector2(size, size);
            mote.Modulate = new Color(1f, 1f, 1f, 0f);
            mote.ZIndex = 29;
            anchor.AddChild(mote);
            mote.GlobalPosition = from;

            float delay = GD.Randf() * (SpiritBombGatherSeconds * 0.55f);
            float travel = Mathf.Lerp(0.42f, 0.62f, GD.Randf());

            var moteTween = mote.CreateTween();
            moteTween.SetParallel(false);
            moteTween.TweenInterval(delay);
            moteTween.TweenProperty(mote, "modulate:a", 1f, 0.10);
            moteTween.Parallel().TweenProperty(mote, "global_position", gatherWorld, travel)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            moteTween.TweenProperty(mote, "modulate:a", 0f, 0.08);
            moteTween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(mote))
                {
                    mote.QueueFree();
                }
            }));
        }
    }
}
