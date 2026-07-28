using System.Linq;
using Godot;
using KakarotMod.KakarotCode.Cards.Ancient;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Powers;

public static class KakarotJusticeHeartRitualHandler
{
    private const int MaxProgress = 5;

    public static async Task AddProgressAndResolve(PlayerChoiceContext choiceContext, Player owner, CardModel sourceCard, int progressToAdd)
    {
        if (owner?.Creature == null || progressToAdd <= 0)
        {
            return;
        }

        var creature = owner.Creature;
        if (creature.HasPower<KakarotSuperSaiyanGodPower>() || HasPendingSuperSaiyanGodTransform(owner))
        {
            return;
        }

        var ritual = creature.GetPower<KakarotJusticeHeartRitualPower>();
        var current = ritual?.Progress ?? 0;
        if (current >= MaxProgress)
        {
            // Recover a counter left capped by an older DLL before beginning the next cycle.
            await PowerCmd.Remove<KakarotJusticeHeartRitualPower>(creature);
            current = 0;
        }

        var next = System.Math.Min(current + progressToAdd, MaxProgress);
        if (next <= current)
        {
            return;
        }

        if (next >= MaxProgress)
        {
            bool isKakarot = owner.Character is Characters.Kakarot;
            if (!isKakarot || creature.CombatState == null)
            {
                GD.Print($"[Kakarot] JusticeHeart reached 5 but no SSG card granted. isKakarot={isKakarot}, hasCombatState={creature.CombatState != null}");
                return;
            }

            if (SaiyanBlood.ResolveBloodlineRelic(owner) == null)
            {
                GD.PrintErr("[Kakarot] JusticeHeart reached 5 but SaiyanBlood-line relic not found; skipping SSG grant.");
                return;
            }

            var ssgCard = creature.CombatState.CreateCard<KakarotSuperSaiyanGodTransform>(owner);
            await CardPileCmd.AddGeneratedCardToCombat(ssgCard, PileType.Hand, owner);
            await PowerCmd.Remove<KakarotJusticeHeartRitualPower>(creature);
            GD.Print("[Kakarot] JusticeHeart reached 5, granted SSG transform to Hand.");
            return;
        }

        await KakarotPowerCmd.Apply<KakarotJusticeHeartRitualPower>(
            choiceContext,
            creature,
            next - current,
            creature,
            sourceCard);
    }

    private static bool HasPendingSuperSaiyanGodTransform(Player owner)
    {
        return owner?.PlayerCombatState?.AllCards?.Any(card =>
            card is KakarotSuperSaiyanGodTransform &&
            card.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard) == true;
    }
}
