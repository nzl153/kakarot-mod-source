using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>
/// At the start of your turn, gain 1 Justice Heart ritual progress.
/// Amount is used as remaining turns and counts down each trigger.
/// </summary>
public sealed class KakarotInsightDivineRealmPower : KakarotPower
{
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0)
        {
            return;
        }

        Flash();
        await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(choiceContext, Owner.Player, null, 1);

        var turnsRemaining = (int)Amount;
        if (turnsRemaining <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.Decrement(this);
    }
}
