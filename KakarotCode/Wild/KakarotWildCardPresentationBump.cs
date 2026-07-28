using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Wild;

// Notify known card-face refresh methods after Wild changes.
public static class KakarotWildCardPresentationBump
{
    public static void TryBump(CardModel card)
    {
        if (card is null)
        {
            return;
        }

        // Invoke refresh methods only; mutating layout fields can corrupt card presentation.

        foreach (var methodName in new[]
                 {
                     "NotifyKeywordsChanged", "OnKeywordsChanged", "InvalidatePresentation", "RefreshPresentation",
                     "SyncPresentation", "InvalidateVisual", "RefreshVisual",
                 })
        {
            var m = AccessTools.Method(card.GetType(), methodName, Type.EmptyTypes);
            if (m == null)
            {
                continue;
            }

            try
            {
                m.Invoke(card, null);
            }
            catch
            {
            }
        }
    }
}
