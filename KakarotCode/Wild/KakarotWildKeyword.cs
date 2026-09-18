using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace KakarotMod.KakarotCode.Wild;

// The current game build leaves CardKeyword slot 8 unused.
public static class KakarotWildKeyword
{
    public const CardKeyword Wild = (CardKeyword)8;

    // Log slot conflicts without preventing startup.
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
