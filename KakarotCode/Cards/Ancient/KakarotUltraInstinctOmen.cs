using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Ancient;

public class KakarotUltraInstinctOmen() : KakarotCard(0, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        await KakarotUltraInstinctTriggerHelper.RemoveSaiyanTransformsForUltraInstinct(creature);
        await PowerCmd.Remove<KakarotUltraInstinctOmenPower>(creature);
        await PowerCmd.Remove<KakarotPerfectUltraInstinctPower>(creature);

        var omen = (KakarotUltraInstinctOmenPower)ModelDb.Power<KakarotUltraInstinctOmenPower>().ToMutable();
        await KakarotPowerCmd.Apply(choiceContext, omen, creature, 1, creature, this);

        var peak = CombatState.CreateCard<KakarotPeakUltraInstinct>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(peak, PileType.Hand, Owner);
        KakarotFormVisuals.Refresh(creature);
    }
}
