using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NDebugAudioManager), nameof(NDebugAudioManager.Play))]
public static class TmpSfxFallbackPatch
{
    private static bool _hasLoggedFallback;

    public static void Prefix(ref string streamName)
    {
        // Keep the fallback surgical: only touch the known missing tmp sfx.
        if (streamName != TmpSfx.bluntAttack)
        {
            return;
        }
        streamName = TmpSfx.heavyAttack;
        if (_hasLoggedFallback)
        {
            return;
        }

        GD.Print("[Kakarot] tmp sfx forced remap: blunt_attack.mp3 -> heavy_attack.mp3");
        _hasLoggedFallback = true;
    }
}
