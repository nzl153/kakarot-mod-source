using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Powers;

public sealed class KakarotFightTogetherPower : KakarotPower
{
    public const decimal RagePerAttackPerStack = 1m;
    public const decimal BlockPerRagePerStack = 2m;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var cardOwner = cardPlay.Card.Owner;
        if (!cardPlay.IsLastInSeries
            || cardPlay.Card.Type != CardType.Attack
            || cardOwner == Owner.Player
            || !cardOwner.Creature.IsAlive
            || !Owner.IsAlive
            || cardOwner.Creature.Side != Owner.Side)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainStars(Amount * RagePerAttackPerStack, Owner.Player);
    }

    public override async Task AfterStarsSpent(int amount, Player spender)
    {
        if (amount <= 0 || spender != Owner.Player || !Owner.IsAlive)
        {
            return;
        }

        var teammates = CombatState.Players
            .Where(player => player != spender
                && player.Creature.IsAlive
                && player.Creature.Side == Owner.Side)
            .ToArray();
        if (teammates.Length == 0)
        {
            return;
        }

        Flash();
        var block = amount * Amount * BlockPerRagePerStack;
        foreach (var teammate in teammates)
        {
            await CreatureCmd.GainBlock(
                teammate.Creature,
                block,
                ValueProp.Unpowered,
                null,
                fast: true);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
