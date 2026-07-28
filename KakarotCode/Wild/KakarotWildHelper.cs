using System;
using System.Linq;
using Godot;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Wild;

public static class KakarotWildHelper
{
    /// <summary>Canonical/百科用的卡可能没有初始化 <see cref="CardModel.Keywords"/>，此处必须防空否则枚举卡池会整页失败。</summary>
    public static bool HasWild(CardModel card) =>
        card?.Keywords?.Contains(KakarotWildKeyword.Wild) == true;

    /// <summary>While SS4 is active, wild cards behave as upgraded (engine upgrade pipeline).</summary>
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
            // Guard against max-upgrade cards from old saves/modded pools causing hard crashes.
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
