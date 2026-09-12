#nullable enable
using System.Linq;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Config;
using KakarotMod.KakarotCode.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;

namespace KakarotMod.KakarotCode.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoom))]
public static class FriezaChallengeEntryPatch
{
    // 诊断埋点（5.4.3 起）：有玩家报告「对全角色开放」开关在他的环境下不生效，
    // 但本地干净环境两次实测都通过，日志里这个补丁又一行输出都没有，
    // 导致「没被调用 / 房间不是建筑师 / runState 为空 / 开关没读成 true」
    // 这四种情况在日志上完全无法区分。
    // 下面的输出只写日志，不参与任何判断，控制流与 5.4.2 完全一致。
    // 案子定位后应当移除或降级为 Debug 级别。
    private static bool _aliveLogged;

    [HarmonyPrefix]
    public static void Prefix(ref AbstractRoom room)
    {
        // 心跳：证明补丁确实被挂上且真的在跑（每个进程只打一次，不刷屏）。
        if (!_aliveLogged)
        {
            _aliveLogged = true;
            GD.Print("[Kakarot] FriezaGate: patch alive (first EnterRoom seen).");
        }

        if (room is not EventRoom nextEvent || nextEvent.CanonicalEvent is not TheArchitect)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            GD.Print("[Kakarot] FriezaGate: architect room reached -> SKIP(runState-null)");
            return;
        }

        if (runState.CurrentRoom is EventRoom { CanonicalEvent: FriezaChallenge })
        {
            GD.Print("[Kakarot] FriezaGate: architect room reached -> SKIP(already-in-frieza)");
            return;
        }

        // 默认只在队伍里有卡卡罗特时触发；玩家可在 mod 设置里开启「对全角色开放」。
        //
        // 🚨 该开关只在单人模式生效，联机时一律回退到「队伍里有卡卡罗特」这一条件。
        // 原因：进哪个房间是每个客户端各自判断的（EnterNextAct → EnterRoom →
        // EnterRoomInternal 只做本地 State.PushRoom，不广播、无主机权威），
        // 而配置是各人本地的 cfg 文件、不随存档同步。若房内有人开有人没开，
        // 各客户端会进入不同的 EventRoom；两个事件又都是 IsShared=true，
        // 共享事件靠互发「投了第几个选项」同步且不校验事件是否一致，
        // 最终一方进入弗利萨战斗、另一方没有，CombatStateSynchronizer 会永久等待。
        // 角色能作为判据是因为它属于 RunState、会镜像到每个客户端；配置不是。
        var configFriezaForAll = KakarotModConfig.FriezaForAllCharacters;
        var isSingleplayer = RunManager.Instance.IsSingleplayerOrFakeMultiplayer;
        var openToAll = configFriezaForAll && isSingleplayer;
        var hasKakarot = runState.Players.Any(static player => player.Character is KakarotCharacter);

        GD.Print(
            "[Kakarot] FriezaGate: architect room reached | " +
            $"cfg.FriezaForAllCharacters={configFriezaForAll} " +
            $"isSingleplayerOrFake={isSingleplayer} " +
            $"openToAll={openToAll} " +
            $"hasKakarotInParty={hasKakarot} " +
            $"players={runState.Players.Count}");

        if (!openToAll && !hasKakarot)
        {
            GD.Print("[Kakarot] FriezaGate: -> SKIP(no-kakarot-in-party-and-not-open-to-all)");
            return;
        }

        GD.Print("[Kakarot] FriezaGate: -> SWAP(replacing architect room with Frieza challenge)");
        room = new EventRoom(ModelDb.Event<FriezaChallenge>());
    }
}
