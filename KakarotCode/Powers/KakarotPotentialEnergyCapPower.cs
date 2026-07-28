using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>Combat-only max energy increase from Potential Release (separate loc from heart-attack cap).</summary>
public sealed class KakarotPotentialEnergyCapPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player?.Creature == Owner)
        {
            return amount + Amount;
        }

        return amount;
    }
}
