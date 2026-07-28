using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotPowerOfWildnessPower : KakarotPower
{
    public const decimal BaseBlockPerStack = 4m;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => StackCount;

    private int StackCount => Math.Max(1, (int)Amount);
    private decimal StrengthGain => Math.Max(0m, Amount);
    private decimal BlockGain => (Math.Max(0m, Amount) * BaseBlockPerStack) + BonusBlockGain;
    private decimal BonusBlockGain => Owner.GetPower<KakarotPowerOfWildnessBonusBlockPower>()?.Amount ?? 0m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthGain", 1m),
        new DynamicVar("BlockGain", 4m),
    ];

    protected override string SmartDescriptionLocKey => "KAKAROTMOD-KAKAROT_POWER_OF_WILDNESS_POWER.smartDescription";

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        RefreshDisplay();
        return base.AfterApplied(applier, cardSource);
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (ReferenceEquals(power, this)
            || (power is KakarotPowerOfWildnessBonusBlockPower && power.Owner == Owner))
        {
            RefreshDisplay();
        }

        return base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }

    private void RefreshDisplay()
    {
        DynamicVars["StrengthGain"].BaseValue = StrengthGain;
        DynamicVars["BlockGain"].BaseValue = BlockGain;
        InvokeDisplayAmountChanged();
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await PowerCmd.Remove<KakarotPowerOfWildnessBonusBlockPower>(oldOwner);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || !KakarotWildHelper.HasWild(cardPlay.Card))
        {
            return;
        }

        Flash();
        if (StrengthGain > 0)
        {
            await KakarotPowerCmd.Apply<KakarotTemporaryStrengthPower>(choiceContext, Owner, StrengthGain, Owner, null);
        }

        if (BlockGain > 0)
        {
            await CreatureCmd.GainBlock(Owner, BlockGain, ValueProp.Unpowered, null);
        }
    }
}
