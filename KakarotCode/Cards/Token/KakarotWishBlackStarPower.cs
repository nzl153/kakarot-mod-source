using KakarotMod.KakarotCode.DragonBalls;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 愿望展示牌不加入卡池。
public sealed class KakarotWishBlackStarPower() : KakarotWishCard(
    KakarotWish.BlackStarPower, "kakarot_dragon_set_blackstar", new DynamicVar("Strength", 2), new DynamicVar("Dexterity", 2));
#pragma warning restore STS004
