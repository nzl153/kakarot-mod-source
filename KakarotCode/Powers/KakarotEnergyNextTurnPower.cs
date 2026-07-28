using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>Next turn start (energy reset): gain energy once then remove.</summary>
public sealed class KakarotEnergyNextTurnPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player == Owner.Player)
        {
            await PlayerCmd.GainEnergy(Amount, player);
            await PowerCmd.Remove(this);
        }
    }
}
