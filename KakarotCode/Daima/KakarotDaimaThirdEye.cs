using System.Threading.Tasks;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace KakarotMod.KakarotCode.Daima;

// Right-click once per combat to enter the Daima route (see KakarotDaimaThirdEyeClickPatch).
public sealed class KakarotDaimaThirdEye : KakarotRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    // Set by the synced action, so every peer agrees on it; saved so full combat state carries it.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    private bool _daimaUsedThisCombat { get; set; }

    public bool UsedThisCombat => _daimaUsedThisCombat;

    // Local-only guard against double clicks before the action runs.
    public bool RequestPending { get; set; }

    public override Task BeforeCombatStart()
    {
        _daimaUsedThisCombat = false;
        RequestPending = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _daimaUsedThisCombat = false;
        RequestPending = false;
        return Task.CompletedTask;
    }

    public void MarkUsed()
    {
        _daimaUsedThisCombat = true;
        RequestPending = false;
        Flash();
    }
}
