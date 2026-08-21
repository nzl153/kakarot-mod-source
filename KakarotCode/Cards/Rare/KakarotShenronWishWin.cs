using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotShenronWishWin() : KakarotCard(4, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    private const int SuccessChance = 20;

    public override int CanonicalStarCost => 4;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(99999m, ValueProp.Move),
        new DynamicVar("Chance", SuccessChance),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.RunState.Rng.Niche.NextInt(100) >= DynamicVars["Chance"].IntValue)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
            .WithHitVfxNode(KakarotCombatPresentation.KiHit(KiHitStyle.Fist, 1.00f))
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        UpgradeStarCostBy(-1);
    }
}
