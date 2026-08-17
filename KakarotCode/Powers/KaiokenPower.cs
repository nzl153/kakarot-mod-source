using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KaiokenPower : KakarotPower
{
    private int _castCount = 1;
    private int _strengthBonus = 2;
    private int _dexterityBonus;
    private int _hpLossPerTurn = 2;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => _castCount;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", 2m),
        new DynamicVar("Dexterity", 0m),
        new DynamicVar("HpLoss", 2m),
    ];

    public void Configure(int strengthBonus, int dexterityBonus, int hpLossPerTurn)
    {
        ConfigureTotals(strengthBonus, dexterityBonus, hpLossPerTurn, 1);
    }

    public void ConfigureTotals(int totalStrengthBonus, int totalDexterityBonus, int totalHpLossPerTurn, int castCount)
    {
        AssertMutable();
        _strengthBonus = totalStrengthBonus;
        _dexterityBonus = totalDexterityBonus;
        _hpLossPerTurn = totalHpLossPerTurn;
        _castCount = Math.Max(1, castCount);
        DynamicVars["Strength"].BaseValue = totalStrengthBonus;
        DynamicVars["Dexterity"].BaseValue = totalDexterityBonus;
        DynamicVars["HpLoss"].BaseValue = totalHpLossPerTurn;
    }

    public int CastCount => _castCount;
    public int StrengthBonus => _strengthBonus;
    public int DexterityBonus => _dexterityBonus;
    public int HpLossPerTurn => _hpLossPerTurn;

    public override async Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(target, _strengthBonus, target, cardSource);

        if (_dexterityBonus > 0)
        {
            await KakarotPowerCmd.Apply<DexterityPower>(target, _dexterityBonus, target, cardSource);
        }
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        KakarotFormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, System.Collections.Generic.IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != Owner.Side)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(choiceContext, Owner, _hpLossPerTurn, ValueProp.Unblockable | ValueProp.Unpowered, Owner);
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(oldOwner, -_strengthBonus, oldOwner, null);

        if (_dexterityBonus > 0)
        {
            await KakarotPowerCmd.Apply<DexterityPower>(oldOwner, -_dexterityBonus, oldOwner, null);
        }

        KakarotFormVisuals.Refresh(oldOwner);
    }
}
