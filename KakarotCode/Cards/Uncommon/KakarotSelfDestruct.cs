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

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotSelfDestruct() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    private const decimal SelfDamage = 10m;

    public override int CanonicalStarCost => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(30m, ValueProp.Move)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotWildRitualPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await KakarotBetaCompat.DamageFromCard(
            choiceContext,
            Owner.Creature,
            SelfDamage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            this,
            cardPlay);
    }

    // Exhaust callbacks are awaited, preserving rage and ritual command order.
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card != this || Owner == null)
        {
            return;
        }

        await PlayerCmd.GainStars(1m, Owner);
        await KakarotWildRitualHandler.AddRitualProgress(choiceContext, Owner, this, 1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
