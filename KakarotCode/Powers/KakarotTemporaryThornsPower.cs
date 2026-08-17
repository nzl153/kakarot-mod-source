using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotTemporaryThornsPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Thorns", 0m)];

    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target == Owner && dealer != null && (props.IsPoweredAttack() || cardSource is Omnislice))
        {
            Flash();
            await CreatureCmd.Damage(
                choiceContext,
                dealer,
                Amount,
                ValueProp.Unpowered | ValueProp.SkipHurtAnim,
                Owner);
        }
    }

    public override Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        DynamicVars["Thorns"].BaseValue = amount;
        return Task.CompletedTask;
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        DynamicVars["Thorns"].BaseValue = Amount;
        return base.AfterApplied(applier, cardSource);
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature applier,
        CardModel cardSource)
    {
        DynamicVars["Thorns"].BaseValue = Amount;
        return base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        await PowerCmd.Remove(this);
    }
}
