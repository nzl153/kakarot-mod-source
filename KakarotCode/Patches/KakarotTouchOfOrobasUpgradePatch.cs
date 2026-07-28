using HarmonyLib;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>
/// Make Touch of Orobas upgrade Saiyan Blood into Legendary Lineage
/// instead of falling back to Circlet.
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
public static class KakarotTouchOfOrobasUpgradePatch
{
    public static void Postfix(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic.Id == ModelDb.Relic<SaiyanBlood>().Id)
        {
            __result = ModelDb.Relic<KakarotLegendaryLineage>();
        }
    }
}
