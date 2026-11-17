using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KakarotMod.KakarotCode.DragonBalls;

internal static class KakarotDragonBallSelection
{
    public static async Task Choose(KakarotSevenDragonBalls relic)
    {
        var player = relic.Owner;
        List<CardModel> cards =
        [
            ModelDb.Card<KakarotShenronEarth>().ToMutable(),
            ModelDb.Card<KakarotShenronNamek>().ToMutable(),
            ModelDb.Card<KakarotShenronSuper>().ToMutable(),
            ModelDb.Card<KakarotShenronBlackStar>().ToMutable(),
        ];
        foreach (var card in cards)
        {
            card.Owner = player;
        }

        var selected = await ChooseOne(player, cards, new BlockingPlayerChoiceContext(),
            new LocString("gameplay_ui", "KAKAROTMOD-DRAGON_BALL_CHOOSE_HEADER"));
        relic.SelectSet((KakarotDragonBallSet)(cards.IndexOf(selected) + 1));
    }

    internal static async Task<CardModel> ChooseOne(Player player, List<CardModel> cards,
        PlayerChoiceContext context, LocString banner)
    {
        var run = RunManager.Instance;

#if STS2_V113
        var selector = run.CardSelectorStack.GetSelector(player);
#else
        var selector = CardSelectCmd.Selector;
#endif
        int index;
        if (selector != null)
        {
            index = cards.IndexOf((await selector.GetSelectedCards(cards, 1, 1)).Single());
        }
        else
        {
            uint choiceId = run.PlayerChoiceSynchronizer.ReserveChoiceId(player);
#if STS2_V109
            await context.SignalPlayerChoiceBegun(player, PlayerChoiceOptions.None);
#else
            await context.SignalPlayerChoiceBegun(PlayerChoiceOptions.None);
#endif
            if (LocalContext.IsMe(player) && run.NetService.Type != NetGameType.Replay)
            {
#if STS2_V113
                var localSelector = run.CardSelectorStack.LocalSelector;
#elif STS2_V109
                var localSelector = CardSelectCmd.LocalSelector;
#endif
                CardModel selected;
#if STS2_V109
                if (localSelector != null)
                {
                    selected = (await localSelector.GetSelectedCards(cards, 1, 1)).Single();
                }
                else
#endif
                {
                    NPlayerHand.Instance?.CancelAllCardPlay();
                    var screen = NChooseACardSelectionScreen.ShowScreen(cards, canSkip: false);
                    screen.GetNode<NCommonBanner>("Banner").label.SetTextAutoSize(
                        banner.GetFormattedText());
                    selected = (await screen.CardsSelected()).Single();
                }

                index = cards.IndexOf(selected);
                run.PlayerChoiceSynchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(index));
            }
            else
            {
                index = (await run.PlayerChoiceSynchronizer.WaitForRemoteChoice(player, choiceId)).AsIndex();
            }

            await context.SignalPlayerChoiceEnded();
        }

        if (index < 0 || index >= cards.Count)
        {
            throw new InvalidOperationException($"Invalid Dragon Ball set choice: {index}");
        }

        return cards[index];
    }
}
