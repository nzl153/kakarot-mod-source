using System;
using System.Linq;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using KakarotMod.KakarotCode.Config;
using MegaCrit.Sts2.Core.Runs;

namespace KakarotMod.KakarotCode.Events;

internal static class KakarotEventEligibility
{
    /// <summary>
    /// 三个走事件池的本 mod 事件共用的准入判定。
    /// 玩家在设置里勾了「关闭弗利萨挑战以外的本 mod 事件」时直接不进池子。
    /// 该开关只在单人生效——联机两端配置不一致会让事件池分叉，导致不同步。
    /// 弗利萨挑战不经过这里（IsAllowed 恒 false），故不受影响。
    /// </summary>
    internal static bool AllowsExtraEvent(IRunState runState)
    {
        if (KakarotModConfig.DisableExtraEvents
            && RunManager.Instance.IsSingleplayerOrFakeMultiplayer)
        {
            return false;
        }

        return HasKakarot(runState);
    }

    internal static bool HasKakarot(IRunState runState)
    {
        return runState.Players.Any(static player =>
        {
            if (player.Character is KakarotCharacter)
            {
                return true;
            }

            string entry = player.Character?.Id.Entry;
            return !string.IsNullOrWhiteSpace(entry)
                && entry.Contains("kakarot", StringComparison.OrdinalIgnoreCase);
        });
    }
}
