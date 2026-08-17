using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Rare;

public class KakarotShenronWishPotion() : KakarotCard(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override int CanonicalStarCost => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(20m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        var shouldTriggerFatal = cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (!shouldTriggerFatal || !attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            return;
        }

        if (CombatState?.RunState?.CurrentRoom is CombatRoom combatRoom)
        {
            combatRoom.AddExtraReward(Owner, new PotionReward(Owner));
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
