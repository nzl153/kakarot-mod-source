#nullable enable
using BaseLib.Config;

namespace KakarotMod.KakarotCode.Config;

/// <summary>
/// 卡卡罗特 mod 设置。BaseLib 根据 static 属性自动生成 UI，并将配置持久化到 mod_configs。
/// 配置属性必须为 static，否则 BaseLib 会忽略该属性；标签文案来自 settings_ui.json，缺失时回退到属性名。
/// </summary>
public sealed class KakarotModConfig : SimpleModConfig
{
    /// <summary>
    /// 弗利萨挑战对全角色开放。默认关闭；仅改变事件入口，不修改战斗奖励或卡池。
    /// </summary>
    public static bool FriezaForAllCharacters { get; set; }

    /// <summary>
    /// 关闭弗利萨挑战以外的三个事件。使用负向开关以保证配置读取失败时回退为“保持事件启用”。
    /// 仅单人生效，因为联机各端独立执行 IsAllowed，配置不一致会使事件池分叉。
    /// </summary>
    public static bool DisableExtraEvents { get; set; }
}
