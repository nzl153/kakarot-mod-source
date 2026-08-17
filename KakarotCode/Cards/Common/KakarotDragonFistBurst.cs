using System.Collections.Generic;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Common;

public class KakarotDragonFistBurst() : KakarotCard(1, CardType.Attack, CardRarity.Token, TargetType.AllEnemies)
{
    public override int CanonicalStarCost => 1;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        new DynamicVar("Hits", 3m),
    ];

    protected override bool IsPlayable =>
        base.IsPlayable && (Owner?.Creature?.GetPowerAmount<SuperSaiyanFormPower>() ?? 0) == 3;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hits = (int)DynamicVars["Hits"].BaseValue;
        for (var i = 0; i < hits; i++)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
