# Code Health Optimization Execution Plan

Last updated: 2026-06-20

本文件把当前项目代码体检结论转成一份可以直接执行的整改文档。
目标不是重复“发现了哪些问题”，而是把每一项可调整内容拆成：

- 明确目标
- 明确边界
- 明确改动步骤
- 明确验证方式
- 明确常见偏差
- 明确完成标准

默认读者：

- 未来的自己
- 新加入项目的协作者
- 临时代码支持同学

默认原则：

- 先做低风险、高收益的调整
- 先消除每帧浪费，再做结构治理
- 不把“性能修复”和“架构重写”混成一个大改动
- 不在整改过程中顺手改玩法、规则、资源命名或 UI 风格

## 0. Read This First

正式执行本文档前，建议先快速通读以下文件：

- [QUICKSTART.md](../QUICKSTART.md)
- [README.md](../README.md)
- [NETWORK_AUTHORITY_MODEL.md](./NETWORK_AUTHORITY_MODEL.md)
- [HIGH_PRIORITY_SPRINT_TASK_BREAKDOWN.md](./HIGH_PRIORITY_SPRINT_TASK_BREAKDOWN.md)

原因：

- `QUICKSTART.md` 和 `README.md` 给出当前项目认可的本地 build / test 入口
- `NETWORK_AUTHORITY_MODEL.md` 固定了联机权责边界，避免整改时误改权威规则
- `HIGH_PRIORITY_SPRINT_TASK_BREAKDOWN.md` 的风格与本文档一致，便于对齐执行颗粒度

如果当前任务涉及：

- 快照
- reconnect
- seat control
- pending selection

则 `NETWORK_AUTHORITY_MODEL.md` 视为必读，而不是可选参考。

## 1. Current Confirmed Facts

以下事实来自 2026-06-20 的本地检查结果，后续执行时默认以这里为基线：

- `dotnet build pokegame.sln` 通过
- 当前构建结果为 `0 warning / 0 error`
- `tests/Rules.Tests/Rules.Tests.csproj` 是一个自定义可执行测试入口，不是标准测试框架工程
- 当前稳定的规则测试命令应使用：
  - `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
- 多人联机回归的现有稳定入口应使用：
  - `bash tests/run_multiplayer_reconnect_smoke_suite.sh`
- 当前最需要关注的是 `scripts/` 下自研代码，不是第三方 `addons/`

确认的大文件：

| File | Approx. Lines | Current Judgment |
| --- | ---: | --- |
| `scripts/GameBoardScene.Network.cs` | 2973 | 大且需要治理 |
| `scripts/NetworkManager.cs` | 2693 | 大且需要治理 |
| `scripts/UnderwaterAmbientLayer.cs` | 1107 | 大且可能存在运行时浪费 |
| `scripts/GameAudioManager.cs` | 1012 | 大但暂时可控 |
| `scripts/GameBoardScene.cs` | 866 | 大且处于主循环中心 |
| `scripts/MainMenuOceanDynamics.cs` | 761 | 大且存在持续更新逻辑 |
| `scripts/GameSaveStore.cs` | 759 | 大但暂时可控 |
| `scripts/GameInputActions.cs` | 684 | 中高体量，暂不优先处理 |
| `scripts/GameBoardScene.CardPlay.cs` | 672 | 大，后续随 `GameBoardScene` 一并治理 |
| `scripts/GameBoardScene.HandRender.cs` | 652 | 大，后续随 `GameBoardScene` 一并治理 |

当前确认的主要优化点：

1. `GameBoardScene` 主循环存在逐帧 UI 刷新
2. `AdventureMapDynamicLayer` 存在无条件 `QueueRedraw()`
3. `GameBoardScene` 到 `NetworkManager` 的访问方式分散且重复
4. `NetworkManager` 内存在一些可避免的排序、查找和临时分配
5. `GameBoardScene` 虽然已拆成 partial，但职责仍高度耦合
6. 多个环境动态层脚本存在重复模式，后续适合统一治理

## 2. Scope And Non-Goals

### In Scope

- 降低明显的逐帧浪费
- 降低高频路径中的重复工作
- 明确大文件治理顺序
- 为后续拆分准备稳定边界
- 固定每一阶段的验收方式

### Out Of Scope

以下内容不应混入本轮执行：

- 新玩法
- 新卡效
- 联机权威模型改写
- 资源命名改造
- UI 视觉风格重做
- 第三方 `addons/` 重构
- 为“以后可能会用”而新增抽象

### Change Discipline

执行时必须遵守：

1. 每个 workstream 单独提交，不把多个主题塞进一个大提交
2. 优先做行为不变的整改
3. 如果某一步开始影响规则结算、联机权威或资源目录，立即停下，拆成单独设计题
4. 任何“顺手清理”都不是默认允许项

## 3. Recommended Execution Order

必须按以下顺序推进，不建议跳步：

1. `P0` 去掉 `GameBoardScene` 中不必要的逐帧 UI 刷新
2. `P0` 修复 `AdventureMapDynamicLayer` 的无条件重绘
3. `P1` 收敛 `GameBoardScene` 对 `NetworkManager` 的重复根节点查找
4. `P1` 收敛 `NetworkManager` 的可避免排序和短生命周期分配
5. `P2` 对 `GameBoardScene` 做真正的职责拆分
6. `P2` 统一三个环境动态层脚本的更新和重绘策略

原因：

- 前两项收益最高、风险最低
- 中间两项能为后续拆分降低耦合
- 后两项属于结构治理，必须建立在前面行为稳定的前提上

## 4. Baseline Before Any Change

任何人正式开始整改前，先执行并记录一次基线。

必须执行：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

建议执行：

```bash
rg --files scripts -g '*.cs' -g '*.gd' | xargs wc -l | sort -nr | head -n 40
```

如果本轮会碰到任一联机相关文件，额外执行：

```bash
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

