using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch]
public static class KakarotVfxIconScalePatch
{
    [HarmonyPatch(typeof(NRelicFlashVfx), "_Ready")]
    [HarmonyPostfix]
    public static void RelicFlashReadyPostfix(NRelicFlashVfx __instance)
    {
        ClampTextureRectScale(__instance, "Image1", 0.55f);
        ClampTextureRectScale(__instance, "Image2", 0.55f);
        ClampTextureRectScale(__instance, "Image3", 0.55f);
    }

    [HarmonyPatch(typeof(NPowerAppliedVfx), "_Ready")]
    [HarmonyPostfix]
    public static void PowerAppliedReadyPostfix(NPowerAppliedVfx __instance)
    {
        ClampTextureRectScale(__instance, "Icon", 0.62f);
    }

    [HarmonyPatch(typeof(NPowerFlashVfx), "_Ready")]
    [HarmonyPostfix]
    public static void PowerFlashReadyPostfix(NPowerFlashVfx __instance)
    {
        var sprite = __instance.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite == null || !ShouldDownscale(sprite.Texture))
        {
            return;
        }

        if (sprite.Scale.X > 0.30f)
        {
            sprite.Scale = Vector2.One * 0.30f;
        }
    }

    private static void ClampTextureRectScale(Node owner, string nodePath, float maxScale)
    {
        var rect = owner.GetNodeOrNull<TextureRect>(nodePath);
        if (rect == null || !ShouldDownscale(rect.Texture))
        {
            return;
        }

        if (rect.Scale.X > maxScale)
        {
            rect.Scale = Vector2.One * maxScale;
        }
    }

    private static bool ShouldDownscale(Texture2D texture)
    {
        if (texture == null)
        {
            return false;
        }

        var path = texture.ResourcePath ?? string.Empty;
        return path.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
    }
}
