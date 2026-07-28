using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Helpers;

internal static class KakarotCardSelectHelper
{
    /// <summary>LocString ctor is (locTable, locEntryKey). Reversed args throw at runtime (empty loc table).</summary>
    private static readonly LocString HandSelectPrompt = new("combat_messages", "KAKAROTMOD_HAND_SELECT");

    private static readonly LocString DrawPileSelectPrompt = new("combat_messages", "KAKAROTMOD_DRAW_SELECT");

    internal static async Task<IEnumerable<CardModel>> FromHandForDiscardAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int cardsToPick,
        AbstractModel source)
    {
        var prefs = new CardSelectorPrefs(HandSelectPrompt, cardsToPick);
        return await CardSelectCmd.FromHandForDiscard(choiceContext, player, prefs, null, source);
    }

    internal static async Task<IEnumerable<CardModel>> FromHandSelectAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int cardsToPick,
        AbstractModel source)
    {
        var prefs = new CardSelectorPrefs(HandSelectPrompt, cardsToPick);
        return await CardSelectCmd.FromHand(choiceContext, player, prefs, null, source);
    }

    /// <summary>Select between <paramref name="minPick"/> and <paramref name="maxPick"/> cards from hand (inclusive). When <paramref name="minPick"/> is 0, uses discard-style selection so “confirm with 0 picks” works.</summary>
    internal static async Task<IEnumerable<CardModel>> FromHandSelectRangeAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int minPick,
        int maxPick,
        AbstractModel source)
    {
        var prefs = new CardSelectorPrefs(HandSelectPrompt, minPick, maxPick);
        if (minPick == 0)
        {
            return await CardSelectCmd.FromHandForDiscard(choiceContext, player, prefs, null, source);
        }

        return await CardSelectCmd.FromHand(choiceContext, player, prefs, null, source);
    }

    internal static async Task<IEnumerable<CardModel>> FromDrawPileSimpleGridAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int pickCount,
        AbstractModel source)
    {
        var drawPile = player.PlayerCombatState.DrawPile;
        var draw = drawPile.Cards;
        if (draw.Count == 0)
        {
            return [];
        }

        var prefs = new CardSelectorPrefs(DrawPileSelectPrompt, pickCount);
        var chosen = await CardSelectCmd.FromSimpleGrid(choiceContext, draw, player, prefs);
        var list = chosen.ToList();
        foreach (var card in list)
        {
            if (card.Pile == drawPile)
            {
                await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source, skipVisuals: false);
            }
        }

        return list;
    }
}
