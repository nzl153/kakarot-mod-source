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

/// <summary>Marker: you played 领悟极易; adds +15% to the Ultra Instinct proc roll until it fires.</summary>
public sealed class KakarotInsightUltraInstinctPower : KakarotPower
{
    private const int BonusPerStack = 15;

    /// <summary>与其它自在极意系 Power 一致；缺省时 ModelDb 会报「同型共享 ID」警告。</summary>
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
