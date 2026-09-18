using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;

namespace KakarotMod.KakarotCode.Relics;

public sealed class KakarotSacredWaterJug : KakarotRelic
{
    private CardType? _lastQualifyingType;
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            ResetTurnState();
        }

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            ResetTurnState();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner)
        {
            return;
        }

        var currentType = cardPlay.Card.Type;
        if (currentType is not CardType.Attack and not CardType.Skill)
        {
            return;
        }

        var alternated = _lastQualifyingType.HasValue &&
                         _lastQualifyingType.Value != currentType;
        _lastQualifyingType = currentType;

        if (_triggeredThisTurn || !alternated)
        {
            return;
        }

        // Commit deterministic state before any command yields.
        _triggeredThisTurn = true;
        Status = RelicStatus.Active;
        Flash();
        await PlayerCmd.GainStars(1m, Owner);
        await CardPileCmd.Draw(context, 1, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTurnState();
        return Task.CompletedTask;
    }

    private void ResetTurnState()
    {
        _lastQualifyingType = null;
        _triggeredThisTurn = false;
        Status = RelicStatus.Normal;
    }
}
