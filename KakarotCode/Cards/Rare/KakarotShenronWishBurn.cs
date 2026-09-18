using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotShenronWishBurn() : KakarotCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var handCount = Owner.PlayerCombatState.Hand.Cards.Count;
        if (handCount <= 0)
        {
            return;
        }

        var chosen = (await KakarotCardSelectHelper.FromHandSelectRangeAsync(choiceContext, Owner, 0, handCount, this)).ToList();
        var exhaustCount = chosen.Count;
        foreach (var card in chosen)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        if (exhaustCount > 0)
        {
            await CardPileCmd.Draw(choiceContext, exhaustCount, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
