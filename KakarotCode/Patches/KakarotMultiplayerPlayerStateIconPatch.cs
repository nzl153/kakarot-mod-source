using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NMultiplayerPlayerState), "_Ready")]
public static class KakarotMultiplayerPlayerStateIconPatch
{
    private const string CustomIconPath = "res://Kakarot/Images/Charui/character_icon_char_name.png";
    private const string FallbackTopPanelIconPath = "res://images/ui/top_panel/character_icon_kakarotmod-kakarot.png";

    [HarmonyPostfix]
    public static void Postfix(NMultiplayerPlayerState __instance)
    {
        try
        {
            ApplyIcon(__instance);
            Callable.From(() => ApplyIcon(__instance)).CallDeferred();
        }
        catch (Exception e)
        {
            GD.PrintErr($"[KakarotMod] Failed to refresh multiplayer player icon: {e.Message}");
        }
    }

    private static void ApplyIcon(NMultiplayerPlayerState playerState)
    {
        if (playerState == null || !GodotObject.IsInstanceValid(playerState) || !IsKakarot(playerState.Player))
        {
            return;
        }

        TextureRect iconRect =
            playerState.GetNodeOrNull<TextureRect>("%CharacterIcon") ??
            playerState.FindChild("CharacterIcon", true, false) as TextureRect;
        Texture2D texture = LoadIconTexture();
        if (iconRect != null && texture != null)
        {
            iconRect.Texture = texture;
        }
    }

    private static bool IsKakarot(Player player)
    {
        var character = player?.Character;
        string entry = character?.Id.Entry;
        if (!string.IsNullOrWhiteSpace(entry) && entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string characterType = character?.GetType().FullName;
        if (!string.IsNullOrWhiteSpace(characterType) && characterType.Contains("Kakarot", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string cardPoolType = character?.CardPool?.GetType().FullName;
        return !string.IsNullOrWhiteSpace(cardPoolType) && cardPoolType.Contains("Kakarot", StringComparison.OrdinalIgnoreCase);
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
