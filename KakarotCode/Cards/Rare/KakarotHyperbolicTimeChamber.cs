using System.Collections.Generic;
using System.Threading.Tasks;
using KakarotMod.KakarotCode.DynamicVars;
using KakarotMod.KakarotCode.Helpers;
using KakarotMod.KakarotCode.Powers;
using KakarotMod.KakarotCode.Wild;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KakarotMod.KakarotCode.Cards.Rare;

/// <summary>S-cell training: Exhaust. Pay HP; next turn gain Energy, rage, and bonus draws.</summary>
public class KakarotHyperbolicTimeChamber() : KakarotCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const decimal HpCost = 6m;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<KakarotSCellPower>()];

    protected override bool ShouldGlowGoldInternal =>
        Owner?.Creature?.GetPower<SuperSaiyanFormPower>() != null;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        KakarotWildDisplay.MergeWild(this, [CardKeyword.Exhaust]);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hpLoss = KakarotTrainingSelfHpCost.Resolve(HpCost, Owner.Creature);
        await CreatureCmd.Damage(choiceContext, Owner.Creature, hpLoss, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);

        var power = (KakarotHyperbolicNextTurnPower)ModelDb.Power<KakarotHyperbolicNextTurnPower>().ToMutable();
        power.Configure(3m, DynamicVars["Stars"].BaseValue, DynamicVars["Draw"].BaseValue);
        await KakarotPowerCmd.Apply(choiceContext, power, Owner.Creature, 1m, Owner.Creature, this);

        await KakarotSCellPower.TryGrantCellsAsync(choiceContext, Owner, (int)HpCost, this);
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new KakarotReducedHpLossVar("ResolvedHpLoss", HpCost),
        new DynamicVar("Stars", 3m),
        new DynamicVar("Draw", 1m),
        new DynamicVar("SCells", 6m),
    ];

    protected override void OnUpgrade()
    {
        DynamicVars["Draw"].UpgradeValueBy(1m);
        DynamicVars["Stars"].UpgradeValueBy(1m);
    }
}
