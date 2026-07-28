using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Godot;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Extensions;
using MegaCrit.Sts2.Core.Helpers;

namespace KakarotMod.KakarotCode.Potions;

[Pool(typeof(KakarotPotionPool))]
public abstract class KakarotPotion : CustomPotionModel
{
    private static readonly string FallbackImage =
        ImageHelper.GetImagePath("atlases/potion_atlas.sprites/blood_potion.tres");

    private static readonly string FallbackOutline =
        ImageHelper.GetImagePath("atlases/potion_outline_atlas.sprites/blood_potion.tres");

    public override string CustomPackedImagePath
    {
        get
        {
            var custom = $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionImagePath();
            return ResourceLoader.Exists(custom) ? custom : FallbackImage;
        }
    }

    public override string CustomPackedOutlinePath
    {
        get
        {
            var custom = $"{Id.Entry.RemovePrefix().ToLowerInvariant()}_outline.png".PotionImagePath();
            return ResourceLoader.Exists(custom) ? custom : FallbackOutline;
        }
    }
}
