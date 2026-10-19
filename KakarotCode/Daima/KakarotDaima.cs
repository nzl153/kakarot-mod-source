using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Cards.Ancient;
using KakarotMod.KakarotCode.Cards.Common;
using KakarotMod.KakarotCode.Cards.Rare;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Cards.Uncommon;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Daima;

// Entry point and shared rules for the Daima route. Everything outside this folder asks IsActive.
public static class KakarotDaima
{
    // The old transformation system is sealed off while the route is active.
    private static readonly HashSet<Type> BannedCards =
    [
        typeof(KakarotSuperSaiyanTransform),
        typeof(KakarotCancelSuperSaiyanForm),
        typeof(KakarotKaioken),
        typeof(KakarotJusticeHeart),
        typeof(KakarotProphecyFishProphecy),
        typeof(KakarotShenronWishTransform),
        typeof(KakarotSuperSaiyanGodTransform),
        typeof(KakarotSuperSaiyanBlueTransform),
        typeof(KakarotGodKi),
        typeof(KakarotUltraInstinctOmen),
        typeof(KakarotPeakUltraInstinct),
        typeof(KakarotReturnToOrigin),
    ];

    public const int WishGoal = 5;

    public static bool IsActive(Creature creature) =>
        creature != null && creature.Powers.Any(p => p is KakarotDaimaStagePower);

    public static bool IsBanned(CardModel card) => BannedCards.Contains(card.GetType());

    public static async Task Enter(Player player)
    {
        var creature = player.Creature;
        await KakarotUltraInstinctTriggerHelper.RemoveAllTransformPowers(creature);

        var stars = player.PlayerCombatState.Stars;
        if (stars > 0)
        {
            await PlayerCmd.LoseStars(stars, player);
        }

        await SetStage<KakarotDaimaKidPower>(creature);
        KakarotCombatPresentation.TryPlayDaimaTransformation(creature, growUp: false);

        var transform = creature.CombatState.CreateCard<KakarotDaimaTransform>(player);
        await CardPileCmd.AddGeneratedCardToCombat(transform, PileType.Hand, player);
    }

    // Apply the new stage before removing the old one, so no refresh in between sees the base form.
    public static async Task SetStage<T>(Creature creature) where T : KakarotDaimaStagePower, new()
    {
        var previous = creature.Powers.OfType<KakarotDaimaStagePower>().ToList();
        await KakarotPowerCmd.Apply<T>(creature, 1m, creature, null);
        foreach (var old in previous)
        {
            await PowerCmd.Remove(old);
        }

        var next = creature.GetPower<T>();
        var strength = next.StrengthBonus - previous.Sum(p => p.StrengthBonus);
        var dexterity = next.DexterityBonus - previous.Sum(p => p.DexterityBonus);
        if (strength != 0)
        {
            await KakarotPowerCmd.Apply<StrengthPower>(creature, strength, creature, null);
        }

        if (dexterity != 0)
        {
            await KakarotPowerCmd.Apply<DexterityPower>(creature, dexterity, creature, null);
        }
    }

    // Kid -> SS1 -> SS3 -> kid SS4. The adult form comes from wishes, not from this card.
    public static async Task AdvanceByTransform(Creature creature)
    {
        if (creature.HasPower<KakarotDaimaKidPower>())
        {
            await SetStage<KakarotDaimaSs1Power>(creature);
            var player = creature.Player;
            CardModel[] cards =
            [
                creature.CombatState.CreateCard<KakarotDaimaRuyiCombo>(player),
                creature.CombatState.CreateCard<KakarotDaimaNimbusDash>(player),
                creature.CombatState.CreateCard<KakarotDaimaKaioshinSupport>(player),
                creature.CombatState.CreateCard<KakarotDaimaTailRegrowth>(player),
            ];
            await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Draw, player, CardPilePosition.Random);
        }
        else if (creature.HasPower<KakarotDaimaSs1Power>())
        {
            await SetStage<KakarotDaimaSs3Power>(creature);
        }
        else if (creature.HasPower<KakarotDaimaSs3Power>())
        {
            await SetStage<KakarotDaimaSs4KidPower>(creature);
            foreach (var card in CardPile.GetCards(creature.Player, PileType.Draw, PileType.Hand, PileType.Discard).ToList())
            {
                UpgradeFeatureCardIfSs4(card);
            }

            // Wishes only count in kid SS4, so the Dragon Ball hunt arrives here (already upgraded by the stage hook).
            var search = creature.CombatState.CreateCard<KakarotDaimaDragonBallSearch>(creature.Player);
            await CardPileCmd.AddGeneratedCardToCombat(search, PileType.Hand, creature.Player);
        }
    }

    public static bool CanTransformFurther(Creature creature) =>
        creature.HasPower<KakarotDaimaKidPower>()
        || creature.HasPower<KakarotDaimaSs1Power>()
        || creature.HasPower<KakarotDaimaSs3Power>();

    public static void UpgradeFeatureCardIfSs4(CardModel card)
    {
        if (card is not (KakarotDaimaRuyiCombo or KakarotDaimaNimbusDash or KakarotDaimaDragonBallSearch
            or KakarotDaimaKaioshinSupport or KakarotDaimaTailRegrowth) || card.IsUpgraded)
        {
            return;
        }

        var creature = card.Owner.Creature;
        if (creature.HasPower<KakarotDaimaSs4KidPower>() || creature.HasPower<KakarotDaimaSs4Power>())
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
    }

    public static async Task GrantWish(Player player)
    {
        var creature = player.Creature;
        if (!creature.HasPower<KakarotDaimaSs4KidPower>()
            || (creature.GetPower<KakarotDaimaWishPower>()?.Amount ?? 0) >= WishGoal)
        {
            return;
        }

        await KakarotPowerCmd.Apply<KakarotDaimaWishPower>(creature, 1m, creature, null);
        if ((creature.GetPower<KakarotDaimaWishPower>()?.Amount ?? 0) < WishGoal)
        {
            return;
        }

        var awakening = creature.CombatState.CreateCard<KakarotDaimaSs4Awakening>(player);
        await CardPileCmd.AddGeneratedCardToCombat(awakening, PileType.Hand, player);
    }

    // Visual path for the current Daima stage, or null when the route is not active.
    public static string ResolveModelPath(Creature creature)
    {
        var stage = creature.Powers.OfType<KakarotDaimaStagePower>().LastOrDefault();
        return stage == null ? null : $"res://Kakarot/Images/Charui/kakarot_combat_model_{stage.ModelName}.png";
    }
}
