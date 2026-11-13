using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 只能由击杀愿望生成。
public sealed class KakarotShenronSlay() : CustomCardModel(
    0, CardType.Skill, CardRarity.Token, TargetType.AnyEnemy, showInCardLibrary: false, autoAdd: false)
{
    public override CardPoolModel Pool => ModelDb.CardPool<KakarotCardPool>();
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
    public override string PortraitPath => "res://Kakarot/Images/Cards/kakarot_dragon_set_earth.png";
    public override string CustomPortraitPath => PortraitPath;
    public override string BetaPortraitPath => PortraitPath;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is { IsAlive: true } target)
        {
#if STS2_BETA
            await CreatureCmd.Kill(target, this);
#else
            await CreatureCmd.Kill(target);
#endif
        }
    }
}
#pragma warning restore STS004
