using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotRageBurning() : KakarotCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StarCost", 3m),
        new DynamicVar("Strength", 1m),
    ];

    public override int CanonicalStarCost => (int)DynamicVars["StarCost"].BaseValue;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["Strength"].BaseValue, Owner.Creature, this);

        var drawPile = Owner.PlayerCombatState.DrawPile;
        if (drawPile.Cards.Count > 0)
        {
            var firstCandidate = drawPile.Cards[0];
            var lastCandidate = drawPile.Cards[^1];
            await CardPileCmd.AutoPlayFromDrawPile(choiceContext, Owner, 1, CardPilePosition.Top, false);

            // Draw-pile internal order can vary by engine version; detect which "top" candidate actually got played.
            CardModel playedTopCard = null;
            if (firstCandidate?.Pile?.Type != PileType.Draw)
            {
                playedTopCard = firstCandidate;
            }
            else if (lastCandidate?.Pile?.Type != PileType.Draw)
            {
                playedTopCard = lastCandidate;
            }

            if (playedTopCard?.Pile != null)
            {
                await CardCmd.Exhaust(choiceContext, playedTopCard);
            }
        }
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-1);
        DynamicVars["Strength"].UpgradeValueBy(1m);
    }
}
