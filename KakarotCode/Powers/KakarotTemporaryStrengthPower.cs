using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

// Separate from StrengthPower to avoid duplicate icons and double rollback.
public sealed class KakarotTemporaryStrengthPower : KakarotPower
{
    public override string CustomPackedIconPath =>
        ImageHelper.GetImagePath("atlases/power_atlas.sprites/strength_power.tres");

    public override string CustomBigIconPath =>
        ImageHelper.GetImagePath("powers/strength_power.png");

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Strength", 0m)];

#if STS2_BETA
    public override decimal ModifyDamageAdditive(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource, CardPlay cardPlay)
#else
    public override decimal ModifyDamageAdditive(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
#endif
    {
        if (dealer == null || Owner != dealer || !props.IsPoweredAttack())
        {
            return 0m;
        }

        return Amount;
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        DynamicVars["Strength"].BaseValue = Amount;
        return base.AfterApplied(applier, cardSource);
    }

    public override Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        DynamicVars["Strength"].BaseValue = amount;
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        DynamicVars["Strength"].BaseValue = Amount;
        return base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, System.Collections.Generic.IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != Owner.Side)
        {
            return;
        }

        Flash();
        await PowerCmd.Remove(this);
    }
}
