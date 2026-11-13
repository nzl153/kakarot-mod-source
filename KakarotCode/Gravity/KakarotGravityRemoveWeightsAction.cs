using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace KakarotMod.KakarotCode.Gravity;

public sealed class KakarotGravityRemoveWeightsAction(Player player) : GameAction
{
    public override ulong OwnerId => player.NetId;
    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

    protected override async Task ExecuteAction()
    {
        var relic = player.GetRelic<KakarotGravityWeights>();
        if (relic == null)
        {
            return;
        }

        relic.RequestPending = false;
        if (!relic.IsActive || relic.UsedWeightsThisCombat || !player.Creature.IsAlive)
        {
            return;
        }

        relic.MarkWeightsUsed();
        await KakarotPowerCmd.Apply<KakarotTemporaryAgilityPower>(player.Creature, 5, player.Creature, null);
    }

    protected override void CancelAction()
    {
        var relic = player.GetRelic<KakarotGravityWeights>();
        if (relic != null)
        {
            relic.RequestPending = false;
        }
    }

    public override INetAction ToNetAction() => default(NetKakarotGravityRemoveWeightsAction);
    public override string ToString() => $"KakarotGravityRemoveWeightsAction {player.NetId}";
}

public struct NetKakarotGravityRemoveWeightsAction : INetAction, IPacketSerializable
{
    public GameAction ToGameAction(Player player) => new KakarotGravityRemoveWeightsAction(player);
    public void Serialize(PacketWriter writer) { }
    public void Deserialize(PacketReader reader) { }
}
