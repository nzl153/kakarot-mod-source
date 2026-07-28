#nullable disable
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>
/// 原版 <c>event</c> 控制台只在 <see cref="ModelDb.AllEvents"/> 枚举里找，不包含纯 mod 注册的事件。
/// 在未命中时再按 <see cref="ModelId"/> 查 <see cref="ModelDb.GetByIdOrNull{T}"/>。
/// </summary>
[HarmonyPatch(typeof(EventConsoleCmd), nameof(EventConsoleCmd.Process))]
public static class KakarotEventConsoleCmdPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Player issuingPlayer, string[] args, ref CmdResult __result)
    {
        if (args.Length == 0 || issuingPlayer == null || !RunManager.Instance.IsInProgress)
        {
            return true;
        }

        string eventName = args[0].ToUpperInvariant();

        bool inVanillaList = ModelDb.AllEvents.Concat(ModelDb.AllAncients).Any(e => e.Id.Entry == eventName);
        if (inVanillaList)
        {
            return true;
        }

        // GetCategory 在类型未注册或控制台输入异常时可能抛 KeyNotFoundException；
        // 失败时回退到原生流程，让控制台报"未找到事件"，不至于栈崩。
        ModelId id;
        try
        {
            id = new ModelId(ModelDb.GetCategory(typeof(EventModel)), eventName);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot] event console: failed to resolve EventModel category for '{eventName}': {ex.Message}");
            return true;
        }

        EventModel ev = ModelDb.GetByIdOrNull<EventModel>(id);
        if (ev is null || ev is DeprecatedEvent)
        {
            return true;
        }

        MapPointType mapPointType = ev is AncientEventModel ? MapPointType.Ancient : MapPointType.Unknown;
        issuingPlayer.RunState.AppendToMapPointHistory(mapPointType, RoomType.Event, ev.Id);
        Task task = RunManager.Instance.EnterRoom(new EventRoom(ev));
        __result = new CmdResult(task, true, $"Jumped to event: '{ev.Id.Entry}'");
        return false;
    }
}
