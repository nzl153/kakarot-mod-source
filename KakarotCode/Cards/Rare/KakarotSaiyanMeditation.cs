using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotSaiyanMeditation() : KakarotCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Draw", 3m), new DynamicVar("RetainPicks", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var draw = (int)DynamicVars["Draw"].BaseValue;
        await CardPileCmd.Draw(choiceContext, draw, Owner);

        var picks = (int)DynamicVars["RetainPicks"].BaseValue;
        var chosen = await KakarotCardSelectHelper.FromHandSelectAsync(choiceContext, Owner, picks, this);

        foreach (var c in chosen)
        {
            c.AddKeyword(CardKeyword.Retain);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Draw"].UpgradeValueBy(1m);
        DynamicVars["RetainPicks"].UpgradeValueBy(1m);
    }
}
