using System.Collections.Generic;
using KakarotMod.KakarotCode.Cards;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Daima;

public class KakarotDaimaTailRegrowth() : KakarotCard(1, CardType.Power, CardRarity.Token, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DamageBonus", 2m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotDaimaTailPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotDaimaTailPower>(choiceContext, Owner.Creature,
            DynamicVars["DamageBonus"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DamageBonus"].UpgradeValueBy(1m);
    }
}
