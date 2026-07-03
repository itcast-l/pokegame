# Deckland 大文件与性能优化方案

Status: planning only
Last updated: 2026-07-02

本文件仅记录优化方案，当前不执行源码、资源、导出配置或测试改动。

## 1. 目标

本轮优化的目标是先降低确定性风险，再做低风险性能收敛，最后治理大文件结构。

优先级顺序：

1. 修复当前测试基线和发布配置风险。
2. 减少明显的每帧重绘、轮询和重复初始化。
3. 收敛导出包资源体积，避免生成素材和调试素材进入运行时包。
4. 拆分 `GameBoardScene` 和 `NetworkManager` 的大文件边界，但不改变玩法规则、联机协议、存档结构或资源 ID。

## 2. 当前检查结论

### 大文件与大类

当前最大自研 C# 文件：

| File | Approx. lines | Judgment |
| --- | ---: | --- |
| `scripts/GameBoardScene.Network.cs` | 2983 | 第一优先级治理对象，网络同步、视觉队列、快照、AI proxy 混在一起 |
| `scripts/NetworkManager.cs` | 2760 | 第一优先级治理对象，LAN、Steam、房间、重连、序列化混在一起 |
| `scripts/UnderwaterAmbientLayer.cs` | 1107 | 动态背景层，已有节流，但仍应统一可见性/低功耗策略 |
| `scripts/GameAudioManager.cs` | 1082 | 大但已有缓存和播放器池，暂不作为第一拆分对象 |
| `scripts/GameBoardScene.cs` | 949 | 主棋盘入口，包含大量状态和 exported setters |
| `scripts/MainMenuOceanDynamics.cs` | 931 | 动态背景层，已有 image-based 路径和 redraw 节流 |

`GameBoardScene*.cs` 当前约 90 个 partial 文件，合计约 26.8k 行。虽然已经拆成 partial，但实际仍是一个巨大类，渲染、动画、网络、规则触发、UI 状态和冒险模式逻辑共享大量私有状态。

### 已经落地的优化

之前文档中的部分 P0 已经完成或部分完成：

- `GameBoardScene._Process` 现在只调用 `UpdateMagnetAttachmentCountdown(delta)` 和 `FlushUiDirtyFlagsIfNeeded()`。
- seat control indicator 和玩家状态 tooltip 已经使用 dirty flag，而不是每帧全量刷新。
- `AdventureMapDynamicLayer` 已经有 `redraw_fps` 节流，并且 image-based 模式只在 fallback 条件下 `QueueRedraw()`。
- 手牌、桌牌、牌堆渲染已有 render signature 或节点池，重复调用时不一定重建节点。

### 新发现的遗漏

需要补进优化范围的遗漏：

- `generated-images` 约 249M，`outputs` 约 116M，其中 `generated-images` 有大量文件被 git 跟踪。
- `export_presets.cfg` 使用 `export_filter="all_resources"`，但未显式排除 `generated-images/**`、`outputs/**` 和部分大型 raw/debug 资产。
- `project.godot` autoload 了 `MCPScreenshot`、`MCPInputService`、`MCPGameInspector`，但导出 preset 又排除了 `addons/godot_mcp/**`。发布包可能引用被排除的 autoload 脚本。
- 多个 UI 装饰控件每帧 `QueueRedraw()`，例如 `OceanMenuButtonFrame`、`OceanDialogPanelArt`、`PlayerCountDialogFrameArt`、`MainMenuPlayerCountDialogPanelArt`。
- 当前规则测试基线不是全绿：`adventure upgrade visual catalog includes palette fields and rarity tier mappings` 失败，期望 12 个 palette 字段，实际 common 条目缺少 4 组 `GlowColor` / `RuleColor`。
- `asset/video/logo_video.ogv` 被场景引用，`asset/video/logo_video.mp4` 未在当前场景引用中发现，应确认是否需要保留在导出包内。

## 3. 非目标

本轮不做以下事项：

- 不改玩法规则。
- 不改联机 RPC payload、协议版本、重连权威模型。
- 不改存档 schema。
- 不重做 UI 视觉风格。
- 不重构第三方 addon 源码。
- 不删除已跟踪素材；先审计引用和导出影响，再决定取消跟踪、移动或仅导出排除。
- 不把性能修复和大架构重写混成一个提交。

## 4. 执行阶段

### P0 - 修复基线与发布风险

目标：先保证后续优化有可靠基线，并消除可能影响发布包启动的配置冲突。

涉及文件：

- `resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres`
- `project.godot`
- `export_presets.cfg`
- `.gitignore`

计划：

1. 修复升级视觉 catalog 的 common 条目，补齐缺失的 `GlowColor` 和 `RuleColor` 字段。
2. 运行规则测试，确认当前失败项消失。
3. 处理 MCP autoload 发布冲突。默认方案：
   - 开发环境保留 Godot MCP plugin。
   - 发布配置不携带 `MCPScreenshot`、`MCPInputService`、`MCPGameInspector` autoload。
   - 如需保留本地调试能力，使用 editor/plugin 注入方式，不让运行时发布包依赖 `addons/godot_mcp/**`。
