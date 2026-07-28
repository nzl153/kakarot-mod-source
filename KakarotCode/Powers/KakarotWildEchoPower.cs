using System.Threading.Tasks;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotWildEchoPower : KakarotPower
{
    private bool _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override string SmartDescriptionLocKey => "KAKAROTMOD-KAKAROT_WILD_ECHO_POWER.smartDescription";

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            _triggeredThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_triggeredThisTurn
            || cardPlay.Card.Owner != Owner.Player
            || !KakarotWildHelper.HasWild(cardPlay.Card))
        {
            return;
        }

        _triggeredThisTurn = true;
        Flash();
        await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);
    }
}
