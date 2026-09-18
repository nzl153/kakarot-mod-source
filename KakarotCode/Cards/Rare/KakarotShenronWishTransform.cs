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

        if (roll < SCellThreshold)
        {
            await KakarotSCellPower.TryGrantCellsAsync(choiceContext, Owner, SCellGrant, this);
            return;
        }

        await CardPileCmd.Draw(choiceContext, 1, Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
