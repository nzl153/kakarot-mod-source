using KakarotMod.KakarotCode.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KakarotMod.KakarotCode.Daima;

// Kid -> SS1 -> SS3 -> kid SS4; returns to hand until the last step, then exhausts.
public class KakarotDaimaTransform() : KakarotCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override int CanonicalStarCost => 3;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override bool IsPlayable =>
        base.IsPlayable && Owner?.Creature != null && KakarotDaima.CanTransformFurther(Owner.Creature);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotDaima.AdvanceByTransform(Owner.Creature);
    }

    private PileType ResultPile => KakarotDaima.CanTransformFurther(Owner.Creature) ? PileType.Hand : PileType.Exhaust;

#if STS2_BETA
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var result = base.GetResultLocationForCardPlay();
        result.pileType = ResultPile;
        return result;
    }
#else
    protected override PileType GetResultPileTypeForCardPlay() => ResultPile;
#endif
}
