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
    private const string EnergyIconPath = "res://images/packed/sprite_fonts/kakarot_energy_icon.png";
    private const string RageIconPath = "res://images/packed/sprite_fonts/kakarot_star_icon.png";
    private const string DefaultStarIconPath = "res://images/packed/sprite_fonts/star_icon.png";
    private const string PresentationAppliedMeta = "kakarot_multiplayer_presentation_applied";
    private const string OriginalTextureMeta = "kakarot_original_texture";

    [HarmonyPostfix]
    public static void Postfix(NMultiplayerPlayerState __instance)
    {
        try
        {
            ApplyPresentation(__instance);
            Callable.From(() => ApplyPresentation(__instance)).CallDeferred();
        }
        catch (Exception e)
        {
            GD.PrintErr($"[KakarotMod] Failed to refresh multiplayer player icon: {e.Message}");
        }
    }

    internal static void ApplyPresentation(NMultiplayerPlayerState playerState)
    {
        if (playerState == null || !GodotObject.IsInstanceValid(playerState))
        {
            return;
        }

        if (!IsKakarot(playerState.Player))
        {
            RestoreOriginalPresentation(playerState);
            return;
        }

        TextureRect iconRect =
            playerState.GetNodeOrNull<TextureRect>("%CharacterIcon") ??
            playerState.FindChild("CharacterIcon", true, false) as TextureRect;
        TextureRect energyRect = FindCounterImage(playerState, "%EnergyCountContainer");
        TextureRect starRect = FindCounterImage(playerState, "%StarCountContainer");

        if (!playerState.HasMeta(PresentationAppliedMeta))
        {
            RememberOriginalTexture(iconRect);
            RememberOriginalTexture(energyRect);
            RememberOriginalTexture(starRect);
            playerState.SetMeta(PresentationAppliedMeta, true);
        }

        Texture2D texture = LoadTexture(CustomIconPath, FallbackTopPanelIconPath);
        if (iconRect != null && texture != null)
        {
            iconRect.Texture = texture;
        }

        SetTexture(energyRect, EnergyIconPath);
        SetTexture(starRect, RageIconPath);
    }

    private static void RestoreOriginalPresentation(NMultiplayerPlayerState playerState)
    {
        if (!playerState.HasMeta(PresentationAppliedMeta))
        {
            return;
        }

        Player player = playerState.Player;
        TextureRect iconRect =
            playerState.GetNodeOrNull<TextureRect>("%CharacterIcon") ??
            playerState.FindChild("CharacterIcon", true, false) as TextureRect;
        TextureRect energyRect = FindCounterImage(playerState, "%EnergyCountContainer");
        TextureRect starRect = FindCounterImage(playerState, "%StarCountContainer");

        RestoreTexture(iconRect, player?.Character?.IconTexture);
        RestoreTexture(energyRect, LoadTexture(player?.Character?.CardPool?.EnergyIconPath));
        RestoreTexture(starRect, LoadTexture(DefaultStarIconPath));
        playerState.RemoveMeta(PresentationAppliedMeta);
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

    private static TextureRect FindCounterImage(Node root, string containerPath)
    {
        return root.GetNodeOrNull<Control>(containerPath)?.GetNodeOrNull<TextureRect>("Image");
    }

    private static void RememberOriginalTexture(TextureRect image)
    {
        if (image != null && !image.HasMeta(OriginalTextureMeta) && image.Texture != null)
        {
            image.SetMeta(OriginalTextureMeta, image.Texture);
        }
    }

    private static void RestoreTexture(TextureRect image, Texture2D fallback)
    {
        if (image == null)
        {
            return;
        }

        Texture2D original = image.HasMeta(OriginalTextureMeta)
            ? image.GetMeta(OriginalTextureMeta).AsGodotObject() as Texture2D
            : null;
        if (original != null || fallback != null)
        {
            image.Texture = original ?? fallback;
        }

        if (image.HasMeta(OriginalTextureMeta))
        {
            image.RemoveMeta(OriginalTextureMeta);
        }
    }

    private static void SetTexture(TextureRect image, string texturePath)
    {
        Texture2D texture = LoadTexture(texturePath);
        if (image != null && texture != null)
        {
            image.Texture = texture;
        }
    }

    private static Texture2D LoadTexture(params string[] paths)
    {
        foreach (string path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path))
            {
                return ResourceLoader.Load<Texture2D>(path);
            }
        }
        return null;
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerState), "RefreshCombatValues")]
public static class KakarotMultiplayerPlayerStateResourceIconPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMultiplayerPlayerState __instance)
    {
        try
        {
            KakarotMultiplayerPlayerStateIconPatch.ApplyPresentation(__instance);
        }
        catch
        {
            // Cosmetic only: never interrupt multiplayer combat UI updates.
        }
    }
}
