using BaseLib.Abstracts;
using KakarotMod.KakarotCode.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 四选一展示牌不加入卡池。
public sealed class KakarotShenronEarth() : CustomCardModel(
    0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false, autoAdd: false)
{
    private const string ArtPath = "res://Kakarot/Images/Cards/kakarot_dragon_set_earth.png";

    // 只借用卡池的显示样式，不注册到卡池。
    public override CardPoolModel Pool => ModelDb.CardPool<KakarotCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ArtPath;
    public override string CustomPortraitPath => ArtPath;
    public override string BetaPortraitPath => ArtPath;
    protected override bool IsPlayable => false;
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}
#pragma warning restore STS004
