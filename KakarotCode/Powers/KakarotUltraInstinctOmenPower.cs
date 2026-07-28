using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotUltraInstinctOmenPower : KakarotPower
{
    private const int StatBonus = 15;
    private const int DodgeChance = 50;

    private int _strengthBonus = StatBonus;
    private int _dexterityBonus = StatBonus;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StatBonus),
        new DynamicVar("Dexterity", StatBonus),
        new DynamicVar("DodgePercent", DodgeChance),
    ];

    public override async Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        // All major forms are mutually exclusive. Enforce again at power-apply layer
        // so non-card application paths also remain safe.
        await KakarotUltraInstinctTriggerHelper.RemoveSaiyanTransformsForUltraInstinct(target);

        await KakarotPowerCmd.Apply<StrengthPower>(target, _strengthBonus, target, cardSource);
        await KakarotPowerCmd.Apply<DexterityPower>(target, _dexterityBonus, target, cardSource);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        KakarotFormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(oldOwner, -_strengthBonus, oldOwner, null);
        await KakarotPowerCmd.Apply<DexterityPower>(oldOwner, -_dexterityBonus, oldOwner, null);
        KakarotFormVisuals.Refresh(oldOwner);
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || amount <= 0)
        {
            return 1m;
        }

        var player = Owner.Player;
        var rollIndex = KakarotUltraInstinctTriggerHelper.GetDamageDodgeRollIndex(Owner);
        if (player == null || !KakarotUltraInstinctTriggerHelper.RollDeterministic(DodgeChance, player, amount, salt: 2, rollIndex))
        {
            return 1m;
        }

        return 0m;
    }
}
