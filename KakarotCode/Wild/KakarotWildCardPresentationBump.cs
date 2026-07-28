using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Wild;

/// <summary>赋予野性后请求卡面刷新，使关键字行及时同步。</summary>
public static class KakarotWildCardPresentationBump
{
    public static void TryBump(CardModel card)
    {
        if (card is null)
        {
            return;
        }

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
                // ignore
            }
        }
    }
}
