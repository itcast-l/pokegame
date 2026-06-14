# 多人对局主机 / 非主机体验一致性整改方案

## Summary

- 当前多人对局的核心问题不是规则错误，而是**网络等待语义和本地表现语义被混用**。
- 现状下，房主在很多操作上是“本地立即执行 + 权威广播”，非房主则是“先发请求 + 本地锁住等待 + 收到权威事件后再表现”。
- 这会直接导致：
  - 非房主出牌时先出现禁止标记或禁用态，再延迟数秒才飞牌。
  - 目标型道具、弃牌、磁铁附着等多段交互，在非房主视角下会比房主**慢一拍**。
  - 某些取消行为主机可用、非主机不可用。
- 用户期望是正确的：**不管是房主还是非房主，在自己的主视角下，任何可操作环节的体验都必须完全一致。**

本方案的目标是把现有多人输入与表现机制，从“等待主机确认后才表现”调整为“本地立即表现，服务端权威校正”，并系统性清理所有主机 / 非主机体验分叉。

## Background

现有多人同步方案文档 `docs/MULTIPLAYER_SYNC_PLAN.md` 中，明确写过以下设计方向：

- `NetworkPlayerInputHandler` 只发送 RPC 请求，并在等待 Host 回应时锁定对应手牌 UI。
- Host 权威执行完整逻辑后，再广播表现事件，客户端根据事件和最终快照对齐。

相关文档位置：

- `docs/MULTIPLAYER_SYNC_PLAN.md:23-29`

这套思路在“防止重复点击”和“避免客户端乱算状态”上是保守且合理的，但它默认接受了一个代价：

- **非房主的本地体验会比房主晚一步。**

这个代价对于纯状态同步可能可以接受，但对于卡牌出牌、道具触发、目标选择这种强交互玩法，不可接受。

## Goals

- 保证房主和非房主在自己的主视角下，交互节奏完全一致。
- 所有玩家主动发起的动作，都应立即进入本地表现，而不是先进入“等待主机确认”的停滞状态。
- 网络等待状态不能再复用“禁用牌”“禁止标记”“本回合不可出”这类玩法语义。
- 保留 Host 权威结算，不改成客户端权威。
- 为后续重连、AI 接管、拒绝回滚保留清晰边界。

## Non-Goals

- 本文档不要求本次同时完成所有实现。
- 本文档不改变多人房间、断线重连、AI 接管的整体架构。
- 本文档不要求中途加入或观战能力。
- 本文档不把客户端预测扩展到“本地先算完整得分 / 抽牌 / 收牌结果”；优先统一**动作起始体验**和**分步交互体验**。

## Audit Scope

本次排查重点覆盖以下交互：

- 普通手牌出牌
- 普通道具出牌
- 目标型道具的目标选择
- 弃牌选择
- 磁铁附着选择
- 待选择状态下的取消行为
- 键盘 / 手柄选择逻辑中受锁定状态影响的路径

未展开深入的部分：

- 重连过程中的局内恢复动画差异
- AI 接管期间所有特殊道具的局部表现一致性
- Debug 网络延迟注入场景下的额外异常

## Key Finding

### 结论

当前实现里，房主和非房主不是同一套“交互 -> 表现 -> 权威确认”流程，而是两套不同节奏的流程：

- 房主：立即本地执行，再广播给其他人。
- 非房主：先发请求，本地进入锁定等待，等房主执行并回流事件 / 快照后才正式表现。

### 更关键的问题

非房主“等待权威确认”的中间态，被错误复用了“牌被禁用 / 本回合不能用”的视觉语义，造成了两个直接错误：

1. 错误的视觉含义  
   玩家明明已经成功发起动作，却被显示成“这张牌不能出”。

2. 错误的体验节奏  
   本应立即飞牌 / 立即进入下一步选择，却先停顿一段时间。

## Root Cause Pattern

本次发现的问题虽然出现在多个系统里，但根因高度一致，可以归纳为同一个反模式：

### 反模式 A：把“等待主机确认”当成“本地禁用态”

典型表现：

- 非房主发起动作后，先记录某个 `pending` 字段。
- 立刻重绘 UI。
- 渲染层把 `pending` / `locked` 合并进“禁用牌”判断。
- 玩家先看到牌被打灰、打禁止标记或无法继续操作。

### 反模式 B：把“动作开始表现”放在权威结算之后

典型表现：

- Host 先完整执行整段 `Play...Async`。
- 可能再推进后续结算或回合推进。
- 最后才广播视觉事件。
- 非房主必须等 Host 执行完，才开始看到自己的动作被表现。

### 反模式 C：多段交互由 Host 切换阶段，非房主靠快照跟进

