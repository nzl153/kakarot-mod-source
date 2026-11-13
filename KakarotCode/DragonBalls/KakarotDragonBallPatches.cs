using System.Reflection;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;

namespace KakarotMod.KakarotCode.DragonBalls;

[HarmonyPatch]
public static class KakarotDragonBallNewRunPatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer)),
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer)),
    ];

    public static void Postfix(RunState state)
    {
        foreach (var player in state.Players)
        {
            if (player.Character is not KakarotCharacter)
            {
                continue;
            }

            var relic = ModelDb.Relic<KakarotSevenDragonBalls>().ToMutable();
            relic.FloorAddedToDeck = 1;
            player.AddRelicInternal(relic);
            SaveManager.Instance.MarkRelicAsSeen(relic);
        }
    }
}

[HarmonyPatch(typeof(NClickableControl), nameof(NClickableControl._GuiInput))]
public static class KakarotDragonBallClickPatch
{
    public static void Postfix(NClickableControl __instance, InputEvent inputEvent)
    {
        if (__instance is not NRelicInventoryHolder holder
            || inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
            || holder.Relic?.Model is not KakarotSevenDragonBalls relic
            || !LocalContext.IsMe(relic.Owner) || !relic.CanSummon || relic.RequestPending)
        {
            return;
        }

        relic.RequestPending = true;
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new KakarotSummonShenronAction(relic.Owner));
    }
}
