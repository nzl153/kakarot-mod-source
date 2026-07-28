using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
namespace KakarotMod.KakarotCode.Powers;

/// <summary>
/// Combat-only: tracks Justice Heart progress toward the next Super Saiyan God transformation card.
/// </summary>
public sealed class KakarotJusticeHeartRitualPower : KakarotPower
{
    private const int MaxProgress = 5;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int Progress => System.Math.Clamp((int)Amount, 0, MaxProgress);
    public override int DisplayAmount => Progress;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Progress", 0m)];

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        DynamicVars["Progress"].BaseValue = Progress;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        return Task.CompletedTask;
    }
}
