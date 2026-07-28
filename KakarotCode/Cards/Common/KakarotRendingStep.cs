using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotRendingStep() : KakarotCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new BlockVar(5m, ValueProp.Move),
        new DynamicVar("BonusDamage", 4m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay.Target).Execute(choiceContext);
        await CommonActions.CardBlock(this, cardPlay);

        if (KakarotWildHelper.HasWild(this))
        {
            await CommonActions.CardAttack(this, cardPlay.Target, DynamicVars["BonusDamage"].BaseValue).Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["BonusDamage"].UpgradeValueBy(1m);
    }
}
