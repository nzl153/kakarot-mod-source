using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotWildCharge() : KakarotCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    // 升级只奖励继续维护野性；不满足条件时不提供通用抽牌收益。
    // 蓄气 KakarotChargeUp 以 1 能量换 3 怒气，本卡基础效果以 3 怒气换 1 能量；调整任一张定价时必须联动评估。
    public override int CanonicalStarCost => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Draw", 3m),
        new EnergyVar(1),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = Owner.PlayerCombatState.Hand.Cards;
        bool leftmostIsWild = hand.Count > 0 && KakarotWildHelper.HasWild(hand[0]);

        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, Owner);
        if (leftmostIsWild)
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}
