using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotYouMadeMeMadPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        var stacks = System.Math.Max(0, Amount);
        if (stacks == 0)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainStars(2m * stacks, player);
        await PlayerCmd.GainEnergy(stacks, player);
    }
}
