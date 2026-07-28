using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Relics;

/// <summary>Boss upgrade of Saiyan Blood with stronger rage gain and +1 max Energy.</summary>
public sealed class KakarotLegendaryLineage : SaiyanBlood
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    /// <summary>较赛亚人之血（首回合 1 点）更高：每场首回合 2 点怒气。</summary>
    protected override decimal OpeningCombatRageBonus => 2m;

    /// <summary>战后回复覆写基础遗物：生命低于最大值 80% 时回复 6。</summary>
    protected override int EndCombatHealHpThresholdPercent => 80;

    protected override decimal EndCombatHealAmount => 6m;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner)
        {
            return amount;
        }

        return amount + DynamicVars.Energy.BaseValue;
    }

    public override async Task AfterObtained()
    {
        var starterId = ModelDb.Relic<SaiyanBlood>().Id;
        var existing = Owner.GetRelicById(starterId);

        if (existing != null && !ReferenceEquals(existing, this))
        {
            if (existing is SaiyanBlood oldBlood)
            {
                TransferRunPersistentStateFrom(oldBlood);
            }

            await RelicCmd.Remove(existing);
        }
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);

        if (creature == Owner.Creature && delta < 0 && CombatManager.Instance.IsInProgress && creature.CombatState != null)
        {
            Flash();
            await PlayerCmd.GainStars(1m, Owner);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(context, cardPlay);

        if (cardPlay.Card.Owner == Owner && cardPlay.Card.Tags.Contains(CardTag.Strike))
        {
            Flash();
            await PlayerCmd.GainStars(1m, Owner);
        }
    }
}
