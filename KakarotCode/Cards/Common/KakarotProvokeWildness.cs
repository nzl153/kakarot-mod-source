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

/// <summary>1 energy: mark 1 card in hand as Wild and gain temporary Strength.</summary>
public class KakarotProvokeWildness() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int CanonicalStarCost => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Strength", 2m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KakarotPowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["Strength"].BaseValue, Owner.Creature, this);
        await KakarotPowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        if (Owner.PlayerCombatState.Hand.Cards.Count == 0)
        {
            return;
        }

        var chosen = await KakarotCardSelectHelper.FromHandSelectAsync(choiceContext, Owner, 1, this);
        var card = chosen.FirstOrDefault();
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