建议记录：

- 当前构建是否通过
- 当前规则测试是否通过
- 当前主要大文件行数
- 本轮准备处理的文件列表
- 是否只处理本文档指定范围

如果有 Godot Profiler 可用，建议额外记录：

- `GameBoardScene._Process`
- `AdventureMapDynamicLayer._Process`
- `UnderwaterAmbientLayer._Process`
- `MainMenuOceanDynamics._Process`

注意：

- Profiler 数据不是本轮开工前置条件
- 但如果要向其他协作者证明优化有效，建议保留一组前后对比截图或文字记录

## 5. Workstream P0-A - Remove Per-Frame UI Polling In GameBoardScene

### Goal

把当前 `GameBoardScene` 中“每帧刷新 UI 状态”的逻辑，改成“数据变化时刷新 + 布局变化时刷新”，前提是不改变现有玩法和联机规则。

### Files In Scope

- `scripts/GameBoardScene.cs`
- `scripts/GameBoardScene.SeatControlIndicators.cs`
- `scripts/GameBoardScene.PlayerStatusTooltip.cs`
- `scripts/GameBoardScene.NetworkUi.cs`
- `scripts/GameBoardScene.Localization.cs`
- 任何直接触发 seat control 状态变化或玩家分数变化的 `GameBoardScene.*.cs`

### Why This Is P0

当前 [GameBoardScene.cs] 里的 `_Process` 会每帧执行：

- `UpdateMagnetAttachmentCountdown(delta)`
- `UpdateReconnectOverlay()`
- `RefreshAllPlayerSeatControlIndicators()`
- `RefreshActivePlayerStatusTooltip()`

其中：

- 倒计时类 UI 逐帧更新是合理的
- 断线遮罩逐帧检查不是首要问题，但仍可后续收敛
- seat control indicator 不应逐帧全量刷新
- tooltip 不应在没有内容变化时每帧重新写文本和布局

### Mandatory Success Criteria

- `GameBoardScene._Process` 不再每帧调用 `RefreshAllPlayerSeatControlIndicators()`
- 只有在以下事件发生时才刷新 seat control indicator：
  - seat control 状态变化
  - `player_count` 变化
  - 本地视角映射变化
  - 语言变化导致状态文案变化
  - 玩家区域布局变化
- tooltip 只在以下时机刷新内容：
  - tooltip 首次显示
  - 当前目标玩家分数变化
  - 当前目标玩家 seat control 状态变化
  - tooltip 目标切换
- tooltip 只在以下时机刷新位置：
  - tooltip 显示时
  - 目标 UI 区域位置变化时
  - 父容器尺寸变化时