4. 收紧导出排除规则，至少加入：
   - `generated-images/**`
   - `outputs/**`
   - `**/*.debug-sse.log`
   - 明确不参与运行时的 raw/source/debug 素材目录
5. 更新 `.gitignore`，防止后续继续把生成图和输出目录加入版本控制。

验收：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj --no-restore
```

期望：

- build 通过。
- 规则测试 0 failed。
- 导出配置中不存在“autoload 指向被排除脚本”的冲突。

### P1 - 低风险运行时性能收敛

目标：减少确定性的每帧浪费和重复初始化，不改变游戏表现。

涉及文件：

- `scripts/GameBoardScene.cs`
- `scripts/OceanMenuButtonFrame.cs`
- `scripts/OceanDialogPanelArt.cs`
- `scripts/PlayerCountDialogFrameArt.cs`
- `scripts/MainMenuPlayerCountDialogPanelArt.cs`
- `scripts/UnderwaterAmbientLayer.cs`
- `scripts/MainMenuOceanDynamics.cs`
- `scripts/AdventureMapDynamicLayer.cs`

计划：

1. 给会触发重发牌或重渲染的 exported setters 增加同值短路：
   - `player_count`
   - `item_mode`
   - `winning_score`
   - 其他检查后确认会触发昂贵副作用的 exported 属性
2. 保持 `GameBoardScene` dirty flag 架构，不改玩法逻辑。
3. 将 UI 装饰控件从无条件每帧 `QueueRedraw()` 改为事件驱动或低频节流：
   - hover/focus/pressed/selected 状态变化时重绘。
   - resize 时重绘。
   - 需要持续闪烁的 spark 动画可保留低频 redraw，或只在控件可见时处理。
4. 三个动态背景层统一可见性策略：
   - 不可见时不更新时间和动画。
   - image-based 模式优先更新子 TextureRect，不走 procedural fallback redraw。
   - procedural fallback 只在资源缺失或明确配置为 procedural 时运行。
5. 保持现有 `redraw_fps`，不要为了“更丝滑”提高默认刷新率。

验收：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj --no-restore
```

手动/日志验收：

- 开启 `debug_performance_trace` 后，空闲棋盘不应持续出现高频 `RenderPlayerHands` / `RenderTableCards` 重建日志。
- 主菜单空闲时，UI 装饰控件不应在不可见或无交互状态下持续重绘。
- 背景层隐藏后不应继续做实际动画更新。

### P2 - 资源加载与内存峰值收敛

目标：降低启动、进棋盘和首次播放特效时的资源峰值。

涉及文件：

- `scripts/GameBoardScene.cs`
- `scripts/GameBoardScene.ResourceWarmup.cs`
- `scripts/GameBoardScene.Assets.cs`
- `scripts/FrameAnimationHelpers.cs`
- `scripts/BackgroundFrameLoader.cs`
- `scripts/GameAudioManager.cs`
- `scenes/logo_intro_scene.tscn`

计划：

1. 盘点 `GameBoardScene.cs` 顶部 static frame loads，区分：
   - 开局首屏必需资源。
   - 常见局内资源。
   - 特定角色/特定卡效才需要的资源。
2. 将非首屏、非开局必需的特效帧迁移到 warmup 或首次使用前加载。
3. 保留 `WarmupGameplayResourcesAsync` 的分帧加载策略，增加分类观测：
   - Core SFX
   - Item SFX
   - Character audio
   - Gameplay tension music
   - Optional FX frames
4. 检查 logo 视频资源：
   - `logo_video.ogv` 当前被 `scenes/logo_intro_scene.tscn` 引用，保留。
   - `logo_video.mp4` 若无运行时引用，导出时排除。
5. 对大型背景图和 raw sheet 建立运行时引用清单，只让 processed 版本进入导出包。

验收：

- 启动主菜单、进入游戏局、进入冒险地图均无缺资源错误。
- `debug_performance_trace` 下 warmup 仍分帧执行，不在单帧集中加载大量音频或贴图。
- 导出包不包含无引用的 `.mp4`、生成预览图或 raw/debug 资产。

### P3 - 大文件结构治理

目标：降低维护成本和后续优化风险。只移动代码和抽边界，不改变行为。

涉及文件：

- `scripts/NetworkManager.cs`
- `scripts/GameBoardScene.Network.cs`
- 新增若干 `scripts/NetworkManager.*.cs`
- 新增若干 `scripts/GameBoardScene.Network.*.cs`

`NetworkManager` 拆分建议：

- `NetworkManager.LanDiscovery.cs`
  - LAN search、LAN advertisement、room stale 清理。
- `NetworkManager.Lobby.cs`
  - host/join/ready/player list/start match。
- `NetworkManager.Steam.cs`
  - Steam lobby、invite、metadata。
- `NetworkManager.Reconnect.cs`
  - reconnect state、seat claim、retry、host ended match。
- `NetworkManager.Serialization.cs`
  - `ToDictionary`、`ParseConfig`、`ParsePlayer`、variant array helpers。

