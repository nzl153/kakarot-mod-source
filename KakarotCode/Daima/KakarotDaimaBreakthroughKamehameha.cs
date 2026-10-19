using System.Collections.Generic;
using KakarotMod.KakarotCode.Cards;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Compatibility;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Daima;

// Adult Daima SS4 finisher. AttackCommand has no unblockable option, so enemy Block is broken first.
public class KakarotDaimaBreakthroughKamehameha() : KakarotCard(2, CardType.Attack, CardRarity.Token, TargetType.AllEnemies)
{
    public override int CanonicalStarCost => 1;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(50m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var enemy in CombatState.HittableEnemies)
        {
            if (enemy.Block > 0)
            {
                await KakarotBetaCompat.LoseBlock(choiceContext, enemy, enemy.Block, Owner.Creature);
            }
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
            .WithHitVfxNode(KakarotCombatPresentation.KiHit(KiHitStyle.Palm, 1.5f))
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(20m);
    }
}
