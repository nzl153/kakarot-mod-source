# 贡献指南 / Contributing

[中文](#中文) · [English](#english)

---

## 中文

感谢你愿意参与完善《卡卡罗特》Mod。提交 Issue 或 Pull Request 前，请先阅读以下协作规则。

### 基本原则

- 保持改动小而明确，一次 Pull Request 解决一个问题。
- 不要提交卡图、美术、动画、音频、游戏文件、PCK、DLL、BaseLib 或自动生成目录。
- 正式版与测试版（beta）两条工坊分支都在维护；提交兼容性问题时请注明你复现所用的是哪一条分支。
- 提交后由维护者人工审阅，只有明确同意的改动才会进入发布版本。

### 翻译

本地化位于 Kakarot/localization/{eng,zhs,jpn}。

- 三种语言必须保留完全相同的文件集合与顶层 JSON key。
- 新增 key 时同时补齐英文、简体中文和日文。
- 不要修改卡牌或 Power 的 ID。
- 用语和标点尽量贴近现有语言文件。

### Bug 与兼容性

请在 Issue 或 Pull Request 中提供：

1. 游戏版本，确认是正式版或 beta。
2. BaseLib 版本。
3. 单人或多人；多人时说明是否房主。
4. 已启用的其他 Mod。
5. 可重复的操作步骤。
6. godot.log 中相关的错误段落。

联机相关改动不得从渲染、预览、提示或卡面描述路径写入同步战斗状态。随机逻辑必须使用可同步、可重放的状态来源。

### 平衡建议

平衡问题请优先开 Issue，说明卡组、难度、关键卡牌、回合过程和预期调整方向。不要把大范围数值改动与无关修复混在同一个 Pull Request 中。

---

## English

Thanks for your interest in improving the **Kakarot** mod. Please read the following collaboration rules before opening an Issue or Pull Request.

### Ground rules

- Keep changes small and focused; one Pull Request should solve one problem.
- Do not commit card art, artwork, animation, audio, game files, PCK files, DLLs, BaseLib, or generated directories.
- Both Workshop branches (release and beta) are maintained. When reporting a compatibility issue, state which branch you reproduced it on.
- Submissions are reviewed manually by the maintainer; only explicitly approved changes make it into a published build.

### Translation

Localization lives in `Kakarot/localization/{eng,zhs,jpn}`.

- All three languages must keep an identical set of files and identical top-level JSON keys.
- When adding a key, fill in English, Simplified Chinese, and Japanese at the same time.
- Do not change card or Power IDs.
- Keep wording and punctuation consistent with the existing language files.

### Bugs and compatibility

Please include the following in your Issue or Pull Request:

1. Game version, and whether it is the release or beta branch.
2. BaseLib version.
3. Singleplayer or multiplayer; if multiplayer, whether you were the host.
4. Any other mods you had enabled.
5. Reproducible steps.
6. The relevant error section from `godot.log`.

Multiplayer-related changes must never write synchronized combat state from rendering, preview, hover-tip, or card-description code paths. Randomness must draw from a synchronized, replayable state source.

### Balance suggestions

Please open an Issue first for balance concerns, describing the deck, ascension level, key cards, how the turns played out, and the direction of the adjustment you have in mind. Do not mix broad numerical changes with unrelated fixes in the same Pull Request.
