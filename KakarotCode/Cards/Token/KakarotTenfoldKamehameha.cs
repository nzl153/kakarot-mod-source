using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Cards.Token;

public class KakarotTenfoldKamehameha() : KakarotCard(2, CardType.Attack, CardRarity.Token, TargetType.AllEnemies)
{
    private bool _adjacentWildReady;

    public override int CanonicalStarCost => 2;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [KakarotWildKeyword.Wild];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(40m, ValueProp.Move)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [WildKeywordHoverTip];

    protected override bool IsPlayable
    {
        get
        {
            _adjacentWildReady = TryWildHandNeighbors();
            return base.IsPlayable && Owner?.Creature?.GetPower<KakarotSuperSaiyan4Power>() != null;
        }
    }

    protected override bool ShouldGlowGoldInternal => _adjacentWildReady;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool repeat = _adjacentWildReady || TryWildHandNeighbors();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
            .WithHitVfxNode(KakarotCombatPresentation.KiHit(KiHitStyle.Palm, 1.35f))
            .Execute(choiceContext);

        if (repeat)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
                .WithHitVfxNode(KakarotCombatPresentation.KiHit(KiHitStyle.Palm, 1.35f))
                .Execute(choiceContext);
        }
    }

    private bool TryWildHandNeighbors()
    {
        var hand = Owner.PlayerCombatState.Hand.Cards;
        var i = -1;
        for (var j = 0; j < hand.Count; j++)
        {
            if (ReferenceEquals(hand[j], this))
            {
                i = j;
                break;
            }
        }

        if (i <= 0 || i >= hand.Count - 1)
        {
            return false;
        }

        return KakarotWildHelper.HasWild(hand[i - 1]) && KakarotWildHelper.HasWild(hand[i + 1]);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
