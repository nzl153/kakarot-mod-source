using HarmonyLib;
using KakarotMod.KakarotCode.Cards.Ancient;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace KakarotMod.KakarotCode.Patches;

// Kakarot's Dusty Tome always offers Father-Son Wave instead of a random Ancient card.
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
public static class KakarotDustyTomeAncientCardPatch
{
    [HarmonyPrefix]
    public static bool Prefix(DustyTome __instance, Player player)
    {
        if (player.Character is not KakarotCharacter)
        {
            return true;
        }

        __instance.AncientCard = ModelDb.Card<KakarotFatherSonWave>().Id;
        return false;
    }

    // Postfixes run even when another mod's prefix skips ours, so the offer text always matches the reward.
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

// Direct relic grants bypass the normal reward path.
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

[HarmonyPatch(typeof(ArchaicTooth), "TranscendenceUpgrades", MethodType.Getter)]
public static class KakarotArchaicToothUpgradePatch
{
    public static void Postfix(Dictionary<ModelId, CardModel> __result)
    {
        __result[ModelDb.Card<KakarotEyeBeam>().Id] = ModelDb.Card<KakarotFatherSonWave>();
    }
}
