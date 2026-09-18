using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotMod.KakarotCode.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Helpers;

// Retrieves existing progression cards without creating duplicate instances.
internal static class KakarotSuperSaiyanRefundCards
{
    // Never issue a draw-to-draw move; the engine can detach that card from the run.
    internal static async Task TryRetrieveChargeUpToHand(Player player, AbstractModel source)
    {
        var cs = player.PlayerCombatState;
        var chargeUpId = ModelDb.Card<KakarotChargeUp>().Id;
        var chargeUpEntry = chargeUpId.Entry;
        bool IsChargeUp(CardModel c) =>
            c != null && (c.Id == chargeUpId || (c.Id.Entry != null && c.Id.Entry == chargeUpEntry));

        foreach (var pile in new[] { cs.DrawPile, cs.DiscardPile, cs.ExhaustPile, cs.Hand })
        {
            var card = pile.Cards.FirstOrDefault(IsChargeUp);
            if (card == null) continue;

            if (pile == cs.Hand) return;

            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            return;
        }

        if (cs.AllCards.Any(IsChargeUp)) return;

        var deckCard = player.Deck?.Cards?.FirstOrDefault(IsChargeUp);
        if (deckCard == null) return;

        await CardPileCmd.Add(deckCard, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
    }

    // First-turn recovery only moves a buried transform card; normal delivery remains authoritative.
    internal static async Task TryRetrieveTransformToHandIfBuriedAsync(Player player, AbstractModel source)
    {
        if (!CombatManager.Instance.IsInProgress || player?.PlayerCombatState is not { } cs)
        {
            return;
        }

        var transformId = ModelDb.Card<KakarotSuperSaiyanTransform>().Id;
        var transformEntry = transformId.Entry;
        bool IsTransform(CardModel c) =>
            c != null && (c.Id == transformId || (c.Id.Entry != null && c.Id.Entry == transformEntry));

        if (cs.Hand.Cards.Any(IsTransform))
        {
            return;
        }

        bool InStandardCombatPile(CardModel card) =>
            card != null && (
                cs.Hand.Cards.Contains(card) ||
                cs.DrawPile.Cards.Contains(card) ||
                cs.DiscardPile.Cards.Contains(card) ||
                cs.ExhaustPile.Cards.Contains(card));

        foreach (var pile in new[] { cs.DrawPile, cs.DiscardPile, cs.ExhaustPile })
        {
            var card = pile.Cards.FirstOrDefault(IsTransform);
            if (card == null) continue;

            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            return;
        }

        var limbo = cs.AllCards?.FirstOrDefault(c => IsTransform(c) && !InStandardCombatPile(c));
        if (limbo != null)
        {
            await CardPileCmd.Add(limbo, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            return;
        }

        var fromDeck = player.Deck?.Cards?.FirstOrDefault(IsTransform);
        if (fromDeck == null)
        {
            return;
        }

        await CardPileCmd.Add(fromDeck, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
    }
}
