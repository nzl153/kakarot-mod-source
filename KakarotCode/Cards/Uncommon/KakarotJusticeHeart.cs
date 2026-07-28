using System.Collections.Generic;
using BaseLib.Utils;
using KakarotMod.KakarotCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Uncommon;

public class KakarotJusticeHeart() : KakarotCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar("Heal", 1m)
    ];

    protected override bool IsPlayable => base.IsPlayable && Owner?.Creature?.GetPower<KakarotSuperSaiyanGodPower>() == null;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<KakarotJusticeHeartRitualPower>(),
        HoverTipFactory.FromPower<KakarotSuperSaiyanGodPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        await CreatureCmd.Heal(Owner.Creature, DynamicVars["Heal"].BaseValue);
        await KakarotJusticeHeartRitualHandler.AddProgressAndResolve(choiceContext, Owner, this, 1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["Heal"].UpgradeValueBy(1m);
    }
}
