using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotBigAppetite() : KakarotCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override bool HasEnergyCostX => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("BonusHeal", 0m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [EnergyHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var healAmount = ResolveEnergyXValue() * 2 + DynamicVars["BonusHeal"].IntValue;
        if (healAmount > 0)
        {
            await CreatureCmd.Heal(Owner.Creature, healAmount);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BonusHeal"].UpgradeValueBy(2m);
    }
}
