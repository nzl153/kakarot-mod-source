using KakarotMod.KakarotCode.DragonBalls;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 愿望展示牌不加入卡池。
public sealed class KakarotWishSuperEnergy() : KakarotWishCard(
    KakarotWish.SuperEnergy, "kakarot_dragon_set_super", new DynamicVar("Energy", 1), new DynamicVar("Rage", 1));
#pragma warning restore STS004