`GameBoardScene.Network` 拆分建议：

- `GameBoardScene.NetworkSnapshot.cs`
  - snapshot build/apply/read helpers。
- `GameBoardScene.NetworkVisualQueue.cs`
  - realtime visual event queue、queued snapshot、catchup completion。
- `GameBoardScene.NetworkPrediction.cs`
  - client predicted hand/item/pending selection rollback。
- `GameBoardScene.NetworkAuthority.cs`
  - host action validation and broadcast entrypoints。
- `GameBoardScene.NetworkAiProxy.cs`
  - AI handoff、pending selection fallback、proxy turn recovery。

执行规则：

- 每次只拆一个边界。
- 拆分提交中不改行为、不改命名风格、不改 RPC payload。
- 拆完立即跑 build 和规则测试。
- 触碰重连/seat control/pending selection 时额外跑联机 smoke suite。

验收：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj --no-restore
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

期望：

- 行为测试保持通过。
- `NetworkManager.cs` 和 `GameBoardScene.Network.cs` 体量明显下降。
- 新 partial 文件职责单一，命名与现有 `GameBoardScene.*.cs` 风格一致。

### P4 - 资源与导出审计自动化

目标：防止同类问题回流。

涉及文件：

- 可新增 `tools/audit_export_resources.sh` 或同等脚本。
- 可新增 `docs/RESOURCE_EXPORT_AUDIT.md` 记录规则。

计划：

1. 新增只读审计脚本，检查：
   - 被 git 跟踪的 `generated-images/**`、`outputs/**` 文件。
   - 被 git 跟踪的 `*.debug-sse.log`。
   - 被 git 跟踪的 raw/source 大图。
   - `project.godot` autoload 是否指向 export preset 排除路径。
   - `export_presets.cfg` 是否遗漏生成目录排除。
2. 审计脚本只报告，不自动删除、不自动修改。
3. 将审计命令加入优化验收清单。

验收：

```bash
bash tools/audit_export_resources.sh
```

期望：

- 脚本能列出风险项并返回清晰状态。
- 后续新增生成素材时可以快速发现。

## 5. 测试矩阵

基础命令：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj --no-restore
```

联机相关改动额外运行：

```bash
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

视觉/布局相关改动按触碰范围选择：

```bash
bash tests/run_game_board_responsive_layout_audit.sh
bash tests/run_game_board_mode_regression_smoke.sh
bash tests/run_game_board_scene_authoring_defaults_smoke.sh
bash tests/run_vertical_backdrop_resource_audit.sh
bash tests/run_localization_layout_audit.sh
```

资源/导出相关改动额外检查：

```bash
rg -n "MCPScreenshot|MCPInputService|MCPGameInspector|addons/godot_mcp" project.godot export_presets.cfg
git ls-files generated-images outputs
git ls-files "art/**/*.log"
```

## 6. 完成标准

P0 完成标准：

- 规则测试恢复全绿。
- 发布配置不再存在 MCP autoload 与导出排除冲突。
- 生成目录、输出目录和大型 debug 文件不会进入导出包。

P1 完成标准：

- 空闲 UI 装饰层不再无条件每帧重绘。
- exported setter 重复赋同值不会触发重发牌或重渲染。
- 动态背景层在不可见时不做实际动画更新。

P2 完成标准：

- 首屏和进入棋盘的资源加载峰值下降。
- 非必需特效帧延迟到 warmup 或首次使用路径。
- 无引用视频/生成素材不进入导出包。

P3 完成标准：

- `NetworkManager.cs` 和 `GameBoardScene.Network.cs` 明显变小。
- 新拆出的 partial 文件职责清晰。
- 联机重连、pending selection、seat control 相关测试仍通过。

P4 完成标准：

- 有一条可重复运行的资源/导出审计命令。
- 审计脚本只报告风险，不做破坏性修改。

## 7. 建议执行顺序

建议按以下顺序执行：

1. P0 catalog 测试修复。
2. P0 MCP autoload 与导出排除修复。
3. P0/P4 资源导出审计脚本。
4. P1 exported setter 同值短路。
5. P1 UI 装饰层重绘节流。
6. P1 动态背景层可见性策略统一。
7. P2 特效帧和视频资源加载收敛。
8. P3 `NetworkManager` partial 拆分。
9. P3 `GameBoardScene.Network` partial 拆分。

原因：

- 前三步先建立可靠基线和防回流能力。
- 中间三步收益高、风险低。
- 最后两步是结构治理，必须建立在测试和导出基线稳定之后。

## 8. 当前已知风险

- `dotnet build pokegame.sln` 当前通过，但第三方 `phantom_camera` 有 4 个 warning。本轮先记录，不改第三方源码。
- 规则测试当前有 1 个失败，必须先修复，否则后续无法判断优化是否引入回归。
- `generated-images` 已有文件被 git 跟踪，清理时不能直接删除，需要先确认哪些是文档/对比所需素材。
- MCP autoload 是开发便利功能，处理时要避免破坏本地调试流程。
- 大文件拆分容易误改行为，必须严格按“移动代码优先、行为不变”的方式推进。
