using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotAfterimageFist() : KakarotCard(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    protected override bool HasEnergyCostX => true;

    public override bool HasStarCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var energyX = ResolveEnergyXValue();
        var starX = ResolveStarXValue();
        if (energyX <= 0 || starX <= 0)
        {
            return;
        }

        for (var i = 0; i < energyX; i++)
        {
            await DamageCmd.Attack(starX).FromKakarotCard(this, cardPlay).TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
