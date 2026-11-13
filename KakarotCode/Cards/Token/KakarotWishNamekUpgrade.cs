using KakarotMod.KakarotCode.DragonBalls;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 愿望展示牌不加入卡池。
public sealed class KakarotWishNamekUpgrade() : KakarotWishCard(
    KakarotWish.NamekUpgrade, "kakarot_dragon_set_namek", new DynamicVar("Cards", 2));
#pragma warning restore STS004
