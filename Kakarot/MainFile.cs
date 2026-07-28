using System;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Patches;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace KakarotMod.Kakarot;

[ModInitializer(nameof(Initialize))]
public class MainFile
{
    public const string ModId = "Kakarot";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var harmony = new Harmony(ModId);
        harmony.PatchAll();
        KakarotWildKeyword.LogIfSlotCollides();

        try
        {
            KakarotStaticModelVisibility.TryApplyOverlayPatches(harmony);
            KakarotWildKeywordVisibilityPatches.TryApply(harmony);
            KakarotWildKeywordFacePatches.TryApply(harmony);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot] Optional patch probe failed: {ex.Message}");
        }
    }
}
