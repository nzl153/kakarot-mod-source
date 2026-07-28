using System;
using System.Linq;
using Godot;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Wild;

public static class KakarotWildHelper
{
    // Canonical cards may not initialize Keywords.
    public static bool HasWild(CardModel card) =>
        card?.Keywords?.Contains(KakarotWildKeyword.Wild) == true;

    public static void EnsureWildActsUpgradedIfSs4(Player player, CardModel card)
    {
        if (player?.Creature?.GetPower<KakarotSuperSaiyan4Power>() == null || card == null)
        {
            return;
        }

        if (!HasWild(card) || card.IsUpgraded)
        {
            return;
        }

        try
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
        catch (Exception ex)
        {
            // Skip invalid max-upgrade cards supplied by saves or other mods.
            GD.PrintErr($"[Kakarot] Skip Wild auto-upgrade for {card.Id.Entry}: {ex.Message}");
        }
    }

    public static void UpgradeAllWildCardsInCombat(Player player)
    {
        if (player?.Creature?.GetPower<KakarotSuperSaiyan4Power>() == null)
        {
            return;
        }

        foreach (var c in player.PlayerCombatState.AllCards.ToList())
        {
            EnsureWildActsUpgradedIfSs4(player, c);
        }
    }
}
