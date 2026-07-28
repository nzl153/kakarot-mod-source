using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
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

/// <summary>Attack; if rightmost hand card is Wild, attack again and draw.</summary>
public class KakarotCoolMirror() : KakarotCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override int CanonicalStarCost => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var trigger = IsRightmostCardWild();
        await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);
        if (trigger)
        {
            await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
    }

    private bool IsRightmostCardWild()
    {
        var hand = Owner.PlayerCombatState.Hand.Cards;
        var rightmost = hand.LastOrDefault();
        return rightmost != null && KakarotWildHelper.HasWild(rightmost);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