典型表现：

- 房主点完一个目标，立刻切到下一步选择。
- 非房主点完目标，只是发请求。
- 必须等 Host 改 `_pendingItemSelectionMode`、发快照，本地才进入下一阶段。
- 于是非房主在多段道具里总是比房主慢一拍。

## Detailed Findings

### P0-1 普通手牌出牌：非房主先被锁牌，再延迟飞牌

#### 相关代码

- `scripts/PlayerInputHandlers.cs:29-46`
- `scripts/GameBoardScene.Network.cs:84-101`
- `scripts/GameBoardScene.Network.cs:128-133`
- `scripts/GameBoardScene.HandRender.cs:276-313`
- `scripts/GameBoardScene.CardRenderHelpers.cs:222-245`
- `scripts/GameBoardScene.Network.cs:306-356`
- `scripts/GameBoardScene.Network.cs:623-649`
- `scripts/GameBoardScene.Network.cs:1006-1023`

#### 当前行为

- 房主点击手牌后，直接走 `ConfirmNetworkHostHandCardPlay(...)`。
- 非房主点击手牌后，走 `RequestNetworkHandCardPlay(...)`。
- 非房主会立刻设置 `_networkPendingHandIndex = handIndex`，随后 `RenderPlayerHands()`。
- 手部渲染把 `isLocked` 合并进 `isJellyfishDisabled || isLocked`。
- 这会触发：
  - 牌半透明
  - `GAME_CARD_DISABLED_THIS_TURN` 提示
  - 红色禁止标记
- Host 在 `PlayNetworkHostHandCardAndBroadcastAsync(...)` 里，先完整执行 `PlayCardFromHandAsync(...)`，必要时继续 `AdvanceTurnAfterPlayedResultAsync(...)`，最后才广播 `BroadcastCardPlayedEventFromHost(...)`。
- 非房主直到收到视觉事件后，才 `HideHandDisplaySlot(...)` 并播放飞牌动画。

#### 问题定性

- 语义错误：等待确认 != 本回合禁用。
- 体验错误：非房主起手动作慢于房主。
- 严重级别：`P0`。

#### 目标行为

- 房主和非房主点牌后，都应立即看到这张牌从手里飞向桌面。
- 非房主本地仍可锁输入，防止重复点，但不能显示禁用标记。
- 权威快照只用于校正，而不是决定动作何时开始表现。

### P0-2 普通道具出牌：同样存在锁牌等待和错误禁用语义

#### 相关代码

- `scripts/GameBoardScene.Network.cs:38-55`
- `scripts/GameBoardScene.Network.cs:136-141`
- `scripts/GameBoardScene.HandRender.cs:316-349`
- `scripts/GameBoardScene.Network.cs:362-406`
- `scripts/GameBoardScene.Network.cs:652-700`

#### 当前行为

- 非房主道具点击后，设置 `_networkPendingItemHandIndex`，然后立刻 `RenderPlayerHands()`。
- 道具卡渲染同样使用 `isJellyfishDisabled || isLocked`。
- 这意味着非房主打道具时，本地先被画成禁用卡，而不是立即进入道具出牌表现。
- Host 仍然先执行完整权威逻辑，然后才广播道具表现事件。

#### 问题定性

- 与普通手牌问题同源。
- 只是用户目前更容易在普通牌上直接观察到。
- 严重级别：`P0`。

#### 目标行为

- 非房主道具点击后也应立即本地飞出或触发对应开启动画。
- 等待主机确认时最多保留“本地已提交、不可重复点击”的弱提示，不得复用禁用牌语义。

### P1-1 目标型道具：非房主多段选择比房主慢一拍

#### 相关代码

- `scripts/GameBoardScene.ItemCardPlays.cs:63-66`
- `scripts/GameBoardScene.ItemCardPlays.cs:137-165`
- `scripts/GameBoardScene.ItemCards.cs:51-63`
- `scripts/GameBoardScene.TargetItemSelection.cs:8-52`
- `scripts/GameBoardScene.TargetItemSelection.cs:80-123`
- `scripts/GameBoardScene.TargetItemSelection.cs:161-181`
- `scripts/GameBoardScene.TargetItemSelection.cs:341-350`
- `scripts/GameBoardScene.Network.cs:58-81`
- `scripts/GameBoardScene.Network.cs:261-285`

#### 当前行为

- 目标型道具会进入 `PendingItemSelectionMode.TargetPlayer`，再进入 `TargetHandCard`。
- 房主点击目标玩家后，本地直接：
  - 改 `_pendingItemSelectionMode`
  - 设置 `_revealedPlayerHandIndex`
  - `RenderPlayerHands()`
  - `ShowTargetHandSelection(...)`
