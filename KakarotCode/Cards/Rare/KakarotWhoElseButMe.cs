using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.DynamicVars;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotWhoElseButMe() : KakarotCard(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    private const decimal HpCostBase = 5m;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotSCellPower>()];

    protected override bool ShouldGlowGoldInternal =>
        IsAtOrBelowHalfHp(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new KakarotReducedHpLossVar("ResolvedHpLoss", HpCostBase),
        new DamageVar(15m, ValueProp.Move),
        new DynamicVar("SCells", 5m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hpCost = KakarotTrainingSelfHpCost.Resolve(HpCostBase, Owner.Creature);

        await CreatureCmd.Damage(choiceContext, Owner.Creature, hpCost, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);

        if (IsAtOrBelowHalfHp(Owner.Creature))
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);
        }

        await KakarotSCellPower.TryGrantCellsAsync(choiceContext, Owner, (int)HpCostBase, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }

    private static bool IsAtOrBelowHalfHp(Creature creature)
    {
        return creature != null && creature.MaxHp > 0 && creature.CurrentHp * 2 <= creature.MaxHp;
    }
}
