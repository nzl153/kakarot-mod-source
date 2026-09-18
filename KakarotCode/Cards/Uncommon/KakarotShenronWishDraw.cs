using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotShenronWishDraw() : KakarotCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override int CanonicalStarCost => 3;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState.Hand.Cards.Count > 0)
        {
            var handCount = Owner.PlayerCombatState.Hand.Cards.Count;
            var chosen = (await KakarotCardSelectHelper.FromHandSelectRangeAsync(choiceContext, Owner, 0, handCount, this)).ToList();
            var discardCount = chosen.Count;
            foreach (var c in chosen)
            {
                await CardCmd.Discard(choiceContext, c);
            }
            if (discardCount > 0)
            {
                await CardPileCmd.Draw(choiceContext, discardCount, Owner);
            }
        }
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-1);
    }
}
