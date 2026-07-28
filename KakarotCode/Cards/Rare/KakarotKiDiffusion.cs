using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotKiDiffusion() : KakarotCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var handCards = Owner?.PlayerCombatState?.Hand?.Cards?.ToList();
        if (handCards == null || handCards.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var card in handCards)
        {
            if (card == null || ReferenceEquals(card, this) || ReferenceEquals(card, cardPlay?.Card) || card.Pile?.Type != PileType.Hand)
            {
                continue;
            }

            if (card.IsUpgraded)
            {
                continue;
            }

            try
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }
            catch (System.Exception ex)
            {
                // Keep combat/action queue stable if one card cannot be upgraded.
                GD.PrintErr($"[Kakarot] KiDiffusion failed to upgrade card '{card.Id?.Entry ?? "UNKNOWN"}': {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
        EnergyCost.UpgradeBy(-1);
    }
}
