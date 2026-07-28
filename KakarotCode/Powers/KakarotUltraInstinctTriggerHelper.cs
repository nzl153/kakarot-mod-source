using System;
using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Cards.Ancient;
using KakarotMod.KakarotCode.Relics;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Powers;

public static class KakarotUltraInstinctTriggerHelper
{
    public static int StableStringHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (value != null)
            {
                foreach (var ch in value)
                {
                    hash ^= ch;
                    hash *= 16777619u;
                }
            }

            return (int)hash;
        }
    }

    private static uint MixStable(uint hash, uint value)
    {
        unchecked
        {
            hash ^= value + 0x9e3779b9u + (hash << 6) + (hash >> 2);
            hash *= 16777619u;
            return hash;
        }
    }

    // Damage-preview hooks cannot safely consume RunState RNG. This uses only synced combat state
    // and a stable hash, avoiding process-random HashCode/GetHashCode behavior in multiplayer.
    public static bool RollDeterministic(int chance, Player player, decimal delta, int salt = 0, int rollIndex = 0)
    {
        if (chance <= 0)
        {
            return false;
        }

        if (chance >= 100)
        {
            return true;
        }

        var round = player.Creature?.CombatState?.RoundNumber ?? 0;
        var deltaScaled = (int)Math.Clamp(
            Math.Truncate(delta * 100m),
            int.MinValue + 1,
            int.MaxValue - 1);

        unchecked
        {
            uint hash = 2166136261u;
            hash = MixStable(hash, (uint)player.NetId);
            hash = MixStable(hash, (uint)(player.NetId >> 32));
            hash = MixStable(hash, (uint)round);
            hash = MixStable(hash, (uint)deltaScaled);
            hash = MixStable(hash, (uint)salt);
            hash = MixStable(hash, (uint)rollIndex);
            return hash % 100u < (uint)chance;
        }
    }

    public static int GetDamageDodgeRollIndex(Creature owner)
    {
        var combatState = owner?.CombatState;
        if (combatState == null || CombatManager.Instance?.History == null)
        {
            return 0;
        }

        return CombatManager.Instance.History.Entries
            .OfType<DamageReceivedEntry>()
            .Count(e => e.Receiver == owner && e.HappenedThisTurn(combatState));
    }

    public static async Task RemoveAllTransformPowers(Creature creature)
    {
        await RemoveUltraInstinctForms(creature);
        await RemoveSaiyanTransformsForUltraInstinct(creature);
    }

    public static async Task RemoveUltraInstinctForms(Creature creature)
    {
        await PowerCmd.Remove<KakarotUltraInstinctOmenPower>(creature);
        await PowerCmd.Remove<KakarotPerfectUltraInstinctPower>(creature);
    }

    public static async Task RemoveSaiyanTransformsForUltraInstinct(Creature creature)
    {
        await PowerCmd.Remove<KakarotSuperSaiyanBluePower>(creature);
        await PowerCmd.Remove<KakarotSuperSaiyanGodPower>(creature);
        await PowerCmd.Remove<SuperSaiyanFormPower>(creature);
        await PowerCmd.Remove<SuperSaiyanRagePower>(creature);
        await PowerCmd.Remove<KaiokenPower>(creature);
        await PowerCmd.Remove<KakarotSuperSaiyan4Power>(creature);
        await PowerCmd.Remove<KakarotSuperSaiyan4EnergyCapPower>(creature);
    }

    public static async Task OnPlayerTookHpLoss(Player player, Creature creature, ICombatState combatState, decimal delta)
    {
        if (combatState is null || delta >= 0 || !CombatManager.Instance.IsInProgress || player.Character is not KakarotCharacter)
        {
            return;
        }

        var chance = 1;
        chance += KakarotUltraInstinctCombatState.GetGodKiBonusPercent(player);
        if (creature.HasPower<KakarotSuperSaiyanGodPower>())
        {
            chance += 5;
        }

        if (creature.HasPower<KakarotSuperSaiyanBluePower>())
        {
            chance += 10;
        }

        var insightPower = creature.GetPower<KakarotInsightUltraInstinctPower>();
        if (insightPower != null)
        {
            chance += insightPower.TotalBonus;
        }

        chance = Math.Min(chance, 100);
        var rollIndex = SaiyanBlood.ResolveBloodlineRelic(player)?.NextUltraInstinctHpLossRollIndex() ?? 0;
        if (!RollDeterministic(chance, player, delta, salt: 1, rollIndex))
        {
            return;
        }

        var omenId = ModelDb.Card<KakarotUltraInstinctOmen>().Id;
        if (player.PlayerCombatState.AllCards.Any(c => c.Id == omenId))
        {
            return;
        }

        var card = combatState.CreateCard<KakarotUltraInstinctOmen>(player);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        await PowerCmd.Remove<KakarotInsightUltraInstinctPower>(creature);
    }
}
