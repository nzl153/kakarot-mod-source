using System.Collections.Concurrent;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using Godot;
using KakarotMod.KakarotCode.Extensions;
using MegaCrit.Sts2.Core.Helpers;

namespace KakarotMod.KakarotCode.Powers;

public abstract class KakarotPower : CustomPowerModel
{
    // 路径解析按 Id.Entry 缓存。tooltip / power UI 刷新会高频调用 getter，
    // 没缓存的话每次都要走 ResourceLoader.Exists（IO 探测）。
    private static readonly ConcurrentDictionary<string, string> PackedIconCache = new();
    private static readonly ConcurrentDictionary<string, string> BigIconCache = new();

    private static readonly string PackedFallback =
        ImageHelper.GetImagePath("atlases/power_atlas.sprites/strength_power.tres");

    private static readonly string BigFallback =
        ImageHelper.GetImagePath("powers/strength_power.png");

    public override string CustomPackedIconPath
    {
        get
        {
            var entry = Id.Entry;
            if (string.IsNullOrEmpty(entry))
            {
                return PackedFallback;
            }

            return PackedIconCache.GetOrAdd(entry, e =>
            {
                var customPath = $"{e.RemovePrefix().ToLowerInvariant()}.png".PowerImagePath();
                return ResourceLoader.Exists(customPath) ? customPath : PackedFallback;
            });
        }
    }

    public override string CustomBigIconPath
    {
        get
        {
            var entry = Id.Entry;
            if (string.IsNullOrEmpty(entry))
            {
                return BigFallback;
            }

            return BigIconCache.GetOrAdd(entry, e =>
            {
                var customPath = $"{e.RemovePrefix().ToLowerInvariant()}.png".BigPowerImagePath();
                return ResourceLoader.Exists(customPath) ? customPath : BigFallback;
            });
        }
    }
}
