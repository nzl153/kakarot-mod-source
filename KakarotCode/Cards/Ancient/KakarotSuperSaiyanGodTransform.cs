using System.Collections.Generic;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace KakarotMod.KakarotCode.Cards.Ancient;

public class KakarotSuperSaiyanGodTransform() : KakarotCard(0, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;

    // Exhaustion is resolved explicitly in OnPlay, but remains visible on the card face.
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override bool IsPlayable
    {
        get
        {
            try
            {
                return base.IsPlayable && Owner?.Creature?.GetPower<KakarotSuperSaiyanGodPower>() == null;
            }
            catch (CanonicalModelException)
            {
                return base.IsPlayable;
            }
            catch (MutableModelException)
            {
                return base.IsPlayable;
            }
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await EnterSuperSaiyanGodForm(choiceContext, Owner, this, addGodKiCard: true);

        await CardCmd.Exhaust(choiceContext, this, causedByEthereal: false, skipVisuals: false);
    }

    public static async Task EnterSuperSaiyanGodForm(PlayerChoiceContext choiceContext, Player owner, CardModel sourceCard, bool addGodKiCard)
    {
        if (owner?.Creature == null)
        {
            GD.PrintErr("[Kakarot] EnterSuperSaiyanGodForm aborted: owner/creature is null.");
            return;
        }

        var creature = owner.Creature;
        await KakarotUltraInstinctTriggerHelper.RemoveAllTransformPowers(creature);

        var god = (KakarotSuperSaiyanGodPower)ModelDb.Power<KakarotSuperSaiyanGodPower>().ToMutable();
        await KakarotPowerCmd.Apply(choiceContext, god, creature, 1, creature, sourceCard);

        KakarotFormVisuals.Refresh(creature);

        if (!addGodKiCard)
        {
            return;
        }

        var combatState = creature.CombatState;
        var playerCombatState = owner.PlayerCombatState;
        if (combatState == null || playerCombatState?.Hand?.Cards == null)
        {
            GD.PrintErr("[Kakarot] EnterSuperSaiyanGodForm skipped bonus card generation: combat state is unavailable.");
            return;
        }

        var godKi = combatState.CreateCard<KakarotGodKi>(owner);
        await CardPileCmd.AddGeneratedCardToCombat(godKi, PileType.Hand, owner);

        var destruction = combatState.CreateCard<KakarotDestruction>(owner);
        await CardPileCmd.AddGeneratedCardToCombat(destruction, PileType.Hand, owner);
    }
}
