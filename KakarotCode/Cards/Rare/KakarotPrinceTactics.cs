using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotPrinceTactics() : KakarotCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override int CanonicalStarCost => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Damage", 2m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotPrinceTacticsPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotPrinceTacticsPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        var bonusDamage = DynamicVars["Damage"].BaseValue - KakarotPrinceTacticsPower.BaseDamagePerStack;
        if (bonusDamage > 0m && Owner.Creature.HasPower<KakarotPrinceTacticsPower>())
        {
            await KakarotPowerCmd.Apply<KakarotPrinceTacticsBonusDamagePower>(
                choiceContext,
                Owner.Creature,
                bonusDamage,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Damage"].UpgradeValueBy(2m);
    }
}