- 非房主点击目标玩家后，只会 `RequestNetworkPendingItemSelection(...)`。
- 非房主要等 Host 改状态并发快照，自己的客户端才能进入下一步目标手牌选择。

#### 问题定性

- 交互分段不一致。
- 房主“连贯”，非房主“点一下，停一下，再继续”。
- 严重级别：`P1`。

#### 目标行为

- 非房主在目标型道具里，每一步确认后都应立即本地进入下一阶段表现。
- Host 仍可保留权威校验，但不能让客户端被动等快照推进交互。

### P1-2 弃牌选择：非房主确认后不立即完成

#### 相关代码

- `scripts/GameBoardScene.ItemCards.cs:160-210`
- `scripts/GameBoardScene.ItemCards.cs:213-270`
- `scripts/GameBoardScene.Network.cs:58-81`
- `scripts/GameBoardScene.Network.cs:227-258`

#### 当前行为

- 人类交互弃牌会进入 `PendingItemSelectionMode.HandLimitDiscard`。
- 房主在本地选择后，直接 `_pendingHandLimitDiscardCompletion.TrySetResult(displayIndex)`。
- 非房主选择后，只会发送 `RequestNetworkPendingItemSelection(-1, displayIndex, ...)`。
- 之后依赖 Host 侧消费该选择并回流同步。

#### 问题定性

- 非房主确认后，本地没有立即完成反馈。
- 与房主存在节奏差。
- 严重级别：`P1`。

#### 目标行为

- 非房主确认弃牌后，本地应立即看到该牌被移除或进入回收表现。
- 权威侧只负责确认是否有效，以及在失败时回滚。

### P1-3 磁铁附着：非房主点选附着目标后等待主机回流

#### 相关代码

- `scripts/GameBoardScene.MagnetAttachments.cs:43-95`
- `scripts/GameBoardScene.MagnetAttachments.cs:103-169`
- `scripts/GameBoardScene.HandInteractions.cs:61-75`
- `scripts/GameBoardScene.TargetItemSelection.cs:45-48`
- `scripts/GameBoardScene.Network.cs:248-258`

#### 当前行为

- 磁铁附着会进入 `PendingItemSelectionMode.MagnetHandCard`。
- 房主点选目标牌后，直接进入 `CompleteMagnetAttachmentSelectionAsync(...)`。
- 非房主点选目标牌后，只发 `RequestNetworkPendingItemSelection(...)`。
- 真正的附着动画和附着结果由 Host 侧推进。

#### 问题定性

- 与目标型道具、弃牌同源。
- 非房主的跟随交互慢一拍。
- 严重级别：`P1`。

#### 目标行为

- 非房主选择附着目标后，应立即本地播放附着动画。
- 最终以 Host 权威状态校正，但不应等 Host 才开始表现。

### P2-1 取消行为不一致：房主可取消，非房主多数情况下不可取消

#### 相关代码

- `scripts/GameBoardScene.Input.cs:118-139`
- `scripts/GameBoardScene.TargetItemSelection.cs:348-350`

#### 当前行为

- 在待选择状态下，如果 `ShouldSendNetworkPendingItemSelectionRequest()` 为真，取消键会直接走 invalid。
- 即：
  - 房主可取消某些待选择状态。
  - 非房主同环节下通常不可取消。

#### 问题定性

- 规则层面对主机 / 非主机不一致。
- 严重级别：`P2`。

#### 目标行为

- 若设计上允许取消，就应对主机 / 非主机统一允许。
- 若设计上不允许取消，就应统一都不允许。
- 不能把“是否房主”当成是否能取消的行为分界。

### P2-2 键盘 / 手柄可选项受锁定状态影响，间接放大主机 / 非主机差异

#### 相关代码

- `scripts/GameBoardScene.Input.cs:169-177`
- `scripts/GameBoardScene.Input.cs:294-310`
- `scripts/GameBoardScene.HandRender.cs:298-313`
- `scripts/GameBoardScene.HandRender.cs:333-349`

#### 当前行为

- 由于非房主的牌和道具会进入 locked 状态，键盘 / 手柄选择可用项也会随之不同。
- 这会进一步让非房主在输入节奏上慢于房主，而不只是视觉层差异。

#### 问题定性

- 输入层差异是表现层差异的连锁结果。
- 严重级别：`P2`。

#### 目标行为

- 键盘 / 手柄选择逻辑应基于“是否已本地进入动作表现阶段”来锁重复输入，而不是基于“看起来像禁用牌”的状态。

## Why The Current Behavior Was Added

