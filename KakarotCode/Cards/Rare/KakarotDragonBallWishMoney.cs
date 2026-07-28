using System.Collections.Generic;
using System.Linq;
using KakarotMod.KakarotCode.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Rare;

/// <summary>AOE attack. Each kill grants extra gold after combat.</summary>
public class KakarotDragonBallWishMoney() : KakarotCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    private const int GoldPerKill = 100;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(20m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        var kills = attack.Results.SelectMany(r => r).Count(r => r.WasTargetKilled);

        if (kills > 0 && SaiyanBlood.ResolveBloodlineRelic(Owner) is { } blood)
        {
            blood.AddWishGoldPendingThisCombat(kills * GoldPerKill);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
