using HarmonyLib;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>赋关键字后尝试强制卡面刷新（解决野性等运行时关键字不刷新黄字行）。</summary>
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
