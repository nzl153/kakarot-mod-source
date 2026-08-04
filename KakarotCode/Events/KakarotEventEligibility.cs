using System;
using System.Linq;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Events;

internal static class KakarotEventEligibility
{
    internal static bool HasKakarot(IRunState runState)
    {
        return runState.Players.Any(static player =>
        {
            if (player.Character is KakarotCharacter)
            {
                return true;
            }

            string entry = player.Character?.Id.Entry;
            return !string.IsNullOrWhiteSpace(entry)
                && entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
        });
    }
}
