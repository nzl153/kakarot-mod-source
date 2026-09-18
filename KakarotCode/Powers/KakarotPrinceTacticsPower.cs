using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotPrinceTacticsPower : KakarotPower
{
    public const decimal BaseDamagePerStack = 2m;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => StackCount;

    private int StackCount => Math.Max(1, (int)Amount);
    private decimal Damage => (Math.Max(0m, Amount) * BaseDamagePerStack) + BonusDamage;
    private decimal BonusDamage => Owner.GetPower<KakarotPrinceTacticsBonusDamagePower>()?.Amount ?? 0m;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Damage", 2m)];

    protected override string SmartDescriptionLocKey => "KAKAROTMOD-KAKAROT_PRINCE_TACTICS_POWER.smartDescription";

    public override System.Threading.Tasks.Task AfterApplied(Creature applier, CardModel cardSource)
    {
        RefreshDisplay();
        return base.AfterApplied(applier, cardSource);
    }

    public override System.Threading.Tasks.Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (ReferenceEquals(power, this)
            || (power is KakarotPrinceTacticsBonusDamagePower && power.Owner == Owner))
        {
            RefreshDisplay();
        }

        return base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }

    private void RefreshDisplay()
    {
        DynamicVars["Damage"].BaseValue = Damage;
        InvokeDisplayAmountChanged();
    }

    public override async System.Threading.Tasks.Task AfterRemoved(Creature oldOwner)
    {
        await PowerCmd.Remove<KakarotPrinceTacticsBonusDamagePower>(oldOwner);
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner?.Creature != Owner || CombatState.HittableEnemies.Count == 0)
        {
            return;
        }

        var hash = (int)Owner.Player.NetId;
        hash = (hash * 397) ^ (Owner.CombatState?.RoundNumber ?? 0);
        hash = (hash * 397) ^ KakarotUltraInstinctTriggerHelper.StableStringHash(card.Id?.Entry);
        var idx = (hash & 0x7FFFFFFF) % CombatState.HittableEnemies.Count;
        var target = CombatState.HittableEnemies[idx];
        Flash();
        await CreatureCmd.Damage(choiceContext, target, Damage, ValueProp.Unpowered, Owner);
    }
}
