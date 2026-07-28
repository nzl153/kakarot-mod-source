using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotPowerOfWildness() : KakarotCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override int CanonicalStarCost => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthGain", 1m),
        new DynamicVar("BlockGain", 4m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotPowerOfWildnessPower>(),
        WildKeywordHoverTip,
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotPowerOfWildnessPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        var bonusBlock = DynamicVars["BlockGain"].BaseValue - KakarotPowerOfWildnessPower.BaseBlockPerStack;
        if (bonusBlock > 0m && Owner.Creature.HasPower<KakarotPowerOfWildnessPower>())
        {
            await KakarotPowerCmd.Apply<KakarotPowerOfWildnessBonusBlockPower>(
                choiceContext,
                Owner.Creature,
                bonusBlock,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockGain"].UpgradeValueBy(4m);
    }
}
