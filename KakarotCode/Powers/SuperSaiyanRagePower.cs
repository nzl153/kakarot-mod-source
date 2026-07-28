using System.Threading.Tasks;
using Godot;
using KakarotMod.KakarotCode.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Powers;

// Form removal must not remove rage granted by unrelated powers.
public sealed class SuperSaiyanRagePower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomPackedIconPath =>
        ResourceLoader.Exists("kakarot_rage_aura_power.png".PowerImagePath())
            ? "kakarot_rage_aura_power.png".PowerImagePath()
            : ImageHelper.GetImagePath("atlases/power_atlas.sprites/strength_power.tres");

    public override string CustomBigIconPath =>
        ResourceLoader.Exists("kakarot_rage_aura_power.png".BigPowerImagePath())
            ? "kakarot_rage_aura_power.png".BigPowerImagePath()
            : ImageHelper.GetImagePath("powers/strength_power.png");

    public override async Task AfterSideTurnStart(CombatSide side, System.Collections.Generic.IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (side != Owner.Side || Owner.Player == null)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainStars(Amount, Owner.Player);
    }
}
