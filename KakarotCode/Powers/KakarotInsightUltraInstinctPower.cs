using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotInsightUltraInstinctPower : KakarotPower
{
    private const int BonusPerStack = 15;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int TotalBonus => BonusPerStack * System.Math.Max(1, Amount);

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Bonus", BonusPerStack)];

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        RefreshBonusDisplay();
        return base.AfterApplied(applier, cardSource);
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            RefreshBonusDisplay();
        }

        return base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }

    private void RefreshBonusDisplay()
    {
        DynamicVars["Bonus"].BaseValue = TotalBonus;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        return Task.CompletedTask;
    }
}
