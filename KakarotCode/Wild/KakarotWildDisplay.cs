using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace KakarotMod.KakarotCode.Wild;

/// <summary>供完全重写 <see cref="CardModel.CanonicalKeywords"/> 的卡合并「野性」展示用。</summary>
public static class KakarotWildDisplay
{
    public static IEnumerable<CardKeyword> MergeWild(CardModel card, IEnumerable<CardKeyword> intrinsicKeywords)
    {
        IEnumerable<CardKeyword> intrinsic = intrinsicKeywords;
        try
        {
            if (card.Keywords != null && card.Keywords.Contains(KakarotWildKeyword.Wild))
            {
                var list = intrinsic.ToList();
                if (!list.Contains(KakarotWildKeyword.Wild))
                {
                    list.Add(KakarotWildKeyword.Wild);
                }

                return list;
            }
        }
        catch (CanonicalModelException)
        {
        }
        catch (MutableModelException)
        {
        }

        return intrinsic;
    }
}
