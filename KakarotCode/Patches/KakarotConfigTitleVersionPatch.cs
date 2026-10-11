using System.Reflection;
using BaseLib.Config;
using BaseLib.Config.UI;
using HarmonyLib;
using KakarotMod.KakarotCode.Config;

namespace KakarotMod.KakarotCode.Patches;

// Shows the mod version (and branch) next to the settings page title so players can report it.
[HarmonyPatch]
public static class KakarotConfigTitleVersionPatch
{
    private static MethodBase Target => AccessTools.Method(typeof(NModConfigSubmenu), "GetModTitle");

    private static bool Prepare() => Target != null;

    private static MethodBase TargetMethod() => Target;

    private static void Postfix(ModConfig config, ref string __result)
    {
        if (config is not KakarotModConfig)
        {
            return;
        }

        var version = typeof(KakarotModConfig).Assembly.GetName().Version!.ToString(3);
#if STS2_BETA
        __result += $"  v{version} (Beta)";
#else
        __result += $"  v{version}";
#endif
    }
}
