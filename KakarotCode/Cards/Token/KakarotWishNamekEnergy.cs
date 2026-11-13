using KakarotMod.KakarotCode.DragonBalls;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 愿望展示牌不加入卡池。
public sealed class KakarotWishNamekEnergy() : KakarotWishCard(
    KakarotWish.NamekEnergy, "kakarot_dragon_set_namek", new DynamicVar("Energy", 2), new DynamicVar("Cards", 2));
#pragma warning restore STS004
