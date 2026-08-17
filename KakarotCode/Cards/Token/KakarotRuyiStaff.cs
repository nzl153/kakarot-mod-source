using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Token;

public class KakarotRuyiStaff() : KakarotCard(0, CardType.Attack, CardRarity.Token, TargetType.AllEnemies)
{
    public override int CanonicalStarCost => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

#if STS2_BETA
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var result = base.GetResultLocationForCardPlay();
        if (result.pileType == PileType.Discard)
        {
            result.pileType = PileType.Hand;
        }

        return result;
    }
#else
    protected override PileType GetResultPileTypeForCardPlay() => PileType.Hand;
#endif

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
