using Godot;
using KakarotMod.KakarotCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotSuperSaiyan4EnergyCapPower : KakarotPower
{
    private const string ReuseIcon = "kakarot_potential_energy_cap_power.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomPackedIconPath =>
        ResourceLoader.Exists(ReuseIcon.PowerImagePath())
            ? ReuseIcon.PowerImagePath()
            : ImageHelper.GetImagePath("atlases/power_atlas.sprites/strength_power.tres");

    public override string CustomBigIconPath =>
        ResourceLoader.Exists(ReuseIcon.BigPowerImagePath())
            ? ReuseIcon.BigPowerImagePath()
            : ImageHelper.GetImagePath("powers/strength_power.png");

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player?.Creature == Owner)
        {
            return amount + Amount;
        }

        return amount;
    }
}
