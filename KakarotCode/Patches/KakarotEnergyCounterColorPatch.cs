using System;
using System.Collections.Generic;
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

        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is CanvasItem ci)
            {
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
        KakarotCounterVisualHelper.ReplaceTexture(counter, "Layers/Layer1", Orb1);
        KakarotCounterVisualHelper.ReplaceTexture(counter, "Layers/RotationLayers/Layer2", Orb2);
        KakarotCounterVisualHelper.ReplaceTexture(counter, "Layers/RotationLayers/Layer3", Orb3);
        KakarotCounterVisualHelper.ReplaceTexture(counter, "Layers/Layer4", Orb4);
        KakarotCounterVisualHelper.ReplaceTexture(counter, "Layers/Layer5", Orb5);
    }
}

[HarmonyPatch(typeof(NStarCounter), nameof(NStarCounter.Initialize))]
public static class KakarotStarCounterScalePatch
{
    public static void Postfix(NStarCounter __instance, Player player)
    {
        try
        {
            if (!IsKakarot(player))
            {
                return;
            }

            KakarotCounterVisualHelper.ApplyKakarotStarHoverTip(__instance);
            KakarotCounterVisualHelper.ReplaceStarTextures(__instance);
            __instance.Scale = new Vector2(0.62f, 0.62f);
            __instance.Position += new Vector2(0f, 16f);
            __instance.SetMeta("kakarot_star_scaled", true);
        }
        catch
        {
        }
    }

    private static bool IsKakarot(Player p)
    {
        var id = p?.Character?.Id.Entry;
        return !string.IsNullOrWhiteSpace(id) && id.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }
}

// Reused combat UI may skip Initialize; metadata keeps the process fallback one-shot.
[HarmonyPatch(typeof(NStarCounter), "_Process")]
public static class KakarotStarCounterScaleRuntimePatch
{
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

            KakarotCounterVisualHelper.ApplyKakarotStarHoverTip(__instance);
            KakarotCounterVisualHelper.ReplaceStarTextures(__instance);
            __instance.Scale = new Vector2(0.62f, 0.62f);
            __instance.Position += new Vector2(0f, 16f);
            __instance.SetMeta("kakarot_star_scaled", true);
        }
        catch
        {
        }
    }
}
