using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace KakarotMod.KakarotCode.Cards.Curse;

#pragma warning disable STS004 // 黑星愿望的代价，不进入诅咒池。
public sealed class KakarotEvilDesire() : CustomCardModel(
    1, CardType.Curse, CardRarity.Curse, TargetType.Self, showInCardLibrary: false, autoAdd: false)
{
    public override CardPoolModel Pool => ModelDb.CardPool<CurseCardPool>();
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Eternal, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("CostIncrease", 1)];
    public override string PortraitPath => "res://Kakarot/Images/Cards/kakarot_evil_desire.png";
    public override string CustomPortraitPath => PortraitPath;
    public override string BetaPortraitPath => PortraitPath;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Pile?.Type != PileType.Hand || card.Owner != Owner || card.Pile?.Type != PileType.Hand
            || card.EnergyCost.CostsX || originalCost < 0 || card.Keywords.Contains(CardKeyword.Unplayable))
        {
            return false;
        }

        modifiedCost += DynamicVars["CostIncrease"].BaseValue;
        return true;
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}
#pragma warning restore STS004
