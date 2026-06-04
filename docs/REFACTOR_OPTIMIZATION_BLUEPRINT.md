# 重构与优化蓝图

生成日期：2026-06-03

本文档基于当前 Godot C# 工程的静态审计结果整理，只作为重构规划与性能优化路线图，不包含代码修改。

## 审计结论摘要

当前项目已经存在一些正确的优化方向，例如：

- `GameBoardScene.TableRender` 已对桌面 `TextureRect` 和 `Panel` 做了对象池。
- `GameBoardScene.HandRender` 已对普通不可交互手牌 slot 做了有限池化。
- `GameAudioManager` 已对 SFX `AudioStreamPlayer` 做了对象池。
- `CaptureScoringRules`、`PotentialCaptureRules`、`GameTurnRules`、`SpecialCardSystem` 已经开始把部分规则从 Godot 节点中抽离。

主要问题不在于缺少局部优化，而在于核心战斗流程仍然集中在 `GameBoardScene` 这个巨型 partial 聚合类中。它跨 75 个 partial 文件，累计约 19,438 行，承担了战斗模型、回合推进、卡牌结算、敌人能力、动画、音频、UI 渲染、资源加载和输入处理等职责。

## 1. 红色警报

### 1.1 `GameBoardScene` 已成为 God Class

位置：

- `scripts/GameBoardScene.cs`
- `scripts/GameBoardScene.CardPlay.cs`
- `scripts/GameBoardScene.TableCaptureFlow.cs`
- `scripts/GameBoardScene.Turns.cs`

问题：

- `PlayCardFromHandAsync` 同时处理出牌、捕获、动画、音效、积分、抽牌、特殊牌事件、敌人能力和 UI 刷新。
- partial 文件拆分主要降低了单文件长度，但没有真正隔离职责。
- 规则逻辑和 Godot 节点操作互相穿透，导致业务逻辑难以单元测试。

风险：

- 后续新增卡牌、敌人能力或动画时，极易扩大回归面。
- async 流程中大量 `turnSequenceGeneration` 检查会继续蔓延。
- UI 表现变化可能破坏核心结算流程。

### 1.2 主菜单与角色帧存在同步资源加载瓶颈

位置：

- `scripts/MainMenuBackgroundAnimator.cs`
- `scripts/FrameAnimationHelpers.cs`
- `scripts/CharacterAvatar.Frames.cs`
- `scripts/GameAudioManager.cs`

问题：

- `MainMenuBackgroundAnimator._Ready()` 同步加载海洋背景帧。当前配置加载 24 帧，目录总量约 45MB。
- `FrameAnimationHelpers.LoadTextureFrames()` 会同步加载纹理帧，并在失败时使用 `Image.LoadFromFile` 兜底。
- `CharacterAvatar.LoadAvatarFrames()` 在角色初始化时同步加载动作帧。
- 项目中没有发现 `ResourceLoader.LoadThreadedRequest`。

风险：

- 主菜单进入和角色首次显示时可能产生明显卡顿。
- `Image.LoadFromFile` 兜底绕过 Godot 导入缓存，不适合作为运行时常规路径。
- 大量帧纹理一次性进入内存，会放大峰值内存占用。

### 1.3 状态机仍由 bool 与 async 链隐式驱动

位置：

- `scripts/GameBoardScene.GameLoopPhase.cs`
- `scripts/GameBoardScene.Turns.cs`
- `scripts/GameBoardScene.ItemCards.cs`
- `scripts/GameBoardScene.MagnetAttachments.cs`

问题：

- 已有 `GameLoopPhaseRules`，但真实状态转移仍由 `_isProcessingCardPlay`、`_isResolvingTurnSequence`、`_pendingItemSelectionMode`、`_turnSequenceGeneration` 等字段组合控制。
- 主玩家行动、AI 回合、道具选择、磁铁选择和游戏结束状态散落在多个 partial 文件中。
- 异步流程取消依赖 generation 检查，而不是统一的回合上下文或取消令牌。

风险：

- 状态组合数量继续增加后，边界 bug 会越来越难复现。
- 新增等待玩家选择类道具时，容易与已有 pending 状态冲突。
- 回合流程难以脱离 Godot 场景做自动化测试。

## 2. 代码拆分规划表

