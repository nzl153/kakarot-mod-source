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

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotGravityTraining() : KakarotCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    private const decimal HpCost = 3m;

    private const int SCellGrant = 3;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotSCellPower>()];

    protected override bool ShouldGlowGoldInternal =>
        Owner?.Creature?.GetPower<SuperSaiyanFormPower>() != null;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new KakarotReducedHpLossVar("ResolvedHpLoss", HpCost),
        new DynamicVar("Stars", 2m),
        new BlockVar(3m, ValueProp.Move),
        new DynamicVar("StrengthTemp", 1m),
        new DynamicVar("SCells", SCellGrant),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hpLoss = KakarotTrainingSelfHpCost.Resolve(HpCost, Owner.Creature);
        await KakarotBetaCompat.DamageFromCard(
            choiceContext,
            Owner.Creature,
            hpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            this,
            cardPlay);

        await PlayerCmd.GainStars(DynamicVars["Stars"].BaseValue, Owner);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await KakarotPowerCmd.Apply<KakarotTemporaryStrengthPower>(choiceContext,
            Owner.Creature,
            DynamicVars["StrengthTemp"].BaseValue,
            Owner.Creature,
            this);

        await KakarotSCellPower.TryGrantCellsAsync(choiceContext, Owner, SCellGrant, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Stars"].UpgradeValueBy(1m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
