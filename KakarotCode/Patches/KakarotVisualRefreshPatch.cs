using HarmonyLib;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NCreature), "_Ready")]
public static class KakarotVisualRefreshPatch
{
    public static void Postfix(NCreature __instance)
    {
        if (__instance?.Entity != null)
        {
            KakarotFormVisuals.Refresh(__instance.Entity);
        }
    }
}

[HarmonyPatch(typeof(NCreature), "StartDeathAnim")]
public static class KakarotVisualDeathRefreshPatch
{
    public static void Postfix(NCreature __instance)
    {
        if (__instance?.Entity != null)
        {
            KakarotFormVisuals.Refresh(__instance.Entity);
        }
    }
}

[HarmonyPatch(typeof(NCreature), "StartReviveAnim")]
public static class KakarotVisualReviveRefreshPatch
{
    public static void Postfix(NCreature __instance)
    {
        if (__instance?.Entity != null)
        {
            KakarotFormVisuals.Refresh(__instance.Entity);
        }
    }
}
