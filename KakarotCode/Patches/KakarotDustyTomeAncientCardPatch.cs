using HarmonyLib;
using KakarotMod.KakarotCode.Cards.Ancient;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>
/// Force Dusty Tome ("魔封的宝典") to always grant Father-Son Wave for Kakarot.
/// </summary>
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
public static class KakarotDustyTomeAncientCardPatch
{
    [HarmonyPostfix]
    public static void Postfix(DustyTome __instance, Player player)
    {
        if (player.Character is not KakarotCharacter)
        {
            return;
        }

        __instance.AncientCard = ModelDb.Card<KakarotFatherSonWave>().Id;
    }
}

/// <summary>
/// Fallback for paths that obtain Dusty Tome directly (e.g. dev console),
/// ensuring pickup reward is still forced to Father-Son Wave for Kakarot.
/// </summary>
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.AfterObtained))]
public static class KakarotDustyTomeAfterObtainedPatch
{
    [HarmonyPrefix]
    public static void Prefix(DustyTome __instance)
    {
        if (__instance.Owner?.Character is not KakarotCharacter)
        {
            return;
        }

        __instance.AncientCard = ModelDb.Card<KakarotFatherSonWave>().Id;
    }
}
