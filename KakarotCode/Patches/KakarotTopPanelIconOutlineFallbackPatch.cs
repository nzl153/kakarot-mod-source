using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

public static class KakarotTopPanelIconOutlineFallbackPatch
{
    private const string CustomOutlinePath = "res://Kakarot/Images/Charui/character_icon_char_name_outline.png";
    private const string CustomIconPath = "res://Kakarot/Images/Charui/character_icon_char_name.png";
    private const string FallbackTopPanelIconPath = "res://images/ui/top_panel/character_icon_kakarotmod-kakarot.png";

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.IconTexture), MethodType.Getter)]
    [HarmonyPostfix]
    public static void IconTexturePostfix(CharacterModel __instance, ref Texture2D __result)
    {
        var entry = __instance?.Id.Entry;
        if (string.IsNullOrWhiteSpace(entry) || !entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var path in new[] { CustomIconPath, FallbackTopPanelIconPath })
        {
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            __result = PreloadManager.Cache.GetTexture2D(path);
            return;
        }
    }

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.IconOutlineTexture), MethodType.Getter)]
    [HarmonyPostfix]
    public static void IconOutlineTexturePostfix(CharacterModel __instance, ref Texture2D __result)
    {
        var entry = __instance?.Id.Entry;
        if (string.IsNullOrWhiteSpace(entry) || !entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var path in new[] { CustomOutlinePath, CustomIconPath, FallbackTopPanelIconPath })
        {
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            __result = PreloadManager.Cache.GetTexture2D(path);
            return;
        }
    }
}