- 不能改变现有 UI 文案
- 不能改变 tooltip 出现/消失时机

### Implementation Steps

#### Step 1 - Find All True Refresh Triggers

先不要删 `_Process` 里的刷新调用，先确认真实触发点。

至少检查以下触发路径：

- seat control 状态更新
- reconnect overlay 状态切换
- 本地 reclaim pending -> connected 流转
- 分数变化
- 回合切换导致的视角映射变化
- 本地语言切换
- `player_count` 变化

产出物：

- 一份“谁负责触发 indicator refresh / tooltip refresh”的对应表

如果没有做完这一步，不要直接改成事件驱动，否则高概率漏刷新。

#### Step 2 - Introduce Dirty Flags Instead Of Immediate Deep Refresh

建议增加最小化脏标记，而不是立刻在每个地方直接深刷新。

推荐脏标记：

- `_seatControlIndicatorsDirty`
- `_seatControlIndicatorLayoutDirty`
- `_activePlayerTooltipContentDirty`
- `_activePlayerTooltipLayoutDirty`

原则：

- 内容变化和布局变化分开
- 能只刷新一个玩家时，就不要全量刷新
- 能只刷新布局时，就不要重写文本和 style

不要做的事：

- 不要新建一套泛化 UI invalidation framework
- 不要为了 4 个控件引入复杂事件总线

#### Step 3 - Replace Per-Frame Full Refresh

把 `_Process` 中的：

- `RefreshAllPlayerSeatControlIndicators()`
- `RefreshActivePlayerStatusTooltip()`

改为只在脏标记为真时刷新。

推荐做法：

- `_Process` 中保留一个很薄的 `FlushUiDirtyFlagsIfNeeded()`
- 如果无脏标记，直接返回
- 如果只有布局脏，只做布局
- 如果只有 tooltip 内容脏，只刷新当前 active tooltip

不要做的事：

- 不要把 `UpdateMagnetAttachmentCountdown(delta)` 和该次改动绑在一起重构
- 不要顺手改 reconnect overlay 文案逻辑

#### Step 4 - Wire Dirty Flags To Real Events

必须把脏标记接到实际变更点上。

至少覆盖：

- seat control 状态应用完成后
- 网络快照应用后
- 本地 reclaim 状态晋升后
- 玩家分数更新后
- 玩家区域重布局后
- 语言刷新入口执行后

如果执行者无法 100% 确认某个状态入口是否影响 indicator，优先多打一处脏标记，也不要漏标。

#### Step 5 - Keep Tooltip Behavior Exact

tooltip 行为很容易在“优化”中被误改。

必须保持：

- hover 延迟仍为当前逻辑
- 淡入淡出时长不变
- 文案拼接格式不变
- 当前 active tooltip 在分数变化时仍会同步更新

重点检查：

- 鼠标进入玩家区域后，tooltip 是否仍能稳定出现
- hover 过程中分数或 seat 状态变化时，tooltip 是否会正确更新
- 鼠标离开后，tooltip 是否仍会正确隐藏
- 多次快速移入移出玩家区域时，delay timer 是否仍稳定，不会残留错误 tooltip

### Common Execution Mistakes

1. 只删 `_Process` 刷新，不补脏标记来源
   结果：状态显示卡住

2. 把所有刷新都并入一个大 `RefreshUi()`
   结果：表面上不再逐帧刷新，实际上只是换了入口，复杂度更高

3. 只考虑 seat control 文案变化，不考虑布局变化
   结果：玩家视角变换或容器尺寸变化后控件位置不对

4. tooltip 只在显示时刷新一次
   结果：显示期间数据变化时内容过期

### Verification

必须执行：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

必须手动验证：

1. 进入普通对局
2. 检查所有玩家 seat control indicator 是否显示正常
3. 触发网络断线 / reclaim 相关场景时，indicator 是否即时变化
4. hover 玩家区域，tooltip 是否出现
5. tooltip 显示期间改变分数或状态时，内容是否更新
6. 改变窗口大小或触发布局变化后，indicator 和 tooltip 位置是否仍正确
7. 切换语言后，状态文案是否刷新

完成标准：

- 主行为保持不变
- 每帧不再全量刷新 indicator
- 没有出现“文案不刷新、位置不刷新、偶现不显示”三类回归

如果本轮修改波及联机状态展示路径，追加执行：

