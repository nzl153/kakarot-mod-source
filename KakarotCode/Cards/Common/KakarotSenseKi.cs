using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Common;

/// <summary>Pick a card from your Draw pile into hand.</summary>
public class KakarotSenseKi() : KakarotCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DrawPick", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var n = (int)DynamicVars["DrawPick"].BaseValue;
        _ = await KakarotCardSelectHelper.FromDrawPileSimpleGridAsync(choiceContext, Owner, n, this);
    }

    protected override void OnUpgrade()
    {
        _ = Keywords; // Ensure keyword set exists before removal.
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
