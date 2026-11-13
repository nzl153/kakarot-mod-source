#nullable enable
using static KakarotMod.Kakarot.MainFile;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace KakarotMod.KakarotCode.DragonBalls;

public sealed class KakarotSummonShenronAction(Player player) : GameAction
{
    private KakarotShenronSummonFx? _fx;

    public override ulong OwnerId => player.NetId;
    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

    protected override async Task ExecuteAction()
    {
        var relic = player.GetRelic<KakarotSevenDragonBalls>();
        if (relic == null)
        {
            return;
        }

        relic.RequestPending = false;
        if (relic.CanSummon)
        {
            Logger.Info($"Player {player.NetId} summoned Shenron: {relic.SelectedSet}");
            _fx = KakarotShenronSummonFx.Start(relic.SelectedSet);
            try
            {
                if (_fx != null)
                {
                    await _fx.IntroDone;
                }

                await KakarotDragonBallWishes.ChooseAndResolve(relic, new GameActionPlayerChoiceContext(this));
            }
            finally
            {
                _fx?.Finish();
            }
        }

    }

    protected override void CancelAction()
    {
        // 等待选择时被取消，上面的 finally 不会执行，演出在这里收掉。
        _fx?.Finish();
        var relic = player.GetRelic<KakarotSevenDragonBalls>();
        if (relic != null)
        {
            relic.RequestPending = false;
        }
    }

    public override INetAction ToNetAction() => default(NetKakarotSummonShenronAction);
    public override string ToString() => $"KakarotSummonShenronAction {player.NetId}";
}

public struct NetKakarotSummonShenronAction : INetAction, IPacketSerializable
{
    public GameAction ToGameAction(Player player) => new KakarotSummonShenronAction(player);
    public void Serialize(PacketWriter writer) { }
    public void Deserialize(PacketReader reader) { }
}
