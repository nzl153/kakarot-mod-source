using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using KakarotMod.KakarotCode.Cards.Rare;
using KakarotMod.KakarotCode.Cards.Uncommon;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KakarotMod.KakarotCode.Relics;

public sealed class KakarotDragonRadar : KakarotRelic
{
    private static readonly LocString PickPrompt = new("combat_messages", "KAKAROTMOD_DRAGON_RADAR_PICK");

    private static readonly Lazy<string[]> WishEntries = new(() =>
    [
        ModelDb.Card<KakarotShenronWishWin>().Id.Entry!,
        ModelDb.Card<KakarotShenronWishLife>().Id.Entry!,
        ModelDb.Card<KakarotShenronWishDraw>().Id.Entry!,
        ModelDb.Card<KakarotShenronWishBurn>().Id.Entry!,
        ModelDb.Card<KakarotShenronWishTransform>().Id.Entry!,
        ModelDb.Card<KakarotShenronWishCard>().Id.Entry!,
        ModelDb.Card<KakarotShenronWishPotion>().Id.Entry!,
        ModelDb.Card<KakarotDragonBallWishMoney>().Id.Entry!,
    ]);

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsAllowedInShops => false;

    public override async Task AfterObtained()
    {
        if (Owner == null)
        {
            return;
        }

        // The map overlay otherwise covers the card-selection screen.
        var mapScreen = NMapScreen.Instance;
        var mapWasOpen = mapScreen?.IsOpen == true;
        if (mapWasOpen)
        {
            mapScreen.Close(animateOut: false);
        }

        // Defer one frame so non-combat acquisition paths can mount the selection UI.
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        var gridCards = BuildWishGridCards();
        var prefs = new CardSelectorPrefs(PickPrompt, 1) { RequireManualConfirmation = true };
        var ctx = new BlockingPlayerChoiceContext();
        var picked = (await CardSelectCmd.FromSimpleGrid(ctx, gridCards, Owner, prefs)).FirstOrDefault();

        if (mapWasOpen)
        {
            mapScreen.Open();
        }

        if (picked == null)
        {
            return;
        }

        var idx = IndexOfWish(picked);
        if (idx < 0)
        {
            return;
        }

        Flash();

        // CreateCard requires the canonical ModelDb instance.
        var toAdd = Owner.RunState.CreateCard(WishCanonical(idx), Owner);
        var addResult = await CardPileCmd.Add(toAdd, Owner.Deck, CardPilePosition.Bottom, this, skipVisuals: false);
        // Map acquisition has no NCard node, so refresh the top-bar deck count explicitly.
        if (addResult.success)
        {
            Owner.Deck.InvokeCardAddFinished();
        }
    }

    private static CardModel WishCanonical(int idx) =>
        idx switch
        {
            0 => ModelDb.Card<KakarotShenronWishWin>(),
            1 => ModelDb.Card<KakarotShenronWishLife>(),
            2 => ModelDb.Card<KakarotShenronWishDraw>(),
            3 => ModelDb.Card<KakarotShenronWishBurn>(),
            4 => ModelDb.Card<KakarotShenronWishTransform>(),
            5 => ModelDb.Card<KakarotShenronWishCard>(),
            6 => ModelDb.Card<KakarotShenronWishPotion>(),
            7 => ModelDb.Card<KakarotDragonBallWishMoney>(),
            _ => ModelDb.Card<KakarotShenronWishWin>(),
        };

    private static List<CardModel> BuildWishGridCards()
    {
        return new List<CardModel>(8)
        {
            ModelDb.Card<KakarotShenronWishWin>(),
            ModelDb.Card<KakarotShenronWishLife>(),
            ModelDb.Card<KakarotShenronWishDraw>(),
            ModelDb.Card<KakarotShenronWishBurn>(),
            ModelDb.Card<KakarotShenronWishTransform>(),
            ModelDb.Card<KakarotShenronWishCard>(),
            ModelDb.Card<KakarotShenronWishPotion>(),
            ModelDb.Card<KakarotDragonBallWishMoney>(),
        };
    }

    private static int IndexOfWish(CardModel picked)
    {
        var entry = picked.Id.Entry;
        if (string.IsNullOrEmpty(entry))
        {
            return -1;
        }

        for (var i = 0; i < WishEntries.Value.Length; i++)
        {
            if (WishEntries.Value[i] == entry)
            {
                return i;
            }
        }

        return -1;
    }

}
