using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch]
public static class TopBarPortraitIconPatch
{
    private const string CustomIconPath = "res://Kakarot/Images/Charui/character_icon_char_name.png";
    private const string FallbackTopPanelIconPath = "res://images/ui/top_panel/character_icon_kakarotmod-kakarot.png";

    static MethodBase TargetMethod()
    {
        Type portraitType =
            AccessTools.TypeByName("MegaCrit.sts2.Core.Nodes.TopBar.NTopBarPortrait") ??
            AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.TopBar.NTopBarPortrait");

        return portraitType == null
            ? null
            : AccessTools.Method(portraitType, "Initialize", new[] { typeof(Player) });
    }

    static void Prefix(Node __instance, out HashSet<ulong> __state)
    {
        __state = new HashSet<ulong>();
        foreach (Node child in __instance.GetChildren())
        {
            __state.Add(child.GetInstanceId());
        }
    }

    static void Postfix(Node __instance, Player player, HashSet<ulong> __state)
    {
        string entry = player?.Character?.Id.Entry;
        if (string.IsNullOrWhiteSpace(entry) || !entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Texture2D texture = LoadIconTexture();
        if (texture == null)
        {
            return;
        }

        ReplaceNewPlaceholderTexture(__instance, __state, texture);
    }

    private static void ReplaceNewPlaceholderTexture(
        Node portrait,
        HashSet<ulong> childrenBeforeInitialize,
        Texture2D texture)
    {
        foreach (Node child in portrait.GetChildren())
        {
            if (childrenBeforeInitialize.Contains(child.GetInstanceId()))
            {
                continue;
            }

            if (child is TextureRect placeholderIcon)
            {
                placeholderIcon.Texture = texture;
                return;
            }
        }
    }

    private static Texture2D LoadIconTexture()
    {
        foreach (string path in new[] { CustomIconPath, FallbackTopPanelIconPath })
        {
            if (ResourceLoader.Exists(path))
            {
                return ResourceLoader.Load<Texture2D>(path);
            }
        }

        return null;
    }
}
