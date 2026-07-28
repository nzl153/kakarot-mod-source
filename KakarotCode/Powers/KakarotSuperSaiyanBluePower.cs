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

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotSuperSaiyanBluePower : KakarotPower
{
    private const int StrengthBonus = 12;
    private const int DexterityBonus = 7;

    private int _strengthBonus = StrengthBonus;
    private int _dexterityBonus = DexterityBonus;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthBonus),
        new DynamicVar("Dexterity", DexterityBonus),
    ];

    public override async Task BeforeApplied(Creature target, decimal amount, Creature applier, CardModel cardSource)
    {
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
}
