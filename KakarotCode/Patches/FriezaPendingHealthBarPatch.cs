#nullable enable
using System.Reflection;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Monsters.Frieza;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
public static class FriezaPendingHealthBarPatch
{
    private static readonly FieldInfo? CreatureField =
        AccessTools.Field(typeof(NHealthBar), "_creature");

    [HarmonyPostfix]
    public static void Postfix(NHealthBar __instance)
    {
        try
        {
            if (CreatureField?.GetValue(__instance) is not Creature creature ||
                creature.Monster is not FriezaBoss { IsAwaitingGoldenTransformation: true })
            {
                return;
            }

            __instance.GetNodeOrNull<Control>("%HpForeground")?.Hide();
            __instance.GetNodeOrNull<Control>("%PoisonForeground")?.Hide();
            __instance.GetNodeOrNull<Control>("%DoomForeground")?.Hide();
            __instance.GetNodeOrNull<Control>("%HpMiddleground")?.Hide();
            __instance.GetNodeOrNull<MegaLabel>("%HpLabel")?
                .SetTextAutoSize($"0/{creature.MaxHp}");
        }
        catch
        {
            // Cosmetic only: never interrupt health bar or combat synchronization.
        }
    }
}
