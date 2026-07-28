using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Powers;

/// <summary>瞬间移动：监听本回合打出的技能牌，叠临时灵敏（见 <see cref="KakarotInstantTransmissionDexPower"/>）。</summary>
public sealed class KakarotInstantTransmissionListenerPower : KakarotPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType InstanceType => MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.None;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        await AppendDexPowerAsync(choiceContext, Owner, cardPlay.Card);
    }

    internal static async Task AppendDexPowerAsync(PlayerChoiceContext choiceContext, Creature creature, CardModel source)
    {
        // Counter 由引擎命令原子累加，避免异步回调之间出现缺失状态。
        await KakarotPowerCmd.Apply<KakarotInstantTransmissionDexPower>(choiceContext, creature, 1m, creature, source);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, System.Collections.Generic.IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != Owner.Side)
        {
            return;
        }

        await PowerCmd.Remove(this);
    }
}
