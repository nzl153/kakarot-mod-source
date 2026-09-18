using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotWildGuard() : KakarotCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar("BonusBlock", 4m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        if (HasPlayedWildThisTurn())
        {
            var bonusBlock = new BlockVar(DynamicVars["BonusBlock"].BaseValue, ValueProp.Move);
            await CommonActions.CardBlock(this, bonusBlock, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["BonusBlock"].UpgradeValueBy(1m);
    }

    private bool HasPlayedWildThisTurn()
    {
        return CombatManager.Instance.History.CardPlaysStarted.Any(entry =>
            entry.CardPlay.Card.Owner == Owner
            && entry.HappenedThisTurn(CombatState)
            && KakarotWildHelper.HasWild(entry.CardPlay.Card));
    }
}
