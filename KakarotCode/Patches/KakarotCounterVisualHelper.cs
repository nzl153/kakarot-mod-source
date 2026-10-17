using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace KakarotMod.KakarotCode.Patches;

internal static class KakarotCounterVisualHelper
{
    private const string Star1 = "res://images/ui/combat/kakarot_energy_star.png";
    private const string Star2 = "res://images/ui/combat/kakarot_energy_star_layer_2.png";
    private const string Star3 = "res://images/ui/combat/kakarot_energy_star_layer_3.png";

    internal static void ReplaceTexture(Node root, string nodePath, string texturePath)
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

    internal static void ReplaceStarTextures(Node root)
    {
        ReplaceTexture(root, "Icon", Star1);
        ReplaceTexture(root, "Icon/RotationLayers/Layer1", Star2);
        ReplaceTexture(root, "Icon/RotationLayers/Layer2", Star3);
    }

    internal static void ApplyKakarotStarHoverTip(NStarCounter starCounter)
    {
        var description = new LocString("static_hover_tips", "KAKAROT_STAR_COUNT.description");
        description.Add("singleStarIcon", "[img]res://images/packed/sprite_fonts/kakarot_star_icon.png[/img]");
        var title = new LocString("static_hover_tips", "KAKAROT_STAR_COUNT.title");
        var hoverTip = new HoverTip(title, description);
        Traverse.Create(starCounter).Field("_hoverTip").SetValue(hoverTip);
    }
}
