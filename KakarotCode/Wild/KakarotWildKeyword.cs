using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace KakarotMod.KakarotCode.Wild;

/// <summary>
/// Custom keyword slot — engine <see cref="CardKeyword"/> enum has no named Wild; slot 8 is unused in current sts2 builds.
/// </summary>
public static class KakarotWildKeyword
{
    public const CardKeyword Wild = (CardKeyword)8;

    /// <summary>
    /// 启动时调用：检测槽位 8 是否被某个版本的 <see cref="CardKeyword"/> 占用。
    /// 占用了不强制中止，但要让我们能在 godot.log 里第一时间看到这个根因，
    /// 否则各种"野性显示异常 / 误判命中"会很难定位。
    /// </summary>
    public static void LogIfSlotCollides()
    {
        try
        {
            const int slot = (int)Wild;
            if (!Enum.IsDefined(typeof(CardKeyword), slot))
            {
                return;
            }

            var collidingName = Enum.GetName(typeof(CardKeyword), slot);
            GD.PrintErr(
                $"[Kakarot] Wild keyword slot {slot} now collides with engine CardKeyword.{collidingName}. " +
                "Wild visibility / detection may misbehave. Consider remapping KakarotWildKeyword.Wild to an unused slot.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kakarot] Wild keyword slot check failed: {ex.Message}");
        }
    }
}
