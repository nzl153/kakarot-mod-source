using KakarotMod.KakarotCode.Cards.Curse;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Encounters;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Relics;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace KakarotMod.KakarotCode.DragonBalls;

internal static class KakarotDragonBallWishes
{
    internal static async Task ChooseAndResolve(KakarotSevenDragonBalls relic, PlayerChoiceContext context)
    {
        var player = relic.Owner;
        List<CardModel> options = relic.SelectedSet switch
        {
            KakarotDragonBallSet.Earth =>
            [
                ModelDb.Card<KakarotWishEarthHeal>().ToMutable(),
                ModelDb.Card<KakarotWishEarthRemove>().ToMutable(),
                ModelDb.Card<KakarotWishEarthUpgrade>().ToMutable(),
                ModelDb.Card<KakarotWishEarthGold>().ToMutable(),
                ModelDb.Card<KakarotWishEarthSlay>().ToMutable(),
            ],
            KakarotDragonBallSet.Namek =>
            [
                ModelDb.Card<KakarotWishNamekHeal>().ToMutable(),
                ModelDb.Card<KakarotWishNamekUpgrade>().ToMutable(),
                ModelDb.Card<KakarotWishNamekRemove>().ToMutable(),
                ModelDb.Card<KakarotWishNamekGold>().ToMutable(),
                ModelDb.Card<KakarotWishNamekStrength>().ToMutable(),
                ModelDb.Card<KakarotWishNamekEnergy>().ToMutable(),
            ],
            KakarotDragonBallSet.Super =>
            [
                ModelDb.Card<KakarotWishSuperEnergy>().ToMutable(),
                ModelDb.Card<KakarotWishSuperRitual>().ToMutable(),
                ModelDb.Card<KakarotWishSuperVitality>().ToMutable(),
                ModelDb.Card<KakarotWishSuperBasics>().ToMutable(),
            ],
            KakarotDragonBallSet.BlackStar =>
            [
                ModelDb.Card<KakarotWishBlackStarDraw>().ToMutable(),
                ModelDb.Card<KakarotWishBlackStarPower>().ToMutable(),
                ModelDb.Card<KakarotWishBlackStarRelics>().ToMutable(),
            ],
            _ => throw new InvalidOperationException($"No Dragon Ball set selected: {relic.SelectedSet}"),
        };

        if (player.RunState.CurrentRoom is CombatRoom room
            && (room.RoomType == RoomType.Boss || room.Encounter is FriezaBossEncounter))
        {
            options.RemoveAll(card => card is KakarotWishEarthSlay);
        }

        foreach (var card in options)
        {
            card.Owner = player;
        }

        List<CardModel> selected;
        if (relic.SelectedSet == KakarotDragonBallSet.Namek)
        {
            var prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "KAKAROTMOD-DRAGON_BALL_THREE_WISHES_HEADER"), 3)
            {
                RequireManualConfirmation = true,
                Cancelable = false,
            };
            selected = (await CardSelectCmd.FromSimpleGrid(context, options, player, prefs)).ToList();
            if (selected.Count != 3 || selected.Distinct().Count() != 3)
            {
                throw new InvalidOperationException("Porunga requires exactly three different wishes.");
            }
        }
        else
        {
            selected = [await KakarotDragonBallSelection.ChooseOne(player, options, context,
                new LocString("gameplay_ui", "KAKAROTMOD-DRAGON_BALL_WISH_HEADER"))];
        }

        var wishes = selected.Cast<KakarotWishCard>().OrderBy(card => card.Wish).ToList();

        // 要玩家选牌的步骤全部先做完，再标记已许愿并一口气结算。
        // 等待选择时行动是暂停的，联机里队友这时打完战斗会把它取消；取消发生在标记之前才不会丢愿望。
        var picks = new Dictionary<KakarotWish, List<CardModel>>();
        foreach (var card in wishes)
        {
            if (card.Wish is KakarotWish.EarthRemove or KakarotWish.NamekRemove)
            {
                picks[card.Wish] = await SelectFromDeck(player, context, card.DynamicVars["Cards"].IntValue, upgrade: false);
            }
            else if (card.Wish is KakarotWish.EarthUpgrade or KakarotWish.NamekUpgrade)
            {
                picks[card.Wish] = await SelectFromDeck(player, context, card.DynamicVars["Cards"].IntValue, upgrade: true);
            }
        }

        relic.MarkWished(wishes.Aggregate(KakarotWish.None, (all, card) => all | card.Wish));
        foreach (var card in wishes)
        {
            await Resolve(player, context, card, picks.GetValueOrDefault(card.Wish));
        }

        if (relic.SelectedSet == KakarotDragonBallSet.BlackStar)
        {
            var curse = player.RunState.CreateCard<KakarotEvilDesire>(player);
            await CardPileCmd.Add(curse, player.Deck);
        }
    }

    private static async Task Resolve(Player player, PlayerChoiceContext context, KakarotWishCard card,
        List<CardModel> picked)
    {
        var creature = player.Creature;
        switch (card.Wish)
        {
            case KakarotWish.EarthHeal:
                await CreatureCmd.Heal(creature, creature.MaxHp);
                break;
            case KakarotWish.NamekHeal:
                await CreatureCmd.Heal(creature, card.DynamicVars["Heal"].BaseValue);
                break;
            case KakarotWish.EarthRemove:
            case KakarotWish.NamekRemove:
                await ChangeDeck(player, picked, upgrade: false);
                break;
            case KakarotWish.EarthUpgrade:
            case KakarotWish.NamekUpgrade:
                await ChangeDeck(player, picked, upgrade: true);
                break;
            case KakarotWish.EarthGold:
            case KakarotWish.NamekGold:
                await PlayerCmd.GainGold(card.DynamicVars["Gold"].BaseValue, player);
                break;
            case KakarotWish.EarthSlay:
                await CardPileCmd.AddGeneratedCardToCombat(
                    creature.CombatState.CreateCard<KakarotShenronSlay>(player), PileType.Hand, player);
                break;
            case KakarotWish.NamekStrength:
                await KakarotPowerCmd.Apply<StrengthPower>(context, creature,
                    card.DynamicVars["Strength"].BaseValue, creature, null);
                break;
            case KakarotWish.NamekEnergy:
                await PlayerCmd.GainEnergy(card.DynamicVars["Energy"].BaseValue, player);
                await CardPileCmd.Draw(context, card.DynamicVars["Cards"].BaseValue, player);
                break;
            case KakarotWish.SuperRitual:
                await GrantRituals(context, player);
                break;
            case KakarotWish.SuperVitality:
                await CreatureCmd.GainMaxHp(creature, card.DynamicVars["MaxHp"].BaseValue);
                break;
            case KakarotWish.BlackStarPower:
                await GrantBlackStarPower(context, player);
                break;
            case KakarotWish.BlackStarRelics:
                for (int i = 0; i < card.DynamicVars["Relics"].IntValue; i++)
                {
                    // 龙珠雷达拿到时要弹选牌界面，战斗中的许愿行动里不能弹。
                    var relic = RelicFactory.PullNextRelicFromFront(player, RelicRarity.Rare,
                        relic => relic.Rarity == RelicRarity.Rare && relic is not KakarotDragonRadar
                            && player.Relics.All(owned => owned.Id != relic.Id));
                    await RelicCmd.Obtain(relic.ToMutable(), player);
                }
                break;
            case KakarotWish.SuperEnergy:
            case KakarotWish.SuperBasics:
            case KakarotWish.BlackStarDraw:
                break;
            default:
                throw new InvalidOperationException($"Unknown wish: {card.Wish}");
        }
    }

    internal static async Task GrantRituals(PlayerChoiceContext context, Player player)
    {
        int amount = ModelDb.Card<KakarotWishSuperRitual>().DynamicVars["Ritual"].IntValue;
        await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(context, player, null, amount);
        await KakarotWildRitualHandler.AddRitualProgress(context, player, null, amount);
        await KakarotSCellPower.TryGrantCellsAsync(context, player, amount, null);
    }

    internal static async Task GrantBlackStarPower(PlayerChoiceContext context, Player player)
    {
        var vars = ModelDb.Card<KakarotWishBlackStarPower>().DynamicVars;
        await KakarotPowerCmd.Apply<StrengthPower>(context, player.Creature,
            vars["Strength"].BaseValue, player.Creature, null);
        await KakarotPowerCmd.Apply<DexterityPower>(context, player.Creature,
            vars["Dexterity"].BaseValue, player.Creature, null);
    }

    private static async Task<List<CardModel>> SelectFromDeck(Player player, PlayerChoiceContext context,
        int amount, bool upgrade)
    {
        int count = Math.Min(amount, player.Deck.Cards.Count(card => upgrade ? card.IsUpgradable : card.IsRemovable));
        if (count == 0)
        {
            return [];
        }

        var prefs = new CardSelectorPrefs(upgrade ? CardSelectorPrefs.UpgradeSelectionPrompt
            : CardSelectorPrefs.RemoveSelectionPrompt, count) { Cancelable = false };
        // 牌组选择同步牌组索引，但不负责暂停 GameAction。
#if STS2_V109
        await context.SignalPlayerChoiceBegun(player, PlayerChoiceOptions.None);
#else
        await context.SignalPlayerChoiceBegun(PlayerChoiceOptions.None);
#endif
        var selected = (await (upgrade ? CardSelectCmd.FromDeckForUpgrade(player, prefs)
            : CardSelectCmd.FromDeckForRemoval(player, prefs))).ToList();
        await context.SignalPlayerChoiceEnded();
        return selected;
    }

    private static async Task ChangeDeck(Player player, List<CardModel> selected, bool upgrade)
    {
        // 同一张牌可能既被选去升级又被选去移除，移除在后，这里只处理还在牌组里的。
        selected = selected.Where(player.Deck.Cards.Contains).ToList();
        var combatCards = player.PlayerCombatState?.AllCards
            .Where(card => selected.Contains(card.DeckVersion)).ToList() ?? [];
        if (upgrade)
        {
            CardCmd.Upgrade(selected, CardPreviewStyle.None);
            CardCmd.Upgrade(combatCards, CardPreviewStyle.None);
        }
        else
        {
            await CardPileCmd.RemoveFromCombat(combatCards);
            await CardPileCmd.RemoveFromDeck(selected, showPreview: false);
        }
    }
}
