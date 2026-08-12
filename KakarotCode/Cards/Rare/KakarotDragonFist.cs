using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotDragonFist() : KakarotCard(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(36m, ValueProp.Move),
        new DynamicVar("Stars", 2m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Reference snapshots distinguish actual deaths from spawns or temporarily unhittable enemies.
        for (var safety = 0; safety < 20; safety++)
        {
            var aliveBefore = CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();
            if (aliveBefore.Count == 0)
            {
                break;
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);
            await PlayerCmd.GainStars(DynamicVars["Stars"].BaseValue, Owner);

            if (!aliveBefore.Any(e => e.IsDead))
            {
                break;
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8m);
    }
}
