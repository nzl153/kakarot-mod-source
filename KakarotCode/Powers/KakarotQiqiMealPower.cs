using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotQiqiMealPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner, Amount);

        var next = Amount - 1;
        await PowerCmd.Remove(this);
        if (next > 0)
        {
            await KakarotPowerCmd.Apply<KakarotQiqiMealPower>(choiceContext, Owner, next, Owner, null);
        }
    }
}
