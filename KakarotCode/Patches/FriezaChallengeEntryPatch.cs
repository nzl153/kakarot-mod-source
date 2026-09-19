#nullable enable
using System.Linq;
using System.Threading.Tasks;
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
    // EnterRoom 是 async Task：参数在进入状态机时即被复制，prefix 改写 ref 参数不保证传递到下游，
    // 同一方法上若存在其他补丁，执行顺序也不可控。因此这里不改写参数，而是返回 false 接管调用，
    // 自行发起一次 EnterRoom 并将结果写入 __result；调用方 EnterNextAct 会 await 该返回值，
    // __result 为空将直接抛出异常。
    // 接管后的重入只有一层：此时 room 为 FriezaChallenge，首个判断即放行原方法。
    //
    // 诊断日志用于区分补丁未调用、房间类型不符、运行状态缺失和配置未生效等入口问题，不参与事件判断。
    private static bool _aliveLogged;

    [HarmonyPrefix]
    public static bool Prefix(AbstractRoom room, RunManager __instance, ref Task __result)
    {
        // 每个进程仅记录一次，用于确认补丁已加载并执行。
        if (!_aliveLogged)
        {
            _aliveLogged = true;
            GD.Print("[Kakarot] FriezaGate: patch alive (first EnterRoom seen).");
        }

        if (room is not EventRoom nextEvent || nextEvent.CanonicalEvent is not TheArchitect)
        {
            return true;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            GD.Print("[Kakarot] FriezaGate: architect room reached -> SKIP(runState-null)");
            return true;
        }

        if (runState.CurrentRoom is EventRoom { CanonicalEvent: FriezaChallenge })
        {
            GD.Print("[Kakarot] FriezaGate: architect room reached -> SKIP(already-in-frieza)");
            return true;
        }

        // 默认仅队伍中存在卡卡罗特时触发；设置可在单人模式下开放给所有角色。
        // 联机各端会独立执行 EnterRoom，而本地 cfg 不随 RunState 同步；允许本地配置参与判断会使共享事件分叉并导致战斗同步等待。
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
            return true;
        }

        GD.Print("[Kakarot] FriezaGate: -> SWAP(taking over EnterRoom with Frieza challenge)");
        __result = __instance.EnterRoom(new EventRoom(ModelDb.Event<FriezaChallenge>()));
        return false;
    }
}
