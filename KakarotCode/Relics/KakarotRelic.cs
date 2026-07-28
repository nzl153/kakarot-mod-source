using System.Collections.Concurrent;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Godot;
using KakarotMod.KakarotCode.Characters;
using KakarotMod.KakarotCode.Extensions;
using MegaCrit.Sts2.Core.Helpers;

namespace KakarotMod.KakarotCode.Relics;

[Pool(typeof(KakarotRelicPool))]
public abstract class KakarotRelic : CustomRelicModel
{
    // 路径解析按 Id.Entry 缓存：遗物 tooltip / 顶栏图标渲染会高频调用 getter。
    private static readonly ConcurrentDictionary<string, string> PackedIconCache = new();
    private static readonly ConcurrentDictionary<string, string> OutlineIconCache = new();
    private static readonly ConcurrentDictionary<string, string> BigIconCache = new();

    private static readonly string Fallback = ImageHelper.GetImagePath("relics/burning_blood.png");

    public override string PackedIconPath
    {
        get
        {
            var entry = Id.Entry;
            if (string.IsNullOrEmpty(entry))
            {
                return Fallback;
            }

            return PackedIconCache.GetOrAdd(entry, e =>
            {
                var custom = $"{e.RemovePrefix().ToLowerInvariant()}.png".RelicImagePath();
                return ResourceLoader.Exists(custom) ? custom : Fallback;
            });
        }
    }

    protected override string PackedIconOutlinePath
    {
        get
        {
            var entry = Id.Entry;
            if (string.IsNullOrEmpty(entry))
            {
                return Fallback;
            }

            return OutlineIconCache.GetOrAdd(entry, e =>
            {
                var custom = $"{e.RemovePrefix().ToLowerInvariant()}_outline.png".RelicImagePath();
                return ResourceLoader.Exists(custom) ? custom : Fallback;
            });
        }
    }

    protected override string BigIconPath
    {
        get
        {
            var entry = Id.Entry;
            if (string.IsNullOrEmpty(entry))
            {
                return Fallback;
            }

            return BigIconCache.GetOrAdd(entry, e =>
            {
                var custom = $"{e.RemovePrefix().ToLowerInvariant()}.png".BigRelicImagePath();
                return ResourceLoader.Exists(custom) ? custom : Fallback;
            });
        }
    }
}
