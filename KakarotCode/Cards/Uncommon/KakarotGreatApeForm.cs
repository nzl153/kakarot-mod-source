using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Cards.Token;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotGreatApeForm() : KakarotCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new DynamicVar("WildPicks", 2m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);

        // 与激发野性同因：结算期间自己还在手牌里，按 Count 算会要求选一张选不出来的牌。
        var selectableCount = Owner.PlayerCombatState.Hand.Cards.Count(c => c != this);
        var pickCount = System.Math.Min((int)DynamicVars["WildPicks"].BaseValue, selectableCount);
        if (pickCount > 0)
        {
            IEnumerable<CardModel> chosen = [];
            try
            {
                chosen = await KakarotCardSelectHelper.FromHandSelectRangeAsync(choiceContext, Owner, 0, pickCount, this);
            }
            catch
            {
                // 选择 UI 出问题不应该卡住整张牌的结算。
            }

            foreach (var card in chosen.Where(c => c != null && c != this).Take(pickCount))
            {
                card.AddKeyword(KakarotWildKeyword.Wild);
                KakarotWildHelper.EnsureWildActsUpgradedIfSs4(Owner, card);
            }
        }

        var outOfControl = CombatState.CreateCard<KakarotOutOfControl>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(outOfControl, PileType.Draw, Owner);
        var outOfControlDiscard = CombatState.CreateCard<KakarotOutOfControl>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(outOfControlDiscard, PileType.Discard, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
