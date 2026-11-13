using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.DragonBalls;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 愿望展示牌不加入卡池。
public abstract class KakarotWishCard(KakarotWish wish, string art, params DynamicVar[] vars)
    : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.Self,
        showInCardLibrary: false, autoAdd: false)
{
    public KakarotWish Wish => wish;
    public override CardPoolModel Pool => ModelDb.CardPool<KakarotCardPool>();
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => $"res://Kakarot/Images/Cards/{art}.png";
    public override string CustomPortraitPath => PortraitPath;
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => vars;
    protected override bool IsPlayable => false;
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}
#pragma warning restore STS004
