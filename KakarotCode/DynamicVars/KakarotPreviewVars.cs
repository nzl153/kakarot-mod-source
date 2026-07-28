#nullable enable
using System;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace KakarotMod.KakarotCode.DynamicVars;

// 预览路径只更新 PreviewValue；不得从卡面渲染路径修改同步战斗状态。
// 词条页和主菜单中的 canonical 卡牌没有有效 Owner，计算时必须先检查 run/combat 状态并容错返回基础值。

/// <summary>
/// 「舍我其谁」段数展示：显示 = 基础段数(Hits，含升级) + 本局已打出次数（从赛亚血脉遗物只读）。
/// 与 OnPlay 实际结算同源同公式（Hits.BaseValue + 计数器，再 clamp）。只读计算，绝不写 BaseValue。
/// </summary>
public sealed class KakarotTotalHitsVar : DynamicVar
{
    public KakarotTotalHitsVar(string name, decimal baseValue)
        : base(name, baseValue)
    {
    }

    private decimal Compute()
    {
        if (_owner is not CardModel card)
        {
            return BaseValue;
        }

        try
        {
            var baseHits = (int)card.DynamicVars["Hits"].BaseValue;

            if (card.RunState == null)
            {
                return baseHits;
            }

            var played = SaiyanBlood.ResolveBloodlineRelic(card.Owner)?.WhoElseButMePlayCountThisRun ?? 0;
            return Math.Clamp(baseHits + played, baseHits, baseHits + 20);
        }
        catch (CanonicalModelException)
        {
            return BaseValue;
        }
        catch (MutableModelException)
        {
            return BaseValue;
        }
    }

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        PreviewValue = Compute();
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        return Compute();
    }

    public override string ToString()
    {
        return ((int)Compute()).ToString();
    }
}

/// <summary>
/// 卖血/修炼牌扣血展示：显示 = 超赛形态减免后的实际扣血（<see cref="KakarotTrainingSelfHpCost.Resolve"/>）。
/// OnPlay 实扣血用同一公式（对常量基础值求 Resolve）。只读计算，绝不写 BaseValue。
/// </summary>
public sealed class KakarotReducedHpLossVar : DynamicVar
{
    public KakarotReducedHpLossVar(string name, decimal baseValue)
        : base(name, baseValue)
    {
    }

    private decimal Compute()
    {
        if (_owner is not CardModel card || card.CombatState == null)
        {
            return BaseValue;
        }

        try
        {
            return KakarotTrainingSelfHpCost.Resolve(BaseValue, card.Owner?.Creature);
        }
        catch (CanonicalModelException)
        {
            return BaseValue;
        }
        catch (MutableModelException)
        {
            return BaseValue;
        }
    }

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        PreviewValue = Compute();
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        return Compute();
    }

    public override string ToString()
    {
        return ((int)Compute()).ToString();
    }
}
