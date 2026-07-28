using System.Linq;
using HarmonyLib;
using KakarotMod.KakarotCode.Events;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Patches;
// Add Kakarot events only after room generation and only for Kakarot runs.
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
public static class KakarotTournamentCafeteriaPoolPatch
{
    private static void Postfix(RunManager __instance)
    {
        var state = Traverse.Create(__instance).Property<RunState>("State").Value;
        if (state == null || !state.Players.Any(static p => p.Character is KakarotCharacter))
        {
            return;
        }

        EventModel[] extra =
        [
            ModelDb.Event<KakarotTournamentCafeteria>(),
            ModelDb.Event<KakarotIncredibleAdventure>(),
            ModelDb.Event<KakarotKorinTowerTrial>(),
        ];

        foreach (ActModel act in state.Acts)
        {
            var rooms = Traverse.Create(act).Field<RoomSet>("_rooms").Value;
            if (rooms == null)
            {
                continue;
            }

            foreach (EventModel ev in extra)
            {
                if (!rooms.events.Any(e => e.Id == ev.Id))
                {
                    rooms.events.Add(ev);
                }
            }
        }
    }
}
