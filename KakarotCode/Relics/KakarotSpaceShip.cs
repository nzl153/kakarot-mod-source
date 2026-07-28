using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;

namespace KakarotMod.KakarotCode.Relics;

/// <summary>First Skill played each combat: draw 2 cards.</summary>
public sealed class KakarotSpaceShip : KakarotRelic
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            _triggeredThisCombat = false;
            Status = RelicStatus.Normal;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (_triggeredThisCombat)
        {
            return;
        }

        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        _triggeredThisCombat = true;
        Status = RelicStatus.Active;
        Flash();
        await CardPileCmd.Draw(context, 2, Owner);
    }
}
