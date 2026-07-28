using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotShenronWishTransform() : KakarotCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const int JusticeThreshold = 30;
    private const int WildThreshold = 60;
    private const int SCellThreshold = 90;

    private const int SCellGrant = 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotJusticeHeartRitualPower>(),
        HoverTipFactory.FromPower<KakarotWildRitualPower>(),
        HoverTipFactory.FromPower<KakarotSCellPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var roll = Owner.RunState.Rng.Niche.NextInt(100);
        if (roll < JusticeThreshold)
        {
            await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(choiceContext, Owner, this, 1);
            return;
        }

        if (roll < WildThreshold)
        {
            await KakarotWildRitualHandler.AddRitualProgress(choiceContext, Owner, this, 1);
            return;
        }

        // Intentional design: rolls 90-99 (10%) do nothing. The card text documents a
        // "10%: nothing" outcome, so the code is kept in sync with the description.
        if (roll < SCellThreshold)
        {
            await KakarotSCellPower.TryGrantCellsAsync(choiceContext, Owner, SCellGrant, this);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
