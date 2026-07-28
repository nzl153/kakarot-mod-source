using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>
/// Whis's Guidance: until your next turn start, chance to negate HP loss; then Justice ritual + draw.
/// </summary>
public sealed class KakarotWhisGuidancePower : KakarotPower
{
    /// <summary>多张维斯卡叠加时在出牌侧合并为单一实例，避免多条图标。</summary>
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>角标显示合计规避概率（%）。</summary>
    public override int DisplayAmount => (int)Amount;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DodgePercent", 10m),
        new DynamicVar("RitualProgress", 1m),
    ];

    public int RitualProgressAtTurnStart => System.Math.Max(1, DynamicVars["RitualProgress"].IntValue);

    public void Configure(int ritualProgressAtTurnStart)
    {
        AssertMutable();
        DynamicVars["RitualProgress"].BaseValue = System.Math.Max(1, ritualProgressAtTurnStart);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        DynamicVars["DodgePercent"].BaseValue = Amount;
        InvokeDisplayAmountChanged();
        return base.AfterApplied(applier, cardSource);
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || amount <= 0)
        {
            return 1m;
        }

        var player = Owner.Player;
        var rollIndex = KakarotUltraInstinctTriggerHelper.GetDamageDodgeRollIndex(Owner);
        if (player == null || !KakarotUltraInstinctTriggerHelper.RollDeterministic((int)Amount, player, amount, salt: 4, rollIndex))
        {
            return 1m;
        }

        return 0m;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(choiceContext, player, null, RitualProgressAtTurnStart);
        await CardPileCmd.Draw(choiceContext, 1, player);
        await PowerCmd.Remove(this);
    }
}
