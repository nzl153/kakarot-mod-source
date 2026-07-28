using MegaCrit.Sts2.Core.Entities.Powers;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>Hidden synchronized storage for upgraded Wild Power block bonuses.</summary>
public sealed class KakarotPowerOfWildnessBonusBlockPower : KakarotPower
{
    public override PowerInstanceType InstanceType => PowerInstanceType.None;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
}
