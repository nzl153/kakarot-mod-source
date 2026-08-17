# 卡卡罗特源码

这是《杀戮尖塔 2》可玩角色 Mod「卡卡罗特」的源码协作仓库，用于代码审阅、翻译贡献、Bug 修复和兼容性维护。

仓库只包含 C# 源码、配置和本地化文本；不包含卡图、美术、动画、音频、PCK、DLL、游戏文件、BaseLib 或可直接安装的发布包。完整可玩版本请从 Steam 创意工坊获取。

## 支持范围

- 支持版本：Steam 正式版与 beta 分支（beta 已适配至 `v0.111.0`）
- 依赖版本：与当前正式版兼容的 BaseLib 稳定版本
- 更新策略：本仓库会随创意工坊的正式发布同步更新
- 分发方式：同一个创意工坊物品挂两份构建，Steam 按玩家所在分支自动派发

两个分支共用同一份源码，靠条件编译产出两份 dll，差异全部收在 `#if STS2_BETA` 内，不复制源码目录。

```bash
dotnet build -p:DefineConstants=STS2_BETA   # beta 分支
dotnet build                                 # 正式版（默认）
```

⚠️ 玩家切换游戏分支后需要**重启 Steam**，创意工坊内容才会刷新成对应分支的版本；
否则可能提示需要更高的游戏版本，或者 Mod 加载不出来。

提交兼容性问题时，请注明你所在的分支（正式版 / beta）与游戏版本号。两个分支的引擎接口存在差异，同一个现象在两边的成因往往完全不同。

## 构建

需要本机已安装：

- .NET 9 SDK
- Godot .NET SDK 4.5.1
- Steam 正式版《杀戮尖塔 2》
- 与当前 Steam 正式版兼容的 BaseLib 稳定版本

示例：

~~~powershell
$env:STS2_GAME_DIR = '<游戏安装目录>'
dotnet build KakarotMod.csproj -c ExportRelease -p:DeployKakarotToMods=false -p:BaseLibDll='<BaseLib.dll 路径>'
~~~

也可以把游戏目录通过 -p:Sts2GameRoot='<游戏安装目录>' 显式传给 MSBuild。构建输出被忽略，不应提交到仓库。

由于此仓库不分发资源包，完整的游戏内测试需要本机已安装正式发布的 Mod 资源。

## 贡献

欢迎提交翻译、可复现的 Bug 修复、兼容性改进和测试结果。请先阅读 CONTRIBUTING.md。

维护者会逐项审阅提交；提交 Pull Request 不代表一定会被合并或发布。

## 许可与权利

作者源代码以 MIT License 发布。原作、游戏和第三方内容的权利边界见 NOTICE.md。