```bash
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

## 6. Workstream P0-B - Fix Unconditional Redraw In AdventureMapDynamicLayer

### Goal

去掉 `scripts/AdventureMapDynamicLayer.cs` 中无条件 `QueueRedraw()`，让其重绘策略与真实渲染模式一致。

### Why This Is P0

当前 [AdventureMapDynamicLayer.cs] 的 `_Process` 末尾无条件 `QueueRedraw()`。
这意味着即使：

- 当前是 image-based 模式
- 当前控件不可见
- 当前数据没有变化

也仍会持续请求重绘。

这是一个单点明确、行为边界清晰、收益稳定的优化项。

### Mandatory Success Criteria

- `_Process` 不能无条件 `QueueRedraw()`
- procedural 模式下，只有需要持续动画时才请求重绘
- image-based 模式下，只更新实例，不额外走 procedural redraw
- 不可见时不应继续推进无意义重绘
- 视觉表现不能明显退化

### Implementation Steps

#### Step 1 - Align Behavior With Other Dynamic Layers

先对比：

- `scripts/MainMenuOceanDynamics.cs`
- `scripts/UnderwaterAmbientLayer.cs`

目标不是强行提取公共父类，而是先把 `AdventureMapDynamicLayer` 的 redraw 节奏收敛到同类脚本的水平。

#### Step 2 - Add Visibility Guard

如果控件当前不可见，直接返回，不做无意义更新。

注意：

- 如果 image instance 需要首次构建，允许在必要时做一次初始化
- 但不要因为“可能稍后显示”就持续重绘

#### Step 3 - Separate Procedural And Image-Based Paths

推荐结构：

- procedural：
  - 更新时间
  - 按需要 `QueueRedraw()`
- image-based：
  - 更新时间
  - 更新 image instances
  - 不进入 procedural redraw 路径

#### Step 4 - If Needed, Introduce A Redraw Interval

如果 procedural 模式实际上不需要满帧 `QueueRedraw()`，可以参考其他动态层加一个可控的 redraw interval。

但要注意：

- 不要在同一改动里引入新的可配置导出参数，除非确有必要
- 优先复用现有脚本风格，而不是重新设计参数系统

### Common Execution Mistakes

1. 直接删除 `QueueRedraw()`，但忘了 procedural 模式仍需要动画
   结果：画面静止

2. 不区分 procedural 和 image-based
   结果：修掉一个浪费，又引入另一个显示问题

3. 只在 `_Draw()` 里判断，`_Process()` 里仍持续推进高频逻辑
   结果：CPU 浪费仍在，只是没有重绘

### Verification

必须执行：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

必须手动验证：

1. 进入 adventure map 场景
2. procedural 模式下观察动态层是否仍正常动画
3. image-based 模式下观察实例动画是否仍正常
4. 场景切换、隐藏、重新显示后，实例是否能恢复
5. 不调整任何美术资源的前提下，视觉表现是否与修改前一致

完成标准：

- 无条件重绘被移除
- 动画仍正常
- 不可见状态下不再做无意义 redraw

## 7. Workstream P1-A - Centralize NetworkManager Access In GameBoardScene

### Goal

减少 `GameBoardScene.*` 各个 partial 文件里散落的 `GetNodeOrNull<NetworkManager>("/root/NetworkManager")`，降低耦合和重复查找。

### Files In Scope

- `scripts/GameBoardScene.NetworkSession.cs`
- `scripts/GameBoardScene.Network.cs`
- `scripts/GameBoardScene.NetworkUi.cs`
- `scripts/GameBoardScene.Input.cs`
- `scripts/GameBoardScene.PauseMenu.cs`
- 其他直接重复查找 `NetworkManager` 的 `GameBoardScene.*.cs`

### Why This Is P1 Instead Of P0

这是明确的代码质量改进，但不是当前最大的运行时浪费。
它更重要的价值是：

- 降低阅读成本
- 降低未来拆分类时的依赖分散度
- 方便统一空值处理和生命周期判断

### Mandatory Success Criteria

- `GameBoardScene` 内存在统一的 `GetNetworkManager()` 或缓存访问入口
- 重复根路径字符串不再散落在大量 partial 文件中
- 行为不变
- 不引入 stale reference 问题

### Implementation Steps

#### Step 1 - Add A Single Access Point

推荐最小方案：

- 在 `GameBoardScene` 主类中增加一个统一访问方法
- 如有必要，增加一个在 `_Ready()` 期间初始化的缓存字段

建议优先级：

1. 先统一入口
2. 再决定是否缓存实例

如果执行者无法确认 `NetworkManager` 生命周期是否绝对稳定，优先做统一入口，不强行上缓存。

#### Step 2 - Replace Repeated Root Lookups

把所有：

- `GetNodeOrNull<NetworkManager>("/root/NetworkManager")`

替换为统一入口。

要求：

- 本轮只改访问方式
- 不顺手改业务条件分支
- 不顺手重命名大量方法

#### Step 3 - Normalize Null Handling

统一入口后，把常见判空逻辑收敛：

- 没找到 `NetworkManager` 时的默认返回
- 联机状态不可用时的降级逻辑

注意：

- 不要在这一步引入异常抛出
- 当前代码明显倾向于容错式返回，本轮保持一致

### Verification

必须执行：

```bash
dotnet build pokegame.sln
```

必须手动验证：

1. 单机进入牌局
2. 联机进入牌局
3. 出牌、道具、暂停、断线提示等入口是否仍正常

完成标准：

- `GameBoardScene.*` 中重复根节点查找明显下降
- 未引入空引用异常
- 联机与单机入口行为一致

如果本轮改动波及联机场景入口，追加执行：

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

## 8. Workstream P1-B - Reduce Avoidable Allocation And Sorting In NetworkManager

### Goal

在不改权威模型的前提下，降低 `NetworkManager` 中容易造成短生命周期分配和重复排序的代码。

### Files In Scope

- `scripts/NetworkManager.cs`
- 可能受影响的只读消费端：
  - `scripts/MultiplayerLobbyScene.cs`
  - `scripts/GameBoardScene.Network.cs`

### Important Constraint

本项不是“重写网络层”。
只允许做：

- 局部去重
- 局部缓存
- 局部排序收敛
- 局部序列化收敛

不允许做：

- 改 RPC 边界
- 改快照结构
- 改 Host authority 规则
- 改 reconnect 状态机

### Mandatory Success Criteria

- 高频或中高频路径中，不再反复做相同排序
- 明确哪些 `ToArray()/OrderBy()/FirstOrDefault()/Any()` 保留，哪些应收敛
- 对外行为完全不变

### P1-B Audit Decision (2026-07-15)

本轮只收敛已确认会重复发生的状态排序：

- `EnsureLobbySeatControlStates()` 通过 seat-order 插入保持 `_seatControlStates` 顺序，不再每次调用后全表 `Sort`。
- 重连快照解码器按 `seat_index` 写入定长数组并拒绝重复/越界 seat，因此 `ApplySeatControlStatesFromAuthoritySnapshot()` 直接消费该顺序，不再重复 `OrderBy`。
- 检查点的 reconnect claims 正常路径沿已排序的 `_seatControlStates` 读取；仅在内存中的 seat/control 配对失效时保留排序兜底。

以下操作保留，不作为本轮优化目标：

- `GetRoomsVariant()`、`GetPlayersVariant()`、`GetSeatControlStatesVariant()` 的 Godot 容器构造是 UI/信号桥接冷路径，不能通过缓存引入失效状态。
- 迁移恢复阶段对 checkpoint roster/control 的 `OrderBy` 属于边界恢复冷路径，用于接受外部检查点的任意合法顺序。
- standby checkpoint 的 `bytes.ToArray()` 用于保留独立 payload 所有权，不能删除。
- 当前玩家数量上限较小，未观察到足以支持额外索引字典的重复线性查找热点。

### Implementation Steps

#### Step 1 - Distinguish Hot Path From Cold Path

先分清楚：

- 哪些方法只在 UI 或测试读取时调用
- 哪些方法会在联机状态变更时频繁触发

例如：

- `GetPlayersVariant()`
- `GetSeatControlStatesVariant()`
- `GetRoomsVariant()`

如果某方法只在低频 UI 刷新时调用，则不必为了“理论性能”过度复杂化。

#### Step 2 - Remove Repeated Same-Shape Sorting

对 `_players` 和 `_seatControlStates`，如果其自然顺序本来就能保持 seat order，优先在写入时维持顺序，而不是在每次导出 variant 时 `OrderBy(...)`。

如果当前无法保证容器内部顺序稳定，则：

- 可在状态改变后重建一次缓存视图
- 不要在每次读取时重新排序

#### Step 3 - Replace Repeated Linear Lookups Only Where It Helps

像：

- `FirstOrDefault(player => player.SeatIndex == ...)`
- `Any(player => player.PeerId == ...)`

如果确实位于多次调用路径，可考虑增加最小辅助索引。

但必须注意：

- 不要为了少量数据结构引入多份难维护索引
- 当前玩家上限较小，复杂度不是唯一问题，可读性同样重要

推荐原则：

- 有明确重复查找热点，再加索引
- 没有明确热点，不要先发明索引系统

#### Step 4 - Review Variant Serialization

检查以下导出逻辑是否存在“无意义复制”：

- `new Godot.Collections.Array<int>(targetIndexes.ToArray())`
- `new Godot.Collections.Array<string>((attachedItemCardIds ?? Array.Empty<string>()).ToArray())`

如果能直接使用原集合安全构造，则优先减少中间数组。

前提：

- 不破坏 Godot 互操作需要的类型形状
- 不改变对外序列化内容

### Common Execution Mistakes

1. 用缓存换来更复杂的失效逻辑
   结果：性能也许更好一点，但 bug 风险大幅上升

2. 一次性引入过多索引字典
   结果：写路径变复杂，维护成本高于收益

3. 把冷路径当热路径优化
   结果：代码变难读，真实收益很小

### Verification

必须执行：

```bash
dotnet build pokegame.sln
```

建议执行：

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

必须手动验证：

1. 房间列表刷新
2. 玩家列表刷新
3. ready 状态变化
4. 断线重连相关 seat control 展示
5. 进入牌局前后的玩家数据同步

完成标准：

- 行为不变
- 没有因缓存失效导致的状态显示错误
- 代码复杂度没有明显上升

## 9. Workstream P2-A - Turn GameBoardScene Partial Split Into Real Responsibility Split

### Goal

把当前“一个超大 partial 类”推进为“主场景 + 少量明确职责组件/服务”的结构。

### Why This Is P2

这是当前最大的结构性问题，但不是应该最先动手的问题。
如果在逐帧浪费和依赖收敛完成前直接大拆：

- diff 会巨大
- 回归面会过宽
- 很难判断问题来自结构调整还是行为修改

### Structural Problem To Solve

当前 `GameBoardScene.*` 总量接近 `25000` 行。
虽然按主题分到了很多 partial 文件，但：

- 共享字段仍大量存在
- 主状态仍集中在同一个大类
- 调用边界不清晰
- 很难单测
- 很难局部替换

### Recommended Split Target

建议最终收敛为以下几类角色：

1. `BoardState And Rules Coordination`
   - 权威本地棋盘状态
   - 回合推进
   - 规则结算协调

2. `BoardVisualPresentation`
   - 发牌、飞牌、得分、洗牌、特效呈现

3. `BoardNetworkBridge`
   - 快照应用
   - 视觉事件同步
   - reconnect catch-up

4. `BoardUiOverlays`
   - pause menu
   - reconnect overlay
   - seat indicators
   - tooltip

5. `BoardInputCoordinator`
   - 本地输入合法性判断
   - 点击目标选择
   - 本地出牌请求入口

注意：

- 上述是职责分组，不是要求一次性创建 5 个新系统
- 真正执行时可以先拆 1 到 2 个最边缘的组件

### Mandatory Success Criteria

- 不再继续新增大型 partial 文件作为主要扩容方式
- 每次拆分只拿下一个明确领域
- 主 `GameBoardScene` 依然负责组装，但不再直接承载所有细节

### Implementation Strategy

#### Phase 1 - Extract UI Overlays First

优先从低耦合模块开始：

- reconnect overlay
- seat control indicators
- player status tooltip
- pause menu 辅助逻辑

原因：

- 这些模块主要依赖展示层
- 对规则与网络权威侵入相对较低
- 最适合建立第一批稳定边界

#### P2-A Phase 1 Record (2026-07-15)

第一步已选择 pause menu：

- 新增 `GameBoardPauseMenuController`，负责 pause overlay 的懒创建、显示/隐藏、暂停树、焦点、退出确认和本地化刷新。
- `GameBoardScene` 只保留输入门控以及重启、离场、回菜单等业务回调；没有抽动牌局状态或网络协议。
- 通过 `EscInputSmoke` 验证 pause menu 仍按需创建并正确显示；后续再评估 reconnect overlay、seat indicator 和 tooltip 是否值得同样抽取。

#### Phase 2 - Extract Visual Presentation Helpers

从动画表现中选一个边界最清晰的模块先拆：

- table shuffle
- score effects
- question card animations

不要一口气把所有动画都抽出去。

#### Phase 3 - Extract Network Bridge

最后再考虑动：

- `scripts/GameBoardScene.Network.cs`

这一阶段必须严格受 [NETWORK_AUTHORITY_MODEL.md](./NETWORK_AUTHORITY_MODEL.md) 约束。

### Hard Rules During Split

1. 不改变 public 行为
2. 不顺手改命名风格
3. 不顺手重写流程控制
4. 不把“抽类”和“修逻辑”混在同一个提交
5. 每次拆分后必须重新跑构建和关键手测

### Verification

每一小步拆分后都必须执行：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

必须手动验证：

1. 能进入牌局
2. 基础出牌流程正常
3. 对应被拆模块的 UI / 动画 / 联机行为正常

完成标准：

- 模块边界更清晰
- `GameBoardScene` 主类的直接职责下降
- 没有把复杂度从一个文件挪到另一个更难懂的文件

如果拆分触达联机桥接、断线恢复或快照相关代码，必须额外执行：

```bash
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

