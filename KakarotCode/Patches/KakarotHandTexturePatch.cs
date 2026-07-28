using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Patches;

public static class KakarotHandTexturePatch
{
    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.ArmPointingTexture), MethodType.Getter)]
    [HarmonyPostfix]
    public static void ArmPointingTexturePostfix(CharacterModel __instance, ref Texture2D __result)
    {
        __result = ResolveHandTexture(__instance, "point", __result);
    }

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.ArmRockTexture), MethodType.Getter)]
    [HarmonyPostfix]
    public static void ArmRockTexturePostfix(CharacterModel __instance, ref Texture2D __result)
    {
        __result = ResolveHandTexture(__instance, "rock", __result);
    }

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.ArmPaperTexture), MethodType.Getter)]
    [HarmonyPostfix]
    public static void ArmPaperTexturePostfix(CharacterModel __instance, ref Texture2D __result)
    {
        __result = ResolveHandTexture(__instance, "paper", __result);
    }

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.ArmScissorsTexture), MethodType.Getter)]
    [HarmonyPostfix]
    public static void ArmScissorsTexturePostfix(CharacterModel __instance, ref Texture2D __result)
    {
        __result = ResolveHandTexture(__instance, "scissors", __result);
    }

    private static Texture2D ResolveHandTexture(CharacterModel model, string suffix, Texture2D fallback)
    {
        var entry = model?.Id.Entry;
        if (string.IsNullOrWhiteSpace(entry) || !entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        var explicitKakarot = $"res://images/ui/hands/multiplayer_hand_kakarot_{suffix}.png";
        if (ResourceLoader.Exists(explicitKakarot))
        {
            return PreloadManager.Cache.GetTexture2D(explicitKakarot);
        }

        var fullEntryPath = $"res://images/ui/hands/multiplayer_hand_{entry.ToLowerInvariant()}_{suffix}.png";
        if (ResourceLoader.Exists(fullEntryPath))
        {
            return PreloadManager.Cache.GetTexture2D(fullEntryPath);
        }

        return fallback;
    }
}
