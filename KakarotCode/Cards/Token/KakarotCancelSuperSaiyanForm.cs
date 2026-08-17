using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Token;

public class KakarotCancelSuperSaiyanForm() : KakarotCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        var current = creature.GetPower<SuperSaiyanFormPower>();
        var tier = current == null ? 0 : (int)current.Amount;
        var firstAttackRageGranted = current?.FirstAttackBonusRageGrantedThisTurn ?? false;
        if (tier <= 0)
        {
            return;
        }

        await PowerCmd.Remove<SuperSaiyanFormPower>(creature);

        if (tier <= 1)
        {
            KakarotFormVisuals.Refresh(creature);
            return;
        }

        var newTier = tier - 1;
        var form = (SuperSaiyanFormPower)ModelDb.Power<SuperSaiyanFormPower>().ToMutable();
        form.ConfigureTier(newTier, firstAttackRageGranted);
        await KakarotPowerCmd.Apply(choiceContext, form, creature, newTier, creature, this);

        KakarotFormVisuals.Refresh(creature);
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