## 10. Workstream P2-B - Unify Dynamic Environment Layer Strategy

### Goal

在不急着做大抽象的前提下，统一以下脚本的更新、可见性和重绘策略：

- `scripts/UnderwaterAmbientLayer.cs`
- `scripts/MainMenuOceanDynamics.cs`
- `scripts/AdventureMapDynamicLayer.cs`

### Why This Is P2

这三个文件结构相似，但当前最该先做的是：

- 先修最明显的单点问题
- 再观察共性
- 最后决定是否抽共同逻辑

如果现在直接做抽象，容易掉进“为了统一而统一”的陷阱。

### Required Outcome

最终至少要形成一份统一约定：

- 什么时候 `_Process` 直接返回
- 什么时候需要 `QueueRedraw()`
- 什么时候只更新 image instances
- 什么时候允许 resize 触发 rebuild
- 不可见时该不该推进时间

### Recommended Refactoring Rules

1. 先统一策略，再决定是否抽公共基类
2. 如果三者仍有明显差异，允许保留独立实现
3. 只有当相同逻辑复制次数和维护成本都足够高时，才考虑基类或 helper

### Verification

手动验证三条场景链路：

1. 主菜单动态背景
2. adventure map 动态层
3. 游戏内 underwater ambient

完成标准：

- 三者的 redraw / visible / rebuild 策略有统一说明
- 至少没有继续彼此发散

