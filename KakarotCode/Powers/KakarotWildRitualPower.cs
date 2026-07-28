using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using KakarotMod.KakarotCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotWildRitualPower : KakarotPower
{
    private const string DedicatedIcon = "kakarot_wild_ritual_power.png";
    private const string FallbackIcon = "kakarot_god_ki_ritual_power.png";

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomPackedIconPath =>
        ResourceLoader.Exists(DedicatedIcon.PowerImagePath())
            ? DedicatedIcon.PowerImagePath()
            : ResourceLoader.Exists(FallbackIcon.PowerImagePath())
                ? FallbackIcon.PowerImagePath()
                : ImageHelper.GetImagePath("atlases/power_atlas.sprites/strength_power.tres");

    public override string CustomBigIconPath =>
        ResourceLoader.Exists(DedicatedIcon.BigPowerImagePath())
            ? DedicatedIcon.BigPowerImagePath()
            : ResourceLoader.Exists(FallbackIcon.BigPowerImagePath())
                ? FallbackIcon.BigPowerImagePath()
                : ImageHelper.GetImagePath("powers/strength_power.png");

    public int Progress => System.Math.Clamp((int)Amount, 0, 5);
    public override int DisplayAmount => Progress;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Progress", 0m)];

    public void Configure(int plays)
    {
        AssertMutable();
        DynamicVars["Progress"].BaseValue = System.Math.Clamp(plays, 0, 5);
    }

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
