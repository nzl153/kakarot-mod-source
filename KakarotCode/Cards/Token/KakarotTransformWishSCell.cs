using KakarotMod.KakarotCode.DragonBalls;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace KakarotMod.KakarotCode.Cards.Token;

#pragma warning disable STS004 // 「神龙许愿我要变」的选项牌，不加入卡池。
public sealed class KakarotTransformWishSCell() : KakarotWishCard(
    KakarotWish.None, "kakarot_shenron_wish_transform", new DynamicVar("Amount", 3));
#pragma warning restore STS004
