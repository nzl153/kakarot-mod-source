using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NRunHistoryPlayerIcon), "LoadRun")]
public static class KakarotRunHistoryIconPatch
{
    private const string PreferredIconScenePath = "res://scenes/ui/character_icons/kakarotmod-kakarot_icon.tscn";
    private const string FallbackIconScenePath = "res://scenes/ui/character_icons/kakarot_icon.tscn";

    public static void Postfix(NRunHistoryPlayerIcon __instance)
    {
        try
        {
            var player = __instance?.Player;
            var entry = player?.Character.Entry;
            if (string.IsNullOrWhiteSpace(entry) || !entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var scenePath = ResourceLoader.Exists(PreferredIconScenePath)
                ? PreferredIconScenePath
                : ResourceLoader.Exists(FallbackIconScenePath)
                    ? FallbackIconScenePath
                    : null;
            if (scenePath == null)
            {
                return;
            }

            var packed = ResourceLoader.Load<PackedScene>(scenePath);
            if (packed?.Instantiate() is not Control icon)
            {
                return;
            }

            var traverse = Traverse.Create(__instance);
            var oldIcon = traverse.Field<Control>("_currentIcon").Value;
            oldIcon?.QueueFree();

            traverse.Field<Control>("_currentIcon").Value = icon;
            __instance.AddChild(icon);
            __instance.MoveChild(icon, 0);
        }
        catch
        {
            // UI-only fallback.
        }
    }
}
