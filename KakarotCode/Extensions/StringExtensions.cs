using System.Linq;
using MainFile = KakarotMod.Kakarot.MainFile;

namespace KakarotMod.KakarotCode.Extensions;

public static class StringExtensions
{
    private static string JoinRes(params string[] parts)
    {
        return string.Join("/", parts.Select(static p => p.Replace('\\', '/').Trim('/')));
    }

    public static string ImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", path);
    }

    public static string CardImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Cards", path);
    }

    public static string BigCardImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Cards", "Big", path);
    }

    public static string FramedBigCardImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Cards", "Big", "Framed", path);
    }

    public static string RelicImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Relics", path);
    }

    public static string BigRelicImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Relics", "Big", path);
    }

    public static string PotionImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Potions", path);
    }

    public static string CharacterUiPath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Charui", path);
    }

    public static string PowerImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Powers", path);
    }

    public static string BigPowerImagePath(this string path)
    {
        return JoinRes(MainFile.ModId, "Images", "Powers", "Big", path);
    }
}
