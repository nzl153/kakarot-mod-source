using System;
using System.Collections;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NEnergyCounter), "_Ready")]
public static class KakarotEnergyCounterColorPatch
{
    private static readonly Color OrbTint = new("f3c44f");
    private static readonly Color OrbTintDim = new("c79a31");
    private static readonly Color OutlineTint = new("7a4f12");

    private const string Orb1 = "res://images/ui/combat/energy_counters/kakarot/kakarot_orb_layer_1.png";
    private const string Orb2 = "res://images/ui/combat/energy_counters/kakarot/kakarot_orb_layer_2.png";
    private const string Orb3 = "res://images/ui/combat/energy_counters/kakarot/kakarot_orb_layer_3.png";
    private const string Orb4 = "res://images/ui/combat/energy_counters/kakarot/kakarot_orb_layer_4.png";
    private const string Orb5 = "res://images/ui/combat/energy_counters/kakarot/kakarot_orb_layer_5.png";

    public static void Postfix(NEnergyCounter __instance)
    {
        try
        {
            var player = Traverse.Create(__instance).Field("_player").GetValue<Player>();
            if (!IsKakarot(player))
            {
                return;
            }

            var layers = __instance.GetNodeOrNull<Control>("%Layers");
            var rotationLayers = __instance.GetNodeOrNull<Control>("%RotationLayers");
            var label = __instance.GetNodeOrNull<Label>("Label");
            var backParticles = __instance.GetNodeOrNull<CpuParticles2D>("%BurstBack");
            var frontParticles = __instance.GetNodeOrNull<CpuParticles2D>("%BurstFront");

            ForceReplaceEnergyCounterTextures(__instance);
            TintTree(layers, OrbTint, OrbTintDim);
            TintTree(rotationLayers, OrbTint, OrbTintDim);

            if (label != null)
            {
                label.AddThemeColorOverride("font_outline_color", OutlineTint);
            }

            if (backParticles != null)
            {
                backParticles.Color = OrbTint;
            }

            if (frontParticles != null)
            {
                frontParticles.Color = OrbTint;
            }
        }
        catch
        {
            // Purely cosmetic patch: never break room flow.
        }
    }

    private static bool IsKakarot(Player p)
    {
        var id = p?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(id) && id.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }

    private static void TintTree(Node root, Color normalTint, Color dimTint)
    {
        if (root == null)
        {
            return;
        }

        var stack = new Stack();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop() as Node;
            if (node == null)
            {
                continue;
            }

            if (node is CanvasItem ci)
            {
                // Keep a subtle difference between main and rotating layers.
                ci.Modulate = node.Name.ToString().Contains("Rotation", StringComparison.OrdinalIgnoreCase) ? dimTint : normalTint;
            }

            foreach (Node child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    private static void ForceReplaceEnergyCounterTextures(NEnergyCounter counter)
    {
        ReplaceTexture(counter, "Layers/Layer1", Orb1);
        ReplaceTexture(counter, "Layers/RotationLayers/Layer2", Orb2);
        ReplaceTexture(counter, "Layers/RotationLayers/Layer3", Orb3);
        ReplaceTexture(counter, "Layers/Layer4", Orb4);
        ReplaceTexture(counter, "Layers/Layer5", Orb5);
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

[HarmonyPatch(typeof(NStarCounter), nameof(NStarCounter.Initialize))]
public static class KakarotStarCounterScalePatch
{
    private const string Star1 = "res://images/ui/combat/kakarot_energy_star.png";
    private const string Star2 = "res://images/ui/combat/kakarot_energy_star_layer_2.png";
    private const string Star3 = "res://images/ui/combat/kakarot_energy_star_layer_3.png";

    public static void Postfix(NStarCounter __instance, Player player)
    {
        try
        {
            if (!IsKakarot(player))
            {
                return;
            }

            ApplyKakarotStarHoverTip(__instance);
            // Restore Kakarot custom star layers and original sizing intent.
            ReplaceStarTextures(__instance);
            __instance.Scale = new Vector2(0.62f, 0.62f);
            __instance.Position += new Vector2(0f, 16f);
            __instance.SetMeta("kakarot_star_scaled", true);
        }
        catch
        {
            // Cosmetic only.
        }
    }

    private static bool IsKakarot(Player p)
    {
        var id = p?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(id) && id.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceStarTextures(Node root)
    {
        ReplaceTexture(root, "Icon", Star1);
        ReplaceTexture(root, "Icon/RotationLayers/Layer1", Star2);
        ReplaceTexture(root, "Icon/RotationLayers/Layer2", Star3);
    }

    private static void ApplyKakarotStarHoverTip(NStarCounter starCounter)
    {
        var description = new LocString("static_hover_tips", "KAKAROT_STAR_COUNT.description");
        description.Add("singleStarIcon", "[img]res://images/packed/sprite_fonts/kakarot_star_icon.png[/img]");
        var title = new LocString("static_hover_tips", "KAKAROT_STAR_COUNT.title");
        var hoverTip = new HoverTip(title, description);
        Traverse.Create(starCounter).Field("_hoverTip").SetValue(hoverTip);
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

/// <summary>初始化路径未执行时的兜底刷新；完成一次设置后通过元数据快速跳过后续帧。</summary>
[HarmonyPatch(typeof(NStarCounter), "_Process")]
public static class KakarotStarCounterScaleRuntimePatch
{
    private const string Star1 = "res://images/ui/combat/kakarot_energy_star.png";
    private const string Star2 = "res://images/ui/combat/kakarot_energy_star_layer_2.png";
    private const string Star3 = "res://images/ui/combat/kakarot_energy_star_layer_3.png";

    public static void Postfix(NStarCounter __instance)
    {
        try
        {
            if (__instance.HasMeta("kakarot_star_scaled"))
            {
                return;
            }

            var player = Traverse.Create(__instance).Field("_player").GetValue<Player>();
            var id = player?.Character?.Id.Entry;
            if (string.IsNullOrWhiteSpace(id) || !id.Contains("kakarot", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyKakarotStarHoverTip(__instance);
            ReplaceStarTextures(__instance);
            __instance.Scale = new Vector2(0.62f, 0.62f);
            __instance.Position += new Vector2(0f, 16f);
            __instance.SetMeta("kakarot_star_scaled", true);
        }
        catch
        {
            // Cosmetic only.
        }
    }

    private static void ReplaceStarTextures(Node root)
    {
        ReplaceTexture(root, "Icon", Star1);
        ReplaceTexture(root, "Icon/RotationLayers/Layer1", Star2);
        ReplaceTexture(root, "Icon/RotationLayers/Layer2", Star3);
    }

    private static void ApplyKakarotStarHoverTip(NStarCounter starCounter)
    {
        var description = new LocString("static_hover_tips", "KAKAROT_STAR_COUNT.description");
        description.Add("singleStarIcon", "[img]res://images/packed/sprite_fonts/kakarot_star_icon.png[/img]");
        var title = new LocString("static_hover_tips", "KAKAROT_STAR_COUNT.title");
        var hoverTip = new HoverTip(title, description);
        Traverse.Create(starCounter).Field("_hoverTip").SetValue(hoverTip);
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
