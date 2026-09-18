using HarmonyLib;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;
// Runtime keyword changes do not automatically refresh the card-face text.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.AddKeyword), typeof(CardKeyword))]
public static class KakarotWildAddKeywordPatch
{
    public static void Postfix(CardModel __instance, CardKeyword keyword)
    {
        if (keyword != KakarotWildKeyword.Wild)
        {
            return;
        }

        KakarotWildCardPresentationBump.TryBump(__instance);
    }
}
