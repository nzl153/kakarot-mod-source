# 卡卡罗特源码 / Kakarot Source

[中文](#中文) · [English](#english)

---

## 中文

这是《杀戮尖塔 2》可玩角色 Mod「卡卡罗特」的源码协作仓库，用于代码审阅、翻译贡献、Bug 修复和兼容性维护。

仓库只包含 C# 源码、配置和本地化文本；不包含卡图、美术、动画、音频、PCK、DLL、游戏文件、BaseLib 或可直接安装的发布包。完整可玩版本请从 Steam 创意工坊获取。

### 支持范围

本 Mod 同时支持《杀戮尖塔 2》的**正式版**与**测试版（beta）**，源码用条件编译在同一套代码里覆盖两者，分别发布为两条工坊分支。

本仓库的内容与创意工坊上的发布版本保持一致。两条分支所需的游戏版本与 BaseLib 版本并不通用，具体版本要求以创意工坊页面和 `Kakarot.json` 为准。

测试版会提前调整 Mod 接口与多人相关逻辑。提交兼容性问题时，请注明你复现所用的是哪一条分支，以及对应的游戏版本和 BaseLib 版本。

### 构建

需要本机已安装：

- .NET 9 SDK
- Godot .NET SDK 4.5.1
- 《杀戮尖塔 2》（正式版或测试版）
- 对应分支所需的 BaseLib

正式版：

~~~powershell
$env:STS2_GAME_DIR = '<游戏安装目录>'
dotnet build KakarotMod.csproj -c ExportRelease -p:DeployKakarotToMods=false -p:BaseLibDll='<BaseLib.dll 路径>'
~~~

测试版：

~~~powershell
$env:STS2_GAME_DIR = '<游戏安装目录>'
dotnet build KakarotMod.csproj -c ExportRelease -p:Sts2Beta=true -p:DeployKakarotToMods=false -p:BaseLibDll='<BaseLib.dll 路径>'
~~~

`-p:Sts2Beta=true` 会定义 `STS2_BETA` 条件编译符号，切换到测试版的 API 调用路径；不传就是正式版。也可以把游戏目录通过 `-p:Sts2GameRoot='<游戏安装目录>'` 显式传给 MSBuild。构建输出被忽略，不应提交到仓库。

由于此仓库不分发资源包，完整的游戏内测试需要本机已安装正式发布的 Mod 资源。

### 贡献

欢迎提交翻译、可复现的 Bug 修复、兼容性改进和测试结果。请先阅读 CONTRIBUTING.md（中文）。

维护者会逐项审阅提交；提交 Pull Request 不代表一定会被合并或发布。

### 许可与权利

作者源代码以 MIT License 发布。原作、游戏和第三方内容的权利边界见 NOTICE.md。

---

## English

This is the source collaboration repository for **Kakarot**, a playable character mod for *Slay the Spire 2*. It exists for code review, translation contributions, bug fixes, and compatibility maintenance.

The repository contains only C# source, configuration, and localization text. It does **not** contain card art, artwork, animation, audio, PCK files, DLLs, game files, BaseLib, or any directly installable release package. For a playable build, get the mod from the Steam Workshop.

### Support scope

The mod supports both the **release** and **beta** versions of *Slay the Spire 2*. A single codebase covers both through conditional compilation, published as two separate Workshop branches.

This repository stays in step with the published Workshop version. The required game version and BaseLib version are **not** interchangeable between the two branches; refer to the Workshop page and `Kakarot.json` for the exact requirements.

The beta branch changes modding interfaces and multiplayer-related logic ahead of release. When reporting a compatibility issue, state which branch you reproduced it on, along with your game version and BaseLib version.

### Building

You need locally installed:

- .NET 9 SDK
- Godot .NET SDK 4.5.1
- *Slay the Spire 2* (release or beta)
- The BaseLib version required by your target branch

Release:

~~~powershell
$env:STS2_GAME_DIR = '<game install directory>'
dotnet build KakarotMod.csproj -c ExportRelease -p:DeployKakarotToMods=false -p:BaseLibDll='<path to BaseLib.dll>'
~~~

Beta:

~~~powershell
$env:STS2_GAME_DIR = '<game install directory>'
dotnet build KakarotMod.csproj -c ExportRelease -p:Sts2Beta=true -p:DeployKakarotToMods=false -p:BaseLibDll='<path to BaseLib.dll>'
~~~

`-p:Sts2Beta=true` defines the `STS2_BETA` compilation symbol and switches to the beta API call paths; omitting it builds for release. You may also pass the game directory explicitly with `-p:Sts2GameRoot='<game install directory>'`. Build output is gitignored and should not be committed.

Because this repository does not distribute the asset bundle, full in-game testing requires the released mod's assets to be installed locally.

### Contributing

Translations, reproducible bug fixes, compatibility improvements, and test results are welcome. Please read CONTRIBUTING.md first (written in Chinese).

The maintainer reviews submissions individually; opening a pull request does not guarantee it will be merged or shipped.

### License and rights

The author's source code is released under the MIT License. Rights boundaries for the original work, the game, and third-party content are described in NOTICE.md.
