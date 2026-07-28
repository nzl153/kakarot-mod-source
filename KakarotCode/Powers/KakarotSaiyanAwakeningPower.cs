using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotSaiyanAwakeningPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        var n = (int)Amount;
        if (n <= 0)
        {
            return;
        }

        await CardPileCmd.AutoPlayFromDrawPile(choiceContext, player, n, CardPilePosition.Top, false);
    }
}
