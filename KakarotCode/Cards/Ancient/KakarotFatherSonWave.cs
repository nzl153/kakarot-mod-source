using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using KakarotMod.KakarotCode.Characters;

namespace KakarotMod.KakarotCode.Cards.Ancient;

public class KakarotFatherSonWave() : KakarotCard(1, CardType.Attack, CardRarity.Ancient, TargetType.AnyEnemy)
{
    public override int CanonicalStarCost => 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(30m, ValueProp.Move),
        new PowerVar<VulnerablePower>(2m),
        new PowerVar<WeakPower>(2m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).Targeting(cardPlay.Target)
            // 父子龟波是龟波的变体，命中层跟龟波保持同一套。
            .WithHitVfxNode(KakarotCombatPresentation.KiHit(KiHitStyle.Palm, 1.25f))
            .Execute(choiceContext);
        await KakarotPowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await KakarotPowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(10m);
    }
}
