using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Daima;

// The relic bar has no click hook for relics, so right-clicks are caught on the holder's input.
[HarmonyPatch(typeof(NClickableControl), nameof(NClickableControl._GuiInput))]
public static class KakarotDaimaThirdEyeClickPatch
{
    public static void Postfix(NClickableControl __instance, InputEvent inputEvent)
    {
        if (__instance is not NRelicInventoryHolder holder
            || inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
            || holder.Relic?.Model is not KakarotDaimaThirdEye relic
            || !LocalContext.IsMe(relic.Owner)
            || !CombatManager.Instance.IsInProgress
            || relic.UsedThisCombat
            || relic.RequestPending)
        {
            return;
        }

        relic.RequestPending = true;
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new KakarotDaimaAwakenAction(relic.Owner));
    }
}
