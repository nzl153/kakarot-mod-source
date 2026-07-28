using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>
/// Combat-only marker: you played Summon Ruyi Staff (no mechanical effect).
/// </summary>
public sealed class KakarotRuyiStaffSummonedPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}
