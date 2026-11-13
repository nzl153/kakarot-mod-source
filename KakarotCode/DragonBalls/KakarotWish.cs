namespace KakarotMod.KakarotCode.DragonBalls;

[Flags]
public enum KakarotWish
{
    None = 0,
    EarthHeal = 1 << 0,
    EarthRemove = 1 << 1,
    EarthUpgrade = 1 << 2,
    EarthGold = 1 << 3,
    EarthSlay = 1 << 4,
    NamekHeal = 1 << 5,
    NamekUpgrade = 1 << 6,
    NamekRemove = 1 << 7,
    NamekGold = 1 << 8,
    NamekStrength = 1 << 9,
    NamekEnergy = 1 << 10,
    SuperEnergy = 1 << 11,
    SuperRitual = 1 << 12,
    SuperVitality = 1 << 13,
    SuperBasics = 1 << 14,
    BlackStarDraw = 1 << 15,
    BlackStarPower = 1 << 16,
    BlackStarRelics = 1 << 17,
}