| 原文件/类名 | 当前职责与问题 | 建议拆分出的新类名 | 新类的核心职责 | 耦合解开方案 |
| --- | --- | --- | --- | --- |
| `GameBoardScene` | 战斗模型、UI、动画、音频、AI、结算混在一个 partial 聚合体 | `GameBoardController`、`GameBoardView`、`TurnStateMachine` | 控制流程、渲染绑定、回合状态转移 | Controller 只持有 model/view 接口；View 通过事件回调请求动作 |
| `GameBoardScene.CardPlay` | `PlayCardFromHandAsync` 过长，业务与表现强耦合 | `CardPlayResolver`、`CardPlayPresenter` | 纯规则结算；播放出牌/收牌动画 | Resolver 返回 `CardPlayCommand` / `CardPlayResult`，Presenter 消费命令 |
| `GameBoardScene.TableCaptureFlow` | 捕获、计分、奖励、敌人能力串在同一流程 | `CaptureResolver`、`ScoreService`、`RewardResolver` | 捕获规则、积分、问号牌奖励 | 通过 `CaptureResult` 传递数据，不直接操作节点 |
| `GameBoardScene.*Fx` / `ScoreEffectHelpers` | 大量一次性 `TextureRect`、`Control`、`AnimationPlayer`、`Tween` 创建 | `FxPoolService`、`CardFlightPool`、`FloatingLabelPool` | 统一管理战斗特效节点池 | 业务代码依赖 `IFxPlayer`，不直接创建/销毁节点 |
| `MainMenuBackgroundAnimator` | `_Ready` 同步加载大帧动画 | `BackgroundFrameLoader` | 异步或分帧加载背景纹理 | 先显示静态图，再后台替换为动画帧 |
| `GameAudioManager` | 音频缓存、播放器池、UI 自动绑定集中在一个 autoload | `AudioCache`、`SfxPlayerPool`、`UiAudioBinder` | 音频加载缓存、播放器池、UI 信号绑定 | Autoload 只协调；`UiAudioBinder` 负责 `NodeAdded` 订阅和解绑 |
| `CharacterAvatar` | 头像显示、动作状态、帧加载、攻击 FX 混合 | `AvatarFrameCacheService`、`AvatarActionPlayer` | 角色帧缓存；角色动作播放 | 头像节点只负责展示，帧缓存服务负责资源生命周期 |

## 3. 性能优化清单

### 3.1 CPU 优化

- 为 `MainMenuOceanDynamics._Process()` 增加 `redraw_fps` 节流，参考 `UnderwaterAmbientLayer` 的做法，避免每帧无条件 `QueueRedraw()`。
- 把 `RenderPlayerHands()` 中的可玩性判断、教程高亮、磁铁选择状态计算下沉为手牌 ViewModel，避免渲染函数重复读取大量业务状态。
- 保留 `RenderTableCards()` 的签名缓存和对象池设计，并把类似签名缓存推广到更多高频 UI 刷新点。
- 将动态节点按名称查找，例如 `HandCard_{index}`，改为渲染阶段维护 slot 引用表，减少字符串路径查找。
- `GameAudioManager.BindUiAudioRecursive()` 当前订阅 `GetTree().NodeAdded`，建议补充 `_ExitTree` 解绑，提升热重载和 autoload 重建安全性。
- 对 `debug_performance_trace` 进行扩展：增加分类标签，例如 `Render`、`Animation`、`Rules`、`ResourceLoad`，便于定位实际卡顿来源。

### 3.2 内存与 GC 优化

已有池化对象：

- `GameBoardScene.HandRender` 中的普通手牌 slot。
- `GameBoardScene.TableRender` 中的桌面 `TextureRect` / `Panel`。
- `GameAudioManager` 中的 `AudioStreamPlayer`。

优先新增池化对象：

- 飞行卡牌节点：`CardFlightAnimations`、`OpeningDealFlights`、`CapturedCardsToDeckAnimation`、`DrawAnimations`。
- 浮动分数和伤害 Label：`ScoreEffectHelpers`、`TableCaptureAnimations`。
- 一次性 FX 纹理节点：爆炸、镜片、问号牌、特殊触发、敌人攻击效果。
- 一次性 `AnimationPlayer`：`FrameAnimationHelpers.PlayTextureFrameSlots()` 当前会频繁创建 `AnimationPlayer`、`Animation`、`AnimationLibrary`。

其他 GC 优化：

- `PlayingCard.WithAttachedItemEffect()` 当前会复制字符串数组并维护 CSV，后续可改为小型不可变 id 列表或整型 id 集合。
- 对常用 `NodePath`、`StringName` 和动画名称做静态缓存，避免重复构造。
- 对 `CardEffectValues.Parse()` 的 JSON/字符串解析结果建立资源级缓存，避免运行时重复解析。

## 4. Godot 引擎层级与信号审计

### 4.1 场景树结构

静态场景树不是主要瓶颈：

- `game_board_scene.tscn`：45 个节点，最大深度 3。
- `main_menu_scene.tscn`：33 个节点，最大深度 6。
- `adventure_avatar_menu_scene.tscn`：65 个节点，最大深度 14。

