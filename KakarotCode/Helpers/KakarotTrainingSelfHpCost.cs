using System;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace KakarotMod.KakarotCode.Helpers;

/// <summary>修炼/S 细胞卖血牌：超赛形态减免伤害（SS1:-1, SS2/SS3:-2, 最低1点）。</summary>
internal static class KakarotTrainingSelfHpCost
{
    internal static decimal Resolve(decimal baseHpLoss, Creature creature)
    {
        var ssPower = creature?.GetPower<SuperSaiyanFormPower>();
        if (ssPower == null)
        {
            return baseHpLoss;
        }

        var tier = (int)ssPower.Amount;
        var reduction = tier >= 2 ? 2m : 1m;
        return Math.Max(1m, baseHpLoss - reduction);
    }
}