### P2-B Strategy Record (2026-07-15)

三层当前遵循同一运行时约定，保留各自的资源类别和 procedural draw 细节：

- `_Ready()` 和 `VisibilityChanged` 根据 `IsVisibleInTree()` 控制 `SetProcess()`；不可见时不推进 `_time`，也不继续做逐帧 redraw/update。
- `NotificationResized` 允许触发 rebuild；零尺寸只记录 pending rebuild，尺寸有效后再建立 image instances。
- procedural 模式只按 `redraw_fps` 节流 `QueueRedraw()`；image 模式只按 `image_update_fps` 更新 image instances。
- image 模式只有在资源缺失、需要 procedural fallback 时才持续 `QueueRedraw()`；切换或 rebuild 的一次性清理绘制可以保留。
- 目前不引入公共基类：主菜单气泡、adventure map ribbon、underwater profile 的资源加载和 fallback 组合仍有真实差异。

## 11. Suggested Work Breakdown By PR

推荐拆成以下 PR 或提交批次：

1. `PR-1`
   - `GameBoardScene` seat indicator / tooltip 去逐帧刷新

2. `PR-2`
   - `AdventureMapDynamicLayer` redraw 修复

3. `PR-3`
   - `GameBoardScene` 中 `NetworkManager` 访问收敛