从当前实现和旧文档看，这些差异大概率不是疏忽，而是当时有意引入的保守设计，目的主要有三类：

1. 防止非房主重复发请求  
   通过 `_networkPendingHandIndex` / `_networkPendingItemHandIndex` 和 `...InFlight` 标记，避免客户端在网络往返期间连续点击。

2. 保持 Host 权威  
   把真正的结算和阶段推进都放在 Host 上做，客户端只等结果。

3. 降低客户端预测复杂度  
   不需要处理大量本地回滚和状态修正。

这些目标本身没问题，问题在于：

- 它们不应该以“牺牲主视角一致性”为代价。
- 尤其不应该用“禁用牌 / 禁止标记”的语义来表示“我已经点了，正在等服务器”。

## Target UX Principles

后续整改必须遵守以下原则：

### 原则 1：动作起始体验必须一致

- 玩家主动点击 / 确认的瞬间，房主和非房主都要立即进入同样的本地表现。

### 原则 2：等待权威确认不是玩法状态

- “等待主机确认”是网络内部状态，不是玩法语义。
- 它不能显示成“本回合禁用”“不可出”“选择无效”。

### 原则 3：权威校正晚于表现起始

- Host 仍然负责最终权威状态。
- 但 Host 不应决定客户端何时开始看见自己的动作。

### 原则 4：多段交互每一步都要立即推进

- 目标选择、附着选择、弃牌确认等，都必须本地即时进入下一步。

### 原则 5：拒绝路径单独处理，不把正常等待做成失败视觉

- 真正被拒绝时，再做回滚、错误音效、恢复原位。
- 正常网络往返不应长得像失败。

## Proposed Technical Direction

### 1. 拆分三种状态，不再混用

当前混在一起的状态至少应拆成三类：

- `GameplayDisabled`
  - 真正的玩法禁用。
  - 例如 Jellyfish 泡泡禁用、本回合不可用、教程强制限制。
- `PendingAuthority`
  - 动作已发出，等待 Host 权威确认。
  - 不能显示成失败或禁用。
- `LocallyCommitted`
  - 本地已经进入表现阶段，防止重复输入。
  - 视觉上应是“已经在执行”，而不是“不可用”。

### 2. 本地立即播放动作起始表现

对所有玩家主动触发的动作：

- 普通牌出牌
- 道具出牌
- 目标型道具第一步 / 第二步
- 弃牌确认
- 磁铁附着选择

都应在本地立即表现，不等待 Host 完整结算后再开始。

### 3. Host 只做权威校验和最终校正

建议保留 Host 权威，但把职责调整为：

- 校验动作是否合法。
- 推进真实游戏状态。
- 广播最终权威事件 / 快照。
- 在客户端预测与权威结果不一致时触发校正或回滚。

### 4. 拒绝时做显式回滚

对于被拒绝的动作：

- 恢复手牌 / 道具到原位。
- 恢复可交互状态。
- 播放错误提示。
- 若有需要，做轻量回退动画。

这样可以把“少数错误路径的回滚复杂度”换成“绝大多数正常路径的一致体验”。

### 5. 多段交互改为客户端本地阶段推进

不要再依赖：

- Host 切换 `_pendingItemSelectionMode`
- 发快照
- 客户端收到快照后才知道该进入下一步

而应改成：

- 客户端本地先推进到下一阶段 UI / 动画
- 同步把选择请求发给 Host
- Host 若确认，快照只对齐状态
- Host 若拒绝，再退回上一步或整体取消

## Proposed Implementation Plan

### Phase 1：修复最明显的普通牌 / 普通道具差异

#### 目标

- 非房主点击普通牌或普通道具时，立即进入本地飞牌 / 触发表现。
- 删除“等待权威确认 = 禁用牌”的错误视觉。

#### 具体调整

- 新增独立的本地待确认表现状态，不再复用 `isJellyfishDisabled`。
- `IsNetworkHandCardLocked()` / `IsNetworkItemCardLocked()` 不再驱动红色禁止标记。
- 非房主点击出牌后：
  - 立即本地隐藏或抽离对应手牌槽位
  - 立即播放出牌动画
  - 同步发送 RPC 请求
- Host 拒绝时，再回滚。

#### 风险

- 若本地已经播了飞牌，权威拒绝时要做回退动画。
- 但只要拒绝属于少数路径，整体体验收益远大于复杂度成本。

### Phase 2：统一目标型道具的分步节奏

#### 目标

- 房主和非房主在目标玩家选择、目标手牌选择上的阶段切换完全一致。

#### 具体调整

