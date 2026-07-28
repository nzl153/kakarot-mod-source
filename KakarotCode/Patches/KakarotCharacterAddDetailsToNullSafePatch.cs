#nullable disable
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>
/// 事件选项若缺少本地化，<see cref="MegaCrit.Sts2.Core.Events.EventOption"/> 会把 null <see cref="LocString"/> 传给
/// <see cref="CharacterModel.AddDetailsTo"/> 导致 NRE、界面无按钮。缺文案时跳过注入变量。
/// </summary>
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
