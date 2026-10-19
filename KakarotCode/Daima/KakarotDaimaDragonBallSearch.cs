using System.Collections.Generic;
using KakarotMod.KakarotCode.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Daima;

public class KakarotDaimaDragonBallSearch() : KakarotCard(1, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Wishes", 2m),
        new DynamicVar("Stars", 2m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<KakarotDaimaSs4KidPower>())
        {
            for (var i = 0; i < DynamicVars["Wishes"].IntValue
                && Owner.Creature.HasPower<KakarotDaimaSs4KidPower>()
                && (Owner.Creature.GetPower<KakarotDaimaWishPower>()?.Amount ?? 0) < KakarotDaima.WishGoal; i++)
            {
                await KakarotDaima.GrantWish(Owner);
            }
        }
        else
        {
            await PlayerCmd.GainStars(DynamicVars["Stars"].BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Wishes"].UpgradeValueBy(1m);
        DynamicVars["Stars"].UpgradeValueBy(1m);
    }
}
