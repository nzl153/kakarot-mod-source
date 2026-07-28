using System.Threading.Tasks;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Rare;

/// <summary>稀有能力：领悟极易（可进常规奖励池）。升级后 2 能量 2 怒气。</summary>
public class KakarotInsightUltraInstinct() : KakarotCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override int CanonicalStarCost => 3;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<KakarotInsightUltraInstinctPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        UpgradeStarCostBy(-1);
    }
}
