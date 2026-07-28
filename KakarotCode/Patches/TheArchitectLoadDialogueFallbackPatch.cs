using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Saves;

namespace KakarotMod.KakarotCode.Patches;
// Reuse Ironclad's script when the Architect has no dialogue registered for a mod character.
[HarmonyPatch(typeof(TheArchitect), "LoadDialogue")]
public static class TheArchitectLoadDialogueFallbackPatch
{
    private static void Postfix(TheArchitect __instance)
    {
        var traverse = Traverse.Create(__instance);
        if (traverse.Field<AncientDialogue>("_dialogue").Value != null)
            return;

        var owner = __instance.Owner;
        if (owner == null)
            return;

        int charVisits = SaveManager.Instance.Progress.GetStatsForCharacter(owner.Character.Id)?.TotalWins ?? 0;
        int wins = SaveManager.Instance.Progress.Wins;
        AncientDialogueSet dialogueSet = traverse.Property<AncientDialogueSet>("DialogueSet").Value;

        ModelId ironcladId = ModelDb.Character<Ironclad>().Id;
        List<AncientDialogue> items = dialogueSet.GetValidDialogues(ironcladId, charVisits, wins, allowAnyCharacterDialogues: false).ToList();
        if (items.Count == 0)
            items = dialogueSet.GetValidDialogues(ironcladId, charVisits, wins, allowAnyCharacterDialogues: true).ToList();
        if (items.Count == 0)
            return;

        AncientDialogue pick = __instance.Rng.NextItem(items);
        if (pick != null)
            traverse.Field<AncientDialogue>("_dialogue").Value = pick;
    }
}
