using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NCard), "Reload")]
public static class KakarotCardStarIconPatch
{
    private const string EnergyIcon = "res://images/packed/sprite_fonts/kakarot_energy_icon.png";
    private const string DefaultStarIcon = "res://images/packed/sprite_fonts/star_icon.png";
    private const string Star1 = "res://images/ui/combat/kakarot_energy_star.png";
    private const string Star2 = "res://images/ui/combat/kakarot_energy_star_layer_2.png";
    private const string Star3 = "res://images/ui/combat/kakarot_energy_star_layer_3.png";

    public static void Postfix(NCard __instance)
    {
        try
        {
            var model = __instance.Model;
            if (!IsKakarot(model))
            {
                // NCard nodes are pooled. Only clear a texture that demonstrably
                // came from Kakarot so another mod's custom star icon is untouched.
                var starIcon = __instance.GetNodeOrNull<TextureRect>("%StarIcon");
                var currentPath = starIcon?.Texture?.ResourcePath ?? string.Empty;
                if (currentPath.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
                {
                    ReplaceTexture(__instance, "%StarIcon", DefaultStarIcon);
                }
                return;
            }

            ReplaceTexture(__instance, "%EnergyIcon", EnergyIcon);
            ReplaceTexture(__instance, "%StarIcon", Star1);
            ReplaceTexture(__instance, "%StarIcon/RotationLayers/Layer1", Star2);
            ReplaceTexture(__instance, "%StarIcon/RotationLayers/Layer2", Star3);
        }
        catch
        {
            // Cosmetic only: never break card rendering.
        }
    }

    private static bool IsKakarot(CardModel model)
    {
        if (model == null)
        {
            return false;
        }

        var id = model.Pool?.Id?.Entry;
        return !string.IsNullOrWhiteSpace(id) && id.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceTexture(Node root, string nodePath, string texturePath)
    {
        if (!ResourceLoader.Exists(texturePath))
        {
            return;
        }

        var textureRect = root.GetNodeOrNull<TextureRect>(nodePath);
        if (textureRect == null)
        {
            return;
        }

        var texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            textureRect.Texture = texture;
        }
    }
}