4. `PR-4`
   - `NetworkManager` 局部排序 / 分配收敛

5. `PR-5+`
   - `GameBoardScene` 结构拆分，小步多次推进

禁止做法：

- 一个 PR 同时改完上述全部内容
- 一个 PR 同时包含行为修复、架构重写和样式清理

## 12. Manual Regression Checklist

每次执行任一 workstream 后，都至少勾一遍下面这份清单。

### Main Flow

- 能进入主菜单
- 能进入 adventure map
- 能进入普通牌局
- 能正常结束一局

### UI And Visuals

- seat control indicator 显示正常
- tooltip 正常出现、更新、隐藏
- pause menu 正常打开关闭
- 主菜单海洋动态背景正常
- adventure map 动态层正常
- 游戏内 underwater ambient 正常

### Network Related

- 玩家列表正常显示
- 房间列表正常刷新
- 联机开局正常
- reconnect overlay 正常出现/隐藏
- reclaim pending 提示正常

### Gameplay Safety

- 普通出牌正常
- 道具选择正常
- 分数变化正常
- 回合推进正常

## 13. Verification Matrix By Change Type

为了避免“改了 A，却忘了跑 B 类验证”，执行时按下表选择验证动作。

| Change Type | Build | Rules Runner | Multiplayer Smoke Suite | Manual Godot Check |
| --- | --- | --- | --- | --- |
| 仅 `AdventureMapDynamicLayer` / `MainMenuOceanDynamics` / `UnderwaterAmbientLayer` | Required | Recommended | Optional | Required |
| `GameBoardScene` UI overlay 逻辑 | Required | Required | If network UI touched | Required |
| `GameBoardScene` 输入或本地出牌入口 | Required | Required | Recommended | Required |
| `GameBoardScene.Network*` | Required | Required | Required | Required |
| `NetworkManager.cs` | Required | Required | Required | Required |
| 纯注释 / 纯文档 | Optional | Optional | Optional | Optional |

