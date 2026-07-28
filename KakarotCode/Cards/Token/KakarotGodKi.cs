using System.Collections.Generic;
using System.Linq;
using KakarotMod.KakarotCode.Cards.Ancient;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Token;

/// <summary>
/// 仅由超赛神 / 超蓝变身召唤，不进常规卡池。本场打出 2 次后加入「变成超级赛亚人蓝色」。
/// </summary>
public class KakarotGodKi() : KakarotCard(2, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override bool IsPlayable =>
        base.IsPlayable &&
        Owner?.Creature is { } creature &&
        (creature.HasPower<KakarotSuperSaiyanGodPower>() || creature.HasPower<KakarotSuperSaiyanBluePower>());

    /// <summary>超级赛亚人蓝色：本场战斗中所有「神之气」实例升一级（基础 2 费）。</summary>
    public static void UpgradeAllInstancesInCombat(Player player)
    {
        foreach (var card in player.PlayerCombatState.AllCards.ToList())
        {
            if (card is KakarotGodKi g && !g.IsUpgraded)
            {
                g.UpgradeInternal();
                g.FinalizeUpgradeInternal();
            }
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        KakarotUltraInstinctCombatState.OnGodKiPlayed(Owner);

        var creature = Owner.Creature;
        if (creature.HasPower<KakarotSuperSaiyanBluePower>() || HasPendingSuperSaiyanBlueTransform())
        {
            return;
        }

        var ritual = creature.GetPower<KakarotGodKiRitualPower>();
        var current = ritual?.Progress ?? 0;
        if (current >= 2)
        {
            // Recover a counter left capped by an older DLL before beginning the next cycle.
            await PowerCmd.Remove<KakarotGodKiRitualPower>(creature);
            current = 0;
        }

        int nextPlays = System.Math.Min(current + 1, 2);
        if (nextPlays == 2)
        {
            var blue = CombatState.CreateCard<KakarotSuperSaiyanBlueTransform>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(blue, PileType.Hand, Owner);
            await PowerCmd.Remove<KakarotGodKiRitualPower>(creature);
            return;
        }

        await KakarotPowerCmd.Apply<KakarotGodKiRitualPower>(
            choiceContext,
            creature,
            nextPlays - current,
            creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    private bool HasPendingSuperSaiyanBlueTransform()
    {
        return Owner?.PlayerCombatState?.AllCards?.Any(card =>
            card is KakarotSuperSaiyanBlueTransform &&
            card.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard) == true;
    }
}
