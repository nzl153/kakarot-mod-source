using HarmonyLib;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace KakarotMod.KakarotCode.Patches;
// Touch of Orobas needs an explicit upgrade path for the custom starter relic.
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
