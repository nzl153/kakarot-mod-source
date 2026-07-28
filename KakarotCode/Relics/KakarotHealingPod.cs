using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace KakarotMod.KakarotCode.Relics;

/// <summary>At the start of each combat, heal 4 HP.</summary>
public sealed class KakarotHealingPod : KakarotRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom)
        {
            return;
        }

        if (Owner?.Creature?.CurrentHp < Owner?.Creature?.MaxHp)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, 4m);
        }
    }
}
