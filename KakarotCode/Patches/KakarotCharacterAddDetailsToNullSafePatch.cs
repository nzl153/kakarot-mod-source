#nullable disable
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

// Skip detail injection when an event option has no localized description.
[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.AddDetailsTo))]
public static class KakarotCharacterAddDetailsToNullSafePatch
{
    public static bool Prefix(CharacterModel __instance, LocString str)
    {
        if (__instance == null || LocString.IsNullOrWhitespace(str))
        {
            return false;
        }

        return true;
    }
}
