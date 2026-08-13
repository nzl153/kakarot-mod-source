using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotPotentialRelease() : KakarotCard(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    public override int CanonicalStarCost => 5;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Energy", 2m),
        new PowerVar<KakarotBattleRhythmPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotBattleRhythmPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, 1, Owner);
        await PlayerCmd.GainEnergy(DynamicVars["Energy"].BaseValue, Owner);
        await KakarotPowerCmd.Apply<KakarotBattleRhythmPower>(choiceContext, Owner.Creature, DynamicVars["KakarotBattleRhythmPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-1);
    }
}
