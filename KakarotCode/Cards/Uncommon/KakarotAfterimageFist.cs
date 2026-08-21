using System.Collections.Generic;
using KakarotMod.KakarotCode.Characters;
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

        // 多段攻击必须共用一条带 HitCount 的命令，避免超巨化提前消耗并保留段数 Hook。
        await DamageCmd.Attack(starX)
            .WithHitCount(energyX)
            .FromKakarotCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .WithHitVfxNode(KakarotCombatPresentation.CreateAfterimageHitVfx)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
