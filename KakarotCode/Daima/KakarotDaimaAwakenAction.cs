using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace KakarotMod.KakarotCode.Daima;

// Player input goes through the action queue so the host orders it and every peer runs it.
public sealed class KakarotDaimaAwakenAction(Player player) : GameAction
{
    public Player Player { get; } = player;

    public override ulong OwnerId => Player.NetId;

    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

    protected override async Task ExecuteAction()
    {
        var relic = Player.GetRelic<KakarotDaimaThirdEye>();
        if (relic == null || relic.UsedThisCombat)
        {
            return;
        }

        // A request deferred through the enemy turn can arrive after its owner went down.
        if (!Player.Creature.IsAlive)
        {
            relic.RequestPending = false;
            return;
        }

        relic.MarkUsed();
        await KakarotDaima.Enter(Player);
    }

    // The host drops player actions once the turn is ending; let the relic be clicked again.
    protected override void CancelAction()
    {
        var relic = Player.GetRelic<KakarotDaimaThirdEye>();
        if (relic != null)
        {
            relic.RequestPending = false;
        }
    }

    public override INetAction ToNetAction() => default(NetKakarotDaimaAwakenAction);

    public override string ToString() => $"KakarotDaimaAwakenAction {Player.NetId}";
}

public struct NetKakarotDaimaAwakenAction : INetAction, IPacketSerializable
{
    public GameAction ToGameAction(Player player) => new KakarotDaimaAwakenAction(player);

    public void Serialize(PacketWriter writer)
    {
    }

    public void Deserialize(PacketReader reader)
    {
    }

    public override string ToString() => "NetKakarotDaimaAwakenAction";
}
