using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Cards.Ancient;
using KakarotMod.KakarotCode.Powers;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Wild;

internal static class KakarotWildRitualHandler
{
    internal static Task OnWildCardPlayed(PlayerChoiceContext choiceContext, Player player, CardModel playedCard)
    {
        if (playedCard == null)
        {
            return Task.CompletedTask;
        }

        return AddRitualProgress(choiceContext, player, playedCard, 1);
    }

    // Applies ritual progress without requiring a Wild card play.
    internal static async Task AddRitualProgress(PlayerChoiceContext choiceContext, Player player, CardModel sourceCard, int progressToAdd)
    {
        if (player.Character is not KakarotCharacter || progressToAdd <= 0)
        {
            return;
        }

        var creature = player.Creature;
        if (creature == null)
        {
            return;
        }

        if (creature.HasPower<KakarotSuperSaiyan4Power>() || HasPendingReturnToOrigin(player))
        {
            return;
        }

        var ritual = creature.GetPower<KakarotWildRitualPower>();
        var current = ritual?.Progress ?? 0;
        if (current >= 5)
        {
            // Reset a capped counter before beginning the next cycle.
            await PowerCmd.Remove<KakarotWildRitualPower>(creature);
            current = 0;
        }

        var next = Math.Min(current + progressToAdd, 5);
        if (next <= current)
        {
            return;
        }

        if (next == 5)
        {
            var cs = creature.CombatState;
            if (cs == null && sourceCard is CustomCardModel cm)
            {
                cs = cm.CombatState;
            }

            if (cs != null)
            {
                var card = cs.CreateCard<KakarotReturnToOrigin>(player);
                var handFull = player.PlayerCombatState?.Hand?.Cards?.Count >= 10;
                await CardPileCmd.AddGeneratedCardToCombat(card, handFull ? PileType.Draw : PileType.Hand, player);
                await PowerCmd.Remove<KakarotWildRitualPower>(creature);
                return;
            }
        }

        await KakarotPowerCmd.Apply<KakarotWildRitualPower>(
            choiceContext,
            creature,
            next - current,
            creature,
            sourceCard);
    }

    private static bool HasPendingReturnToOrigin(Player player)
    {
        return player?.PlayerCombatState?.AllCards?.Any(card =>
            card is KakarotReturnToOrigin &&
            card.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard) == true;
    }
}
