using System.Collections.Generic;
using System.Linq;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotProphecyFishProphecy() : KakarotCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int CanonicalStarCost => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Draw", 2m)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotJusticeHeartRitualPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var beforeDraw = Owner.PlayerCombatState.Hand.Cards.ToHashSet();
        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].IntValue, Owner);
        var drawnAttacks = Owner.PlayerCombatState.Hand.Cards.Count(card => !beforeDraw.Contains(card) && card.Type == CardType.Attack);
        if (drawnAttacks > 0)
        {
            await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(choiceContext, Owner, this, drawnAttacks);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["Draw"].UpgradeValueBy(1m);
    }
}
