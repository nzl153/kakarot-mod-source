using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotWildUnleashed() : KakarotCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override int CanonicalStarCost => 2;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = Owner.PlayerCombatState.Hand.Cards.ToList();
        foreach (var card in hand)
        {
            if (ReferenceEquals(card, this))
            {
                continue;
            }

            card.AddKeyword(KakarotWildKeyword.Wild);
            KakarotWildHelper.EnsureWildActsUpgradedIfSs4(Owner, card);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
