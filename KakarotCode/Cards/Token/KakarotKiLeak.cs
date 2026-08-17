using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Token;

public class KakarotKiLeak() : KakarotCard(1, CardType.Status, CardRarity.Token, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;

#if STS2_BETA
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var result = base.GetResultLocationForCardPlay();
        if (result.pileType == PileType.Discard)
        {
            result.pileType = PileType.Exhaust;
        }

        return result;
    }
#else
    protected override PileType GetResultPileTypeForCardPlay() => PileType.Exhaust;
#endif
}
