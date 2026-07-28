using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

/// <summary>Playable only when leftmost hand card is Wild; gain resources and draw.</summary>
public class KakarotSaiyanPowerBank() : KakarotCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override bool IsPlayable
    {
        get
        {
            if (!base.IsPlayable)
            {
                return false;
            }

            var hand = Owner.PlayerCombatState.Hand.Cards;
            return hand.Count > 0 && KakarotWildHelper.HasWild(hand[0]);
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Draw", 1m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(2m, Owner);
        await PlayerCmd.GainStars(2m, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Draw"].UpgradeValueBy(1m);
    }
}
