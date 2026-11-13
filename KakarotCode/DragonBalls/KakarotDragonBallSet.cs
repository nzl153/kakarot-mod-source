namespace KakarotMod.KakarotCode.DragonBalls;

public enum KakarotDragonBallSet
{
    None,
    Earth,
    Namek,
    Super,
    BlackStar,
}

internal static class KakarotDragonBallRules
{
    public const int RequiredBalls = 7;
    public const int EarthAndNamekDropPercent = 18;
    public const int EarthAndNamekPityMisses = 4;
    public const int BlackStarDropPercent = 50;
    public const int BlackStarPityMisses = 1;
}
