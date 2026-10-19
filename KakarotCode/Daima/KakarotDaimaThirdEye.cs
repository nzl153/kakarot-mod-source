using System.Threading.Tasks;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace KakarotMod.KakarotCode.Daima;

// Right-click once per combat to enter the Daima route (see KakarotDaimaThirdEyeClickPatch).
public sealed class KakarotDaimaThirdEye : KakarotRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    // Set by the synced action, so every peer agrees on it.
    public bool UsedThisCombat { get; private set; }

    // Local-only guard against double clicks before the action runs.
    public bool RequestPending { get; set; }

    public override Task BeforeCombatStart()
    {
        UsedThisCombat = false;
        RequestPending = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        UsedThisCombat = false;
        RequestPending = false;
        return Task.CompletedTask;
    }

    public void MarkUsed()
    {
        UsedThisCombat = true;
        RequestPending = false;
        Flash();
    }
}
