#nullable enable
using BaseLib.Config;

namespace KakarotMod.KakarotCode.Config;

/// <summary>
/// 卡卡罗特的 mod 设置。继承 <see cref="SimpleModConfig"/>：BaseLib 会按属性自动生成 UI
/// （bool → 勾选框）、自动加「恢复默认」按钮、自动读写用户数据目录\mod_configs\ 下的 cfg，
/// 不需要自己写界面或存档逻辑。
///
/// 🚨 <b>配置属性必须是 static。</b>BaseLib 扫描时会跳过实例属性并打日志
/// 「Ignoring &lt;Mod&gt; property X: only static properties are supported」，
/// 结果是配置项数为 0、Register 判定「无可见设置」直接不登记，整个配置面板不出现。
///
/// 标签文案走 settings_ui.json，键为 <c>KAKAROTMOD-&lt;属性名转下划线大写&gt;.title</c>；
/// 查不到本地化时基类回退显示属性名本身，不会崩、也不影响功能。
/// </summary>
public sealed class KakarotModConfig : SimpleModConfig
{
    /// <summary>
    /// 弗利萨挑战对全角色开放。默认 false = 只有队伍里有卡卡罗特时才触发。
    /// 开启后任何角色走到建筑师事件都会被替换成弗利萨挑战（事件本身仍可选择拒绝）。
    /// 战斗不发任何奖励、不碰卡池，故对其他角色天然安全，无需额外守卫。
    /// </summary>
    public static bool FriezaForAllCharacters { get; set; }

    /// <summary>
    /// 关闭弗利萨挑战以外的本 mod 事件。默认 false = 照常出现。
    /// 只作用于走事件池的三个事件（不可思议的冒险 / 卡林塔试炼 / 武道会食堂），
    /// 弗利萨挑战不受影响——它 IsAllowed 恒 false、不进池子，靠 EnterRoom 补丁换房。
    ///
    /// 🚨 <b>刻意用「关闭」而不是「启用」，默认必须是 false。</b>
    /// BaseLib 读不到 cfg 时 bool 落在 false，若写成「启用事件」默认 true，
    /// 一旦某次读取失败就会静默把三个事件全关掉；写成「关闭」则读取失败等于「不关闭」，
    /// 行为退化成现状，坏的方向是安全的。
    ///
    /// 🚨 <b>仅单人生效</b>（同弗利萨开关）：IsAllowed 是各端本地跑的，
    /// 联机时两边配置不同会让事件池不一致，抽取结果可能分叉导致不同步。
    /// </summary>
    public static bool DisableExtraEvents { get; set; }
}
