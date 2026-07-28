using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotQiqiMeal() : KakarotCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override int CanonicalStarCost => 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        KakarotWildDisplay.MergeWild(this, IsUpgraded ? [CardKeyword.Retain] : []);

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<KakarotQiqiMealPower>(6m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotQiqiMealPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotQiqiMealPower>(choiceContext, Owner.Creature, DynamicVars["KakarotQiqiMealPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