命令口径固定为：

```bash
dotnet build pokegame.sln
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

注意：

- `Rules.Tests` 在本项目里是自定义控制台 runner，不要误用 `dotnet test` 作为主要验收命令
- 若 smoke suite 由于本机环境缺失无法执行，必须在提交说明里明确写出“未执行原因”

## 14. Stop Conditions

执行过程中，遇到以下情况必须暂停，不要继续硬改：

1. 为了修 UI 刷新，不得不修改规则结算顺序
2. 为了减少 `NetworkManager` 查找，不得不改变其生命周期
3. 为了去掉重绘，导致视觉系统需要全面重写
4. 拆分类后，出现大量原本无关文件需要一起修改
5. `dotnet build pokegame.sln` 不再稳定通过

暂停后应做的事：

- 记录当前阻塞点
- 记录触发阻塞的文件
- 将问题升级为单独设计决策，而不是继续在当前整改里硬塞

## 15. Definition Of Done For This Plan

当以下条件全部满足时，可以认为本轮代码健康整改阶段完成：

1. `GameBoardScene` 的明显逐帧 UI 轮询已被移除
2. `AdventureMapDynamicLayer` 的无条件重绘已被移除
3. `GameBoardScene` 对 `NetworkManager` 的访问方式已统一
4. `NetworkManager` 中已收敛至少一批明确的重复排序或短生命周期分配
5. `GameBoardScene` 至少完成一轮真正的职责拆分，而不再只是继续扩写 partial
6. 三个环境动态层脚本已有统一的更新策略说明
7. 构建保持通过
8. 关键手测链路保持通过

## 16. Execution Notes For Future Editors

这份计划在设计上刻意保守。
原因不是“改不动”，而是当前项目已经有以下特点：

- 主场景体量大
- 联机状态复杂
- 展示逻辑与行为逻辑交织
- 自动化覆盖不足以兜住所有回归

因此本文件默认反对：

- 一步到位的大重写
- 没有验证点的“顺手优化”
- 没有边界的“抽象提升”

如果未来有人觉得“某一步可以一次性做更多”，先回答三个问题：

1. 行为验证怎么做？
2. 回归出现时如何快速定位？
3. 如果做一半失败，是否容易回滚？

只要其中一个问题答不清，就继续按本文档的小步执行方式推进。
