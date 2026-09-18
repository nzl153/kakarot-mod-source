using System;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace KakarotMod.KakarotCode.Helpers;

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
