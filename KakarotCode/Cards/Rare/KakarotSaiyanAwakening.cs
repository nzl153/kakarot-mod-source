using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotSaiyanAwakening() : KakarotCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override int CanonicalStarCost => 4;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<KakarotSaiyanAwakeningPower>(2m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotSaiyanAwakeningPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotSaiyanAwakeningPower>(choiceContext, Owner.Creature, DynamicVars["KakarotSaiyanAwakeningPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["KakarotSaiyanAwakeningPower"].UpgradeValueBy(1m);
    }
}
