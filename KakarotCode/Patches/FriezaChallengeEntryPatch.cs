#nullable enable
using System.Linq;
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
    [HarmonyPrefix]
    public static void Prefix(ref AbstractRoom room)
    {
        if (room is not EventRoom nextEvent || nextEvent.CanonicalEvent is not TheArchitect)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null ||
            runState.CurrentRoom is EventRoom { CanonicalEvent: FriezaChallenge })
        {
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
        var openToAll = KakarotModConfig.FriezaForAllCharacters
                        && RunManager.Instance.IsSingleplayerOrFakeMultiplayer;

        if (!openToAll &&
            !runState.Players.Any(static player => player.Character is KakarotCharacter))
        {
            return;
        }

        room = new EventRoom(ModelDb.Event<FriezaChallenge>());
    }
}
