using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>
/// 保留此类型以兼容现有存档和显式的 <c>PowerCmd.Remove</c> 调用。
/// </summary>
public sealed class KakarotUltraInstinctTurnCleanupPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            KakarotUltraInstinctCombatState.OnPlayerTurnStarted(player);
        }

        return Task.CompletedTask;
    }
}