结论：

- 战斗场景静态层级较浅，问题主要来自运行时动态添加的临时节点。
- 冒险菜单 UI 层级较深，但大多是布局容器，不属于战斗热路径。

### 4.2 节点访问

审计结果：

- `GetNode<T>()` 约 130 次。
- `GetNodeOrNull<T>()` 约 65 次。
- 未发现 `FindChild()` 使用。

结论：

- 大部分 `GetNode` 集中在 `_Ready`，不是每帧热路径问题。
- 需要关注的是动态渲染后再用名称查找临时节点，例如手牌 slot、牌堆子节点、桌面卡牌。

建议：

- 对场景中稳定节点继续使用 `%UniqueName` 或 `_Ready` 缓存。
- 对运行时生成节点建立池对象句柄，不再依赖名称字符串反查。

### 4.3 信号连接

审计结果：

- 大量 UI 信号使用 C# 事件/lambda 连接。
- 多数动态可交互节点最终 `QueueFree()`，短期泄漏风险不高。
- `GameAudioManager` 订阅 `GetTree().NodeAdded` 后没有发现对应解绑。

建议：

- 为 autoload 或长生命周期服务补充 `_ExitTree` 解绑。
- 对池化节点禁止保留闭包信号；归还对象池时统一清理可变状态。
- 对通用 UI 音频绑定保持 `UiAudioBoundMeta`，同时将绑定逻辑抽成独立服务。

## 5. 资源加载与异步优化

当前同步加载路径：

- `GD.Load<T>()`：约 30 次。
- `ResourceLoader.Load<T>()`：约 3 次。
- `Image.LoadFromFile`：2 次。
- `LoadThreadedRequest`：0 次。

建议优先级：

1. 主菜单背景帧：改为静态首帧立即显示，剩余帧后台加载。
2. 角色动作帧：按当前敌人、当前玩家、即将显示的角色分批预热，不要一次性全量加载。
3. 战斗 FX 帧：按道具/敌人能力分组加载，进入战斗后分帧 warmup。
4. 音频：现有 SFX 池和缓存可保留；大 BGM 可维持缓存，但避免首次播放时才同步文件读取。

## 6. 三步执行计划

### 第一步：剥离 UI 与动画表现层

风险最低，收益最高。

目标：

- 从 `GameBoardScene` 中抽出 `GameBoardView`。
- 从出牌、收牌、分数飞行、道具特效中抽出 `CardAnimationPresenter` / `FxPresenter`。
- 保持现有业务逻辑不变，只改变节点操作位置。

验收标准：

- `GameBoardScene` 不再直接创建大部分战斗临时 FX 节点。
- `RenderPlayerHands()` 和 `RenderTableCards()` 仍保持现有视觉行为。
- 现有规则测试继续通过。

### 第二步：抽出纯规则服务

目标：

- 抽出 `CardPlayResolver`、`CaptureResolver`、`ScoreService`、`RewardResolver`。
- 输入输出使用纯 C# DTO，不依赖 Godot Node。
- 复用现有 `CaptureScoringRules`、`PotentialCaptureRules`、`GameTurnRules`。

验收标准：

- 出牌、捕获、计分、问号牌奖励可以通过单元测试覆盖。
- `GameBoardScene.CardPlay` 缩减为流程编排，不直接承载结算细节。
- 新增卡牌效果时优先改规则服务，而不是修改 UI 渲染代码。

### 第三步：正式状态机与异步资源系统

风险最高，最后执行。

目标：

- 用 `TurnStateMachine` 替代分散 bool 状态。
- 用 `TurnContext` 或取消令牌替代散落的 `turnSequenceGeneration` 检查。
- 引入 `ResourcePreloadService`，管理菜单背景、角色帧、战斗 FX、音频预热。

验收标准：

- 主玩家回合、AI 回合、待选择、游戏结束状态有明确进入/退出动作。
- 资源加载可以分阶段执行，不阻塞关键场景切换。
- Debug 性能追踪能区分规则、渲染、动画、资源加载耗时。

## 7. 推荐首个实施切入点

建议先从 `GameBoardScene.CardPlay.PlayCardFromHandAsync` 开始，但不要立刻拆规则。第一步只做表现层解耦：

1. 定义 `ICardAnimationPresenter`。
2. 把出牌飞行、捕获飞行、补牌飞行、分数飞行的节点创建迁移到 Presenter。
3. `GameBoardScene` 继续持有当前模型和流程，调用 Presenter 播放动画。
4. 行为稳定后，再拆 `CardPlayResolver`。

这个顺序可以最大限度降低回归风险，同时最快减少 `GameBoardScene` 的 Godot 节点操作密度。
