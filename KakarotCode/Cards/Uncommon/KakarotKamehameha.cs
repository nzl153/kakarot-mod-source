using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotKamehameha() : KakarotCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override int CanonicalStarCost => 4;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(25m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
            // 光束打的是气压，不是刀刃：命中层换成掌，和自制 shader 光束同一套视觉语言。
            .WithHitVfxNode(KakarotCombatPresentation.KiHit(KiHitStyle.Palm, 1.15f))
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8m);
    }
}
