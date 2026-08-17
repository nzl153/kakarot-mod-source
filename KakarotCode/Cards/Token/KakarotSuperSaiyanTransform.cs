using System;
using System.Collections.Generic;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Token;

public class KakarotSuperSaiyanTransform() : KakarotCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override int CanonicalStarCost => 4;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override bool IsPlayable
    {
        get
        {
            var tier = Owner?.Creature?.GetPowerAmount<SuperSaiyanFormPower>() ?? 0;
            return base.IsPlayable && tier < 3;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        var currentForm = creature.GetPower<SuperSaiyanFormPower>();
        var currentTier = currentForm == null ? 0 : (int)currentForm.Amount;
        var firstAttackRageGranted = currentForm?.FirstAttackBonusRageGrantedThisTurn ?? false;
        await KakarotUltraInstinctTriggerHelper.RemoveAllTransformPowers(Owner.Creature);
        var nextTier = (int)Math.Min(currentTier + 1, 3);

        await PowerCmd.Remove<SuperSaiyanFormPower>(creature);
        var form = (SuperSaiyanFormPower)ModelDb.Power<SuperSaiyanFormPower>().ToMutable();
        form.ConfigureTier(nextTier, firstAttackRageGranted);
        await KakarotPowerCmd.Apply(choiceContext, form, creature, nextTier, creature, this);

        KakarotFormVisuals.Refresh(creature);

        // Entering tier 3 grants its form-exclusive finisher.
        if (nextTier == 3)
        {
            var dragonFist = CombatState.CreateCard<KakarotDragonFistBurst>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(dragonFist, PileType.Hand, Owner);
        }

        // Follow-up cards are delivered after play resolution to avoid the manual-play lock.
        SaiyanBlood.MarkPendingSuperSaiyanTransformFollowup(Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }

#if STS2_BETA
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var result = base.GetResultLocationForCardPlay();
        if (result.pileType == PileType.Discard)
        {
            result.pileType = PileType.Hand;
        }

        return result;
    }
#else
    protected override PileType GetResultPileTypeForCardPlay() => PileType.Hand;
#endif
}
