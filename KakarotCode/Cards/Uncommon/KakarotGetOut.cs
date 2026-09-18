using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotGetOut() : KakarotCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int CanonicalStarCost => 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("ExhaustPickMax", 2m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var maxPick = (int)DynamicVars["ExhaustPickMax"].BaseValue;
        if (Owner.PlayerCombatState.Hand.Cards.Count == 0)
        {
            return;
        }

        var chosen = await KakarotCardSelectHelper.FromHandSelectRangeAsync(choiceContext, Owner, 0, maxPick, this);
        foreach (var c in chosen)
        {
            await CardCmd.Exhaust(choiceContext, c, causedByEthereal: false, skipVisuals: false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ExhaustPickMax"].UpgradeValueBy(1m);
    }
}
