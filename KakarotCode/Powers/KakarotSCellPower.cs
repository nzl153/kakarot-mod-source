using KakarotMod.KakarotCode.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotSCellPower : KakarotPower
{
    private const int TransformCardCellThreshold = 6;
    private const int StatMilestoneStep = 10;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int CellCount => System.Math.Max(0, (int)Amount);
    public override int DisplayAmount => CellCount;

    protected override bool IsVisibleInternal => CellCount > 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("SCells", 0m)];

    public void Configure(int cells)
    {
        AssertMutable();
        DynamicVars["SCells"].BaseValue = System.Math.Max(0, cells);
    }

    public static async Task TryGrantCellsAsync(PlayerChoiceContext choiceContext, Player player, int amount, CardModel cardSource)
    {
        if (player == null || amount <= 0 || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        var creature = player.Creature;
        if (creature == null)
        {
            return;
        }

        var existing = creature.GetPower<KakarotSCellPower>();
        var current = existing?.CellCount ?? 0;
        var next = current + amount;
        if (next <= current)
        {
            return;
        }

        await PowerCmd.Remove<KakarotSCellPower>(creature);

        var power = (KakarotSCellPower)ModelDb.Power<KakarotSCellPower>().ToMutable();
        power.Configure(next);
        await KakarotPowerCmd.Apply(choiceContext, power, creature, next, creature, cardSource);

        if (current < TransformCardCellThreshold && next >= TransformCardCellThreshold && creature.CombatState != null)
        {
            var card = creature.CombatState.CreateCard<KakarotSuperSaiyanTransform>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }

        var oldMilestones = current / StatMilestoneStep;
        var newMilestones = next / StatMilestoneStep;
        for (var i = oldMilestones; i < newMilestones; i++)
        {
            await KakarotPowerCmd.Apply<StrengthPower>(choiceContext, creature, 2m, creature, null);
            await KakarotPowerCmd.Apply<DexterityPower>(choiceContext, creature, 2m, creature, null);
        }
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        DynamicVars["SCells"].BaseValue = CellCount;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
