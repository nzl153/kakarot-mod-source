using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KakarotMod.KakarotCode.Powers;

// God Ki bonuses live on SaiyanBlood so rollback snapshots restore them.
public static class KakarotUltraInstinctCombatState
{
    public static int GetGodKiBonusPercent(Player player)
    {
        return SaiyanBlood.ResolveBloodlineRelic(player)?.GodKiBonusPercentThisTurn ?? 0;
    }

    public static void OnGodKiPlayed(Player player)
    {
        SaiyanBlood.ResolveBloodlineRelic(player)?.AddGodKiBonusPercentThisTurn(5);
    }

    public static void OnPlayerTurnStarted(Player player)
    {
        SaiyanBlood.ResolveBloodlineRelic(player)?.ResetGodKiBonusPercentThisTurn();
    }

    public static void ResetForNewCombat(Player player)
    {
        SaiyanBlood.ResolveBloodlineRelic(player)?.ResetGodKiBonusPercentThisTurn();
    }
}
