using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.DynamicVars;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

/// <summary>S-cell training: pay HP to draw and gain Energy + rage.</summary>
public class KakarotExtremeTraining() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal HpCost = 3m;

    /// <summary>S 细胞发放量固定，与超赛减免后的实际扣血无关。</summary>
    private const int SCellGrant = 4;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotSCellPower>()];

    protected override bool ShouldGlowGoldInternal =>
        Owner?.Creature?.GetPower<SuperSaiyanFormPower>() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new KakarotReducedHpLossVar("ResolvedHpLoss", HpCost),
        new DynamicVar("Draw", 2m),
        new DynamicVar("Stars", 1m),
        new DynamicVar("SCells", SCellGrant),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hpLoss = KakarotTrainingSelfHpCost.Resolve(HpCost, Owner.Creature);
        await CreatureCmd.Damage(choiceContext, Owner.Creature, hpLoss, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);

        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["Draw"].BaseValue, Owner);
        await PlayerCmd.GainStars(DynamicVars["Stars"].BaseValue, Owner);

        await KakarotSCellPower.TryGrantCellsAsync(choiceContext, Owner, SCellGrant, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Stars"].UpgradeValueBy(2m);
    }
}
