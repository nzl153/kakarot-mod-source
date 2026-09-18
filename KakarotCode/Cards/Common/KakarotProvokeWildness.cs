using System.Linq;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotProvokeWildness() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int CanonicalStarCost => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Strength", 2m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["Strength"].BaseValue, Owner.Creature, this);

        // 本牌结算期间自己仍在手牌里，按 Count 判断会把自己算进去：
        // 手上只剩这一张时仍会弹出「选 1 张」，而唯一可选项被排除，UI 永远确认不了，
        // 野性就挂不上去。这与兽性冲拳当初的成因是同一个。
        var selectableCount = Owner.PlayerCombatState.Hand.Cards.Count(c => c != this);
        if (selectableCount <= 0)
        {
            return;
        }

        IEnumerable<CardModel> chosen = [];
        try
        {
            // 用区间选择（下限 0），手牌在结算中途变化时 UI 也能安全确认。
            chosen = await KakarotCardSelectHelper.FromHandSelectRangeAsync(choiceContext, Owner, 0, 1, this);
        }
        catch
        {
            // 选择 UI 出问题不应该卡住整张牌的结算。
        }

        var card = chosen.FirstOrDefault(c => c != null && c != this);
        if (card == null)
        {
            return;
        }

        card.AddKeyword(KakarotWildKeyword.Wild);
        KakarotWildHelper.EnsureWildActsUpgradedIfSs4(Owner, card);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Strength"].UpgradeValueBy(1m);
    }
}
