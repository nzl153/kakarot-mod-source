using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>
/// 每回合「神之气」触发率加成的存取入口。状态存放于玩家遗物实例中，以参与战斗快照。
/// </summary>
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
