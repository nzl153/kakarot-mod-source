using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace KakarotMod.KakarotCode.Characters;

// 龟波的打击感：光束接触时轻震，收束时波头炸开再震一下。
public static partial class KakarotCombatPresentation
{
    private const string DaimaBreakthroughKamehamehaId = "KAKAROTMOD-KAKAROT_DAIMA_BREAKTHROUGH_KAMEHAMEHA";

    private static readonly Color DaimaBreakthroughBurstColor = new(1.0f, 0.75f, 0.2f);

    private static (Color Beam, Color Core) ResolveKamehamehaPalette(string cardEntryId)
        => KamehamehaBeamPalette.TryGetValue(cardEntryId ?? string.Empty, out var palette)
            ? palette
            : (KamehamehaBeamColorDefault, KamehamehaCoreColorDefault);

    private static void ScheduleKamehamehaBeamFeedback(
        Player player, CardPlay cardPlay, Node2D anchor, Vector2 originWorld, Vector2 endWorld)
    {
        var tree = anchor.GetTree();
        if (tree == null)
        {
            return;
        }

        var entry = cardPlay.Card?.Id.Entry;
        var palette = ResolveKamehamehaPalette(entry);
        if (string.Equals(entry, DaimaBreakthroughKamehamehaId, StringComparison.OrdinalIgnoreCase))
        {
            palette = (DaimaBreakthroughBurstColor, palette.Core);
        }

        tree.CreateTimer(KamehamehaShaderBeamGrowSeconds).Timeout += () =>
            PlayImpactFeedback(ShakeStrength.Weak, ShakeDuration.Normal, hitStop: false);

        tree.CreateTimer(KamehamehaShaderBeamGrowSeconds + KamehamehaShaderBeamHoldSeconds).Timeout += () =>
        {
            try
            {
                if (!GodotObject.IsInstanceValid(anchor))
                {
                    return;
                }

                SpawnKamehamehaEndBurst(anchor, ResolveKamehamehaBurstWorld(player, cardPlay, originWorld, endWorld),
                    (endWorld - originWorld).Angle(), palette.Beam, palette.Core);
                PlayImpactFeedback(ShakeStrength.Medium, ShakeDuration.Short, hitStop: false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[Kakarot][Vfx] Kamehameha end burst failed: {ex.Message}");
            }
        };
    }

    // 只炸一处，落在光束线上：取最远存活目标的胸口投影到光束上，没目标就用光束终点前一段。
    private static Vector2 ResolveKamehamehaBurstWorld(Player player, CardPlay cardPlay, Vector2 originWorld, Vector2 endWorld)
    {
        var dir = (endWorld - originWorld).Normalized();
        float reach = Math.Max(originWorld.DistanceTo(endWorld) - KamehamehaBeamOvershoot, 0f);

        var chests = ResolveKamehamehaImpactTargets(player, cardPlay)
            .Select(c => TryGetCreatureChestWorld(c, out var w) ? (Vector2?)w : null)
            .Where(w => w.HasValue)
            .Select(w => (w.Value - originWorld).Dot(dir))
            .ToList();
        if (chests.Count > 0)
        {
            reach = chests.Max();
        }

        return originWorld + dir * reach;
    }

    private static void SpawnKamehamehaEndBurst(Node2D anchor, Vector2 world, float beamAngle, Color beam, Color core)
    {
        var holder = new Node2D { Name = "KakarotKamehamehaEndBurst", ZIndex = 32 };
        anchor.AddChild(holder);
        holder.GlobalPosition = world;
        holder.GlobalScale = Vector2.One;

        SpawnKiFlash(holder, 1.3f, core, 0.3f, 1.2f);
        SpawnKiRing(holder, 2.2f, 0.7f, beam, 0.34f, 0f);
        SpawnKiRing(holder, 1.4f, 0.7f, core, 0.26f, 0.06f);
        SpawnKiSparks(holder, 12, 0.6f, core, beamAngle, Mathf.DegToRad(70f), 200f, 380f, 0.36f);
        SpawnKiMotes(holder, 50, 1f, core, beam, beamAngle, Mathf.DegToRad(80f), 260f, 680f, 0.5f, 120f);

        var life = holder.CreateTween();
        life.TweenInterval(0.9);
        life.TweenCallback(Callable.From(() => FreeIfValid(holder)));
    }
}