- 客户端点选目标玩家后，本地立即进入目标手牌选择。
- 客户端点选目标手牌后，本地立即进入后续动画或结算前表现。
- Host 拒绝时，再整体回滚或退回上一步。

### Phase 3：统一弃牌与磁铁附着跟随交互

#### 目标

- 非房主在弃牌和磁铁附着确认后，本地不再卡住等待。

#### 具体调整

- 弃牌确认后，本地立即移除或飞走该牌。
- 磁铁附着选择后，本地立即播放附着动画。
- Host 拒绝时，再执行回滚。

### Phase 4：统一取消规则

#### 目标

- 取消行为不再按“是否房主”分叉。

#### 具体调整

- 为各类待选择状态定义明确规则：
  - 可取消：主机 / 非主机都可取消。
  - 不可取消：主机 / 非主机都不可取消。
- 删除“只有非主机走 invalid”的特殊分支。

### Phase 5：清理旧设计和文档

#### 目标

- 避免未来继续沿用“等待主机确认就锁住 UI”这一旧思路。

#### 具体调整

- 更新 `docs/MULTIPLAYER_SYNC_PLAN.md` 中关于 “NetworkPlayerInputHandler 等待 Host 回应时锁定对应手牌 UI” 的描述。
- 将本文件作为新的体验一致性约束文档。

## Validation Plan

### 功能验证

#### 普通牌出牌

- 2 人局，房主出牌：
  - 点击瞬间立即飞牌
  - 无禁用标记
- 2 人局，非房主出牌：
  - 点击瞬间也立即飞牌
  - 与房主体感一致
  - 无禁用标记

#### 普通道具出牌

- 房主、非房主都应立即进入道具出牌表现。
- 不允许先出现“禁用态”。

#### 目标型道具

- 房主、非房主选择目标玩家后，都应立即进入下一步目标手牌选择。
- 房主、非房主选择目标手牌后，都应立即进入后续表现。

#### 弃牌

- 房主、非房主确认弃牌后，都应立即看到牌被移除或进入回收表现。

#### 磁铁附着

- 房主、非房主选择附着目标后，都应立即播放附着表现。

#### 取消

- 对同一种待选择状态，房主和非房主取消行为必须一致。

### 稳定性验证

- 人为加入网络延迟后，非房主依旧不能出现“先禁止、后动作”的视觉。
- 被拒绝动作必须能正确回滚，不残留隐藏槽位或 pending 状态。
- 快照应用后，不能把已经完成的本地表现硬性闪回。

### 回归验证

- Jellyfish 真正的禁用牌视觉仍然保留。
- 教程限制牌视觉仍然保留。
- AI 接管与重连流程不被本次改动破坏。
- 键盘 / 手柄选择逻辑不因新增状态失效。

## Open Questions

### Q1：客户端预测做到哪一层

推荐最小范围：

- 只预测动作起始表现和分步交互推进。
- 不在客户端本地完整预测最终得分、抽牌、收牌结果。

这样可以先解决最痛的体验问题，同时控制回滚复杂度。

### Q2：权威拒绝时退回上一步还是整体取消

建议：

- 普通牌 / 普通道具：整体回滚到动作发起前。
- 目标型道具第二步失败：优先退回到上一步选择，而不是整个流程全部取消。

### Q3：是否保留“等待提交”的弱提示

可以保留，但必须满足：

- 不像禁用牌
- 不像失败
- 不阻断动作已经开始的表现

例如：

- 很轻的蓝色描边
- 很短的“已提交”粒子效果
- 动作期间整体不可重复点，但不显示红色禁止标记

## Recommended Priority

建议实施顺序：

1. `P0` 普通手牌出牌
2. `P0` 普通道具出牌
3. `P1` 目标型道具分步选择
4. `P1` 弃牌和磁铁附着
5. `P2` 取消行为和输入细节统一

原因：

- 前两项直接影响最常见、最频繁的核心操作。
- 后三项虽然不如普通出牌高频，但它们共享同样的设计问题，后续应统一收口。

## Final Recommendation

本项目接下来在多人交互上，不应再接受“房主即时、非房主等待”的本地主视角差异。

更准确地说：

- **Host 权威** 可以保留。
- **客户端等待权威确认后才开始表现** 这一交互设计，不应保留。

后续整改的总原则应固定为：

- 玩家主动发起的动作，主机和非主机都立即本地表现。
- 网络只负责确认、校正、拒绝回滚。
- “等待主机确认”永远不能再表现成“这张牌被禁用 / 这次操作无效”。

只要这个原则落地，当前这次暴露出来的普通出牌问题，以及同源的道具、目标选择、弃牌、附着等问题，都会有一套统一且可维护的解法。
