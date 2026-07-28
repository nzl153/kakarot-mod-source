#nullable enable
using System.Linq;
using HarmonyLib;
using KakarotMod.KakarotCode.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoom))]
public static class FriezaChallengeEntryPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref AbstractRoom room)
    {
        if (room is not EventRoom nextEvent || nextEvent.CanonicalEvent is not TheArchitect)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null ||
            runState.CurrentRoom is EventRoom { CanonicalEvent: FriezaChallenge } ||
            !runState.Players.Any(static player => player.Character is KakarotCharacter))
        {
            return;
        }

        room = new EventRoom(ModelDb.Event<FriezaChallenge>());
    }
}
