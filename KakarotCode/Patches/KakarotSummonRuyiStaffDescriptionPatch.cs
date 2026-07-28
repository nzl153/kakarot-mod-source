using System.Reflection;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Cards.Rare;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>升级版召唤如意棒使用独立描述键（引擎无内置 upgraded description key）。</summary>
[HarmonyPatch]
public static class KakarotSummonRuyiStaffDescriptionPatch
{
    private const string PlusDescriptionKey = "KAKAROTMOD-KAKAROT_SUMMON_RUYI_STAFF.description_plus";

    private static MethodBase TargetMethod()
    {
        var previewType = typeof(CardModel).GetNestedType("DescriptionPreviewType", BindingFlags.NonPublic);
        return AccessTools.Method(
            typeof(CardModel),
            nameof(CardModel.GetDescriptionForPile),
            [typeof(PileType), previewType!, typeof(Creature)]);
    }

    [HarmonyPostfix]
    public static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance is not KakarotSummonRuyiStaff staff || !staff.IsUpgraded || string.IsNullOrEmpty(__result))
        {
            return;
        }

        var translated = TranslationServer.Translate(PlusDescriptionKey);
        var text = translated.ToString();
        if (!string.IsNullOrEmpty(text) && text != PlusDescriptionKey)
        {
            __result = text;
        }
    }
}
