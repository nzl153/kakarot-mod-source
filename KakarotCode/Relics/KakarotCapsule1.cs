using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace KakarotMod.KakarotCode.Relics;

public sealed class KakarotCapsule1 : KakarotRelic
{
    private const decimal HealAmount = 6m;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not RestSiteRoom)
        {
            return;
        }

        if (Owner?.Creature == null || Owner.Creature.CurrentHp >= Owner.Creature.MaxHp)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner.Creature, HealAmount);
    }
}
