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

/// <summary>Gain block, mark hand cards Wild, then add Out of Control to draw pile.</summary>
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

        var pickCount = System.Math.Min((int)DynamicVars["WildPicks"].BaseValue, Owner.PlayerCombatState.Hand.Cards.Count);
        if (pickCount > 0)
        {
            var chosen = await KakarotCardSelectHelper.FromHandSelectAsync(choiceContext, Owner, pickCount, this);
            foreach (var card in chosen.Where(c => c != null))
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
