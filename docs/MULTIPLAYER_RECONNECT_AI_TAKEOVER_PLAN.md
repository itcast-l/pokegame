# 多人断线 AI 接管与下一回合交还方案

## Summary

- 继续采用 **Host 权威** 的多人对局架构。
- 非 Host 玩家断线后，**不暂停整局**，由 Host 立刻让该座位进入 **AI 接管**。
- 断线玩家只要该局 **尚未结束**，都允许重连并认领自己的座位。
- 重连成功后，玩家不会立刻打断 AI 当前控制，而是进入 **待交还** 状态，并在 **该 seat 的下一回合开始时** 恢复人类控制。
- Host 断线仍然直接结束本局；本方案 **不实现 host migration**。

## Goals

- 保证其他在线玩家的对局不中断。
- 允许断线玩家在本局结束前重新接管自己的座位。
- 避免在一个回合中途切换控制者，减少状态机复杂度和表现错乱。
- 保持玩家的名字、头像、分数、座位不变，只切换“决策来源”。

## Non-Goals

- 不支持 Host 迁移。
- 不支持中途加入。
- 不支持观战。
- 不回滚 AI 在断线期间已经执行的操作。
- 不把断线玩家“替换成冒险敌人角色”；只复用最弱冒险敌人的出牌决策风格。

## Current Relevant Files

- `scripts/NetworkManager.cs`
- `scripts/GameBoardScene.Network.cs`
- `scripts/GameBoardScene.TurnAi.cs`
- `scripts/GameBoardScene.ItemCards.cs`
- `scripts/GameBoardScene.HandInteractions.cs`
- `scripts/EnemyAiRules.cs`
- `scripts/NetworkSessionModels.cs`

## Core Behavior

### 1. 断线后立即 AI 接手

- 当非 Host 玩家在 `InGame` 状态下断线时，Host 不暂停整局。
- 该玩家座位立刻从 `HumanConnected` 切换到 `AiProxyReclaimable`。
- 若当前正轮到该 seat 行动，Host 立即按 AI 逻辑继续该回合。
- 若当前正在等待该 seat 完成目标选择、弃牌、道具后续选择，Host 也立即由 AI 补完该选择，避免卡局。

### 2. 重连窗口

- 只要该局尚未结束，原玩家都可以重新连接并认领自己的座位。
- 在玩家重新认领前，该 seat 一直由 AI 代理控制。
- 若本局已经结算结束，则不再允许认领。

### 3. 重连成功后的交还策略

- 重连成功后，该 seat 从 `AiProxyReclaimable` 切到 `HumanReclaimPending`。
- `HumanReclaimPending` 表示：
  - 网络连接已恢复。
  - 玩家能收到完整局面同步。
  - 但当前 seat 的控制权仍由 AI 保持到该 seat 的 **下一回合开始**。
- 当 `current_turn_player_index` 即将切到该 seat 时，若状态为 `HumanReclaimPending`，则在该回合开始前切回 `HumanConnected`。

### 4. 待交还状态下的二次断线

- 若玩家处于 `HumanReclaimPending` 时再次断线，不允许维持一个失效的待交还状态。
- Host 应立刻把该 seat 从 `HumanReclaimPending` 回退到 `AiProxyReclaimable`。
- 回退后：
  - AI 继续保持该 seat 的控制权。
  - 原 `ReconnectToken` 继续有效，或按实现需要重新发放新 token，但同一时刻只能存在一个有效 token。
  - 该玩家只要本局未结束，仍可再次认领。
- 这样可以避免该 seat 在轮到它时仍被视为“即将交还给一个实际上已离线的 peer”，从而导致输入或回合状态卡死。

### 5. Host 断线

- Host 断线直接结束本局。
- 本方案不尝试把 Host 切为 AI，也不做主机迁移。

## Seat Control State Model

建议新增独立的座位控制状态，而不是把这些字段塞进现有 `NetworkPlayerPublicState`。

### Enum

```csharp
public enum SeatControlMode
{
	HumanConnected,
	AiProxyReclaimable,
	HumanReclaimPending,
}
```

### State Record

```csharp
public readonly record struct SeatControlState(
	int SeatIndex,
	SeatControlMode Mode,
	int PeerId,
	string StableUserId,
	string ReconnectToken);
```

### Notes

- `StableUserId` 继续代表该座位原始玩家身份。
- `ReconnectToken` 是本局唯一、按 seat 分配的重连凭据。
- `PeerId` 在断线后可置为 `0` 或 `-1`。
- `HumanReclaimPending` 需要单独存在，避免“已重连但立即打断 AI 当前回合”。

## Network Flow

### Match Start

- Host 为每个入局玩家生成 `ReconnectToken`。
- Token 只通过私有 RPC 发给对应客户端。
- Token 生命周期只覆盖当前 `match_id`。

### Disconnect Flow

1. `NetworkManager.OnPeerDisconnected()` 发现非 Host 玩家在 `InGame` 断线。
2. 若当前状态为 `HumanConnected` 或 `HumanReclaimPending`，Host 都把该 seat 的控制状态改为 `AiProxyReclaimable`。
3. 广播 seat control 更新。
4. 若当前轮到该 seat，或者有待该 seat 完成的选择，立即由 Host 触发 AI 处理。

### Reconnect Flow

1. 客户端重新连上 Host。
2. 客户端发送：

```text
RpcReconnectPlayer(matchId, reconnectToken, stableUserId)
```

3. Host 校验：
   - `matchId` 一致
   - `reconnectToken` 匹配
   - `stableUserId` 匹配
   - 当前 seat 状态为 `AiProxyReclaimable`
4. 校验通过后：
   - seat 状态改为 `HumanReclaimPending`
   - 绑定新的 `peerId`
   - 发完整私有局面快照
   - 广播 seat control 更新
5. 客户端进入“已重连，等待下回合恢复控制”状态。

### Reconnect Idempotency / Anti-Replay

- `RpcReconnectPlayer(matchId, reconnectToken, stableUserId)` 必须按 **幂等操作** 设计。
- 原因：
  - 网络抖动时，客户端可能在极短时间内重复发送相同重连请求。
  - 若 Host 把每次请求都当成新的认领，会导致重复绑定 `peerId`、重复下发全量快照、重复推进 seat control 状态。
- 建议规则：
  1. 当 token 首次校验成功并把 seat 从 `AiProxyReclaimable` 切到 `HumanReclaimPending` 后，Host 记录一次“已认领”结果。
  2. 后续携带 **相同 `matchId` + `reconnectToken` + `stableUserId`** 的重复请求：
     - 若来自同一个已绑定 `peerId`，直接返回成功或静默忽略。
     - 若来自新的 `peerId`，需要先明确是否允许替换当前已认领连接；若不允许，就返回已认领状态而不是重新执行整套认领流程。
  3. 对于已完成认领的 token，Host 不应再次：
     - 重新切换 seat control 状态
     - 再次广播相同 seat control 更新
     - 再次触发完整快照下发
  4. 一旦 token 已被新一轮有效认领消费，旧连接上的重复请求必须不能造成状态回退或二次绑定。
- 这样既能处理正常的重复提交，也能降低简单重放攻击导致的状态震荡风险。

### Recommended Host Tracking

- 建议为每个 seat 或每个 token 维护一份最小认领记录：
  - `matchId`
  - `seatIndex`
  - `reconnectToken`
  - `stableUserId`
  - `claimedPeerId`
  - `claimState`（未认领 / 已认领 / 已失效）
- `RpcReconnectPlayer` 应先查这份记录，再决定：
  - 接受为首次认领
  - 作为幂等重复请求直接成功
  - 作为无效/过期/冲突请求拒绝

### Match End Boundary

1. 只要本局尚未结束，`AiProxyReclaimable` seat 就允许认领。
2. 当本局结算结束后，认领入口自动失效。
3. 不需要单独的“超时转永久 AI”状态。

### Reconnect Retry Rule

1. `HumanReclaimPending` 不是稳定终态，而是“已重连但未到交还时机”的临时状态。
2. 若该状态对应的 `peerId` 再次断线，必须回退到 `AiProxyReclaimable`。
3. 回退后，后续再次重连的处理流程与第一次认领一致。

## Turn Handoff Rule

这是本方案最关键的约束：

- **重连成功不立即交还控制权。**
- 若 seat 当前仍由 AI 在执行回合，AI 把该回合完整打完。
- 只有当该 seat 的 **下一回合开始** 时，才把状态从 `HumanReclaimPending` 切回 `HumanConnected`。

### Recommended Hook Point

建议在回合推进逻辑里，在“确定新的 `current_turn_player_index` 之后、允许该 seat 发起任何玩家决策之前”执行：

```text
TryPromoteReclaimedSeatAtTurnStart(seatIndex)
```

### Why This Boundary

- 不会打断当前动画、出牌、结算。
- 不会出现一个回合前半段 AI 打、后半段人接手的混乱状态。
- 不需要额外设计“中途抢回待选择状态”的复杂逻辑。

## AI Takeover Rules

### Decision Source

- AI 接管只替换“该 seat 的行动决策来源”。
- 不改变：
  - 玩家名字
  - 玩家头像
  - 玩家得分
  - 玩家座位
  - 非冒险角色表现

### AI Baseline

- 普通手牌出牌优先复用 `EnemyAiRules.GetAdventureProfile(1)`。
- 这是当前最弱冒险敌人的决策画像，但只用于“怎么选牌”。
- 不启用任何冒险敌人专属能力、Boss 逻辑、冒险特效。

### AI Scope

AI 接管不仅要会打普通手牌，还必须能在下列场景补全合法决策：

- 普通手牌出牌
- 道具牌出牌
- 目标玩家选择
- 目标手牌选择
- 手牌上限弃牌
- 其他会把多人流程卡住的待确认步骤

### Suggested Strategy

- 普通手牌：
  - 优先复用现有 `TryPickCaptureCardIndex()` / `TryPickBestCaptureHandIndex()` 路径
  - 无法捕获时走现有随机合法动作兜底
- 道具牌：
  - 优先做“最小可用”启发式
  - 只要求行为合法、能推进对局，不追求高智能
- 待选目标：
  - 默认优先对领先对手或当前最优目标生效
  - 如果没有明显收益，选择第一个合法目标

### Last-Resort Safety Fallback

- 由于多人断线接管发生在 Host 权威流程里，**AI 决策绝不能把整局对局卡死**。
- 因此，对于下列 Host 侧 AI 决策入口：
  - 道具牌目标选择
  - 目标手牌选择
  - 手牌上限弃牌
  - 磁铁或其他多阶段道具的后续选择
  - 任何“断线玩家待确认步骤”的 AI 补完逻辑
- 都应在最外层增加一层“永不中断”兜底：
  1. 先尝试正常启发式决策。
  2. 若启发式内部因极端局面、空列表、状态竞争、资源缺失、索引错误等原因抛出异常：
     - 记录错误日志，便于后续修复。
     - 立即降级为 **随机选择一个合法目标** 或 **随机弃掉一张合法牌**。
  3. 若不存在任何合法选择，则必须显式走“无合法动作”的权威分支，而不是把状态挂起等待。
- 这条规则的优先级高于“AI 选得聪明”：
  - 第一目标：对局继续推进
  - 第二目标：选择合法
  - 第三目标：选择合理
- 换句话说，Host 侧 AI 的最低标准不是“像人”，而是“永远不阻塞局面状态机”。

## Snapshot Requirements

由于重连后不回滚，只恢复当前权威局面，因此快照必须足够完整。

### Current Gap

当前多人快照只足以恢复普通手牌和若干公开状态，不足以支撑“断线后继续打”。

### Required Additions

- `own_hand_cards`
- `own_item_hand_cards`
- `hand_counts`
- `item_hand_counts`
- `table_cards`
- `scores`
- `deck_counts`
- `turn_sequence_generation`
- `current_turn_player_index`
- `winner_player_index`
- `pending_selection_mode`
- `pending_selection_owner`
- `pending_item_card_id`
- `pending_turn_sequence_generation`
- `seat_control_states`

### Snapshot Semantics

- 快照总是代表 **当前 Host 权威状态**。
- 若 AI 在断线期间已经替该 seat 打出两步，重连玩家看到的就是这两步之后的局面。
- 不做追帧回放，不做状态回滚。

### Snapshot Catch-up Transition

- 重连客户端收到完整私有快照时，Host 端可能正处于以下任一阶段：
  - AI 刚刚打出一张牌
  - 某个表现事件还在 Visual Queue 中
  - 某个技能、道具或结算动画仍在播放
- 因此，**重连后的全量快照不能直接裸应用到可见 UI 上**，否则容易出现：
  - 牌面瞬间闪跳
  - 角色或桌面状态突然跳帧
  - 玩家错过关键表现却又看到结果已经落地
  - 客户端内部状态和可见动画短时间不一致
- 建议增加一个专用的 **Snapshot Catch-up** 过渡态：
  1. 重连认领成功后，客户端先进入遮罩或黑屏提示态。
  2. 遮罩显示文案，例如：`正在同步最新战局…`
  3. 在遮罩下应用完整私有快照，并清理或重建本地网络表现队列。
  4. 确认客户端内部状态机、手牌、道具牌、桌面、回合状态、待选状态都与 Host 快照对齐。
  5. 若仍有必须补播的本地最小过渡表现，只在遮罩下完成，不向玩家暴露底层强切。
  6. 对齐完成后，再撤掉遮罩并进入正常观看状态。
- 这个过渡态的目标不是补播断线期间的所有动画，而是**隐藏强制状态同步带来的视觉断层**。
- 对于重连玩家，优先级应是：
  - 状态正确
  - 恢复后画面稳定
  - 明确知道“当前已同步到最新战局”
  - 不要求补完所有错过的历史表现

## UI / UX Notes

### Player List / Board Indicators

- `AiProxyReclaimable`
  - 显示：`断线中 · AI 接管`
- `HumanReclaimPending`
  - 显示：`已重连 · 下回合恢复`

### Reconnected Player Feedback

- 重连成功后明确提示：
  - `你已重连，当前回合结束后将在你的下一个回合恢复控制。`
- 若客户端正在执行快照追赶，还应显示：
  - `正在同步最新战局…`

## Code Changes

### `scripts/NetworkSessionModels.cs`

- 新增 `SeatControlMode`
- 新增 `SeatControlState`

### `scripts/NetworkManager.cs`

- 新增 seat control 状态容器
- 在开局时生成并分发 `ReconnectToken`
- 扩展 `OnPeerDisconnected()` 为“立即 AI 接管”
- 新增 `RpcReconnectPlayer(...)`
- 为 `RpcReconnectPlayer(...)` 增加幂等处理与最小 anti-replay 记录
- 新增私有 seat control 广播 / 拒绝 / 接受逻辑
- 明确定义 `HumanReclaimPending` 再次断线时回退到 `AiProxyReclaimable`
- 认领入口只在本局结束时关闭

### `scripts/GameBoardScene.Network.cs`

- 接收并应用 `seat_control_states`
- 支持重连后完整私有快照恢复
- 增加 `Snapshot Catch-up` 过渡态，遮罩下完成快照对齐与本地事件队列重建
- 根据 seat control 状态锁定或放开本地输入

### `scripts/GameBoardScene.cs` / 相关 UI 场景

- 增加一个轻量遮罩层或黑屏同步提示
- 在重连后的快照追赶阶段覆盖底层棋盘，避免 UI 强切暴露给玩家

### `scripts/GameBoardScene.TurnAi.cs`

- 为“断线代理 AI”补独立入口
- 普通回合动作、待选目标、弃牌等都能走 AI 补全
- 在 Host 侧 AI 决策入口增加最外层异常兜底，失败时退化到随机合法动作

### `scripts/GameBoardScene.ItemCards.cs`

- 把待选目标、弃牌、磁铁等中间步骤抽成可由 Host 直接驱动的 AI 选择接口
- 为目标选择与弃牌接口提供“随机合法选择”后备路径

### `scripts/GameBoardScene.HandInteractions.cs`

- 本地输入在 `HumanReclaimPending` 时保持禁用
- 只有 seat 真正回到 `HumanConnected` 才恢复可操作

## Implementation Phases

## Phase 1: Seat Control Foundation

目标：

- 加入 seat control 状态
- 断线即切 AI
- 本局内可认领逻辑

验收：

- 非 Host 玩家断线后，对局继续，不暂停。
- 该 seat 显示为 AI 接管。

## Phase 2: Reconnect Claim + Next-Turn Handoff

目标：

- 实现 `RpcReconnectPlayer`
- 只要本局未结束都可重连
- 重连后标记 `HumanReclaimPending`
- 同 token 重复请求不会重复推进状态或重复发完整快照
- 到该 seat 下一回合开始时交还控制

验收：

- 玩家断线后只要本局未结束，仍能回到当前对局。
- 当前由 AI 继续打完，玩家在自己下个回合恢复控制。
- 网络抖动导致的重复重连请求不会造成重复认领或状态震荡。

## Phase 3: Full Snapshot Recovery

目标：

- 补足私有快照字段
- 增加重连后的 `Snapshot Catch-up` 过渡态
- 支持重连后恢复普通手牌、道具牌、待选状态

验收：

- 玩家在道具牌局、待选目标中断线后，重连仍能看到正确当前局面。
- 重连时即使 Host 正在播放动作或结算表现，客户端也不会出现明显闪跳或半同步画面。

## Phase 4: AI Completeness for Pending Choices

目标：

- AI 不仅会普通出牌，也会补完多人中的目标选择和弃牌
- 即使启发式异常，也能退化到随机合法选择而不阻塞对局

验收：

- 断线发生在目标选择、手牌上限弃牌、道具后续步骤中时，对局不会卡死。
- 人为制造启发式异常时，Host 仍能回退到随机合法选择并继续推进。

## Test Plan

### Rules / Unit Coverage

- seat control 状态迁移正确：
  - `HumanConnected -> AiProxyReclaimable`
  - `AiProxyReclaimable -> HumanReclaimPending`
  - `HumanReclaimPending -> AiProxyReclaimable`
  - `HumanReclaimPending -> HumanConnected`
- 非法 token / 错误 `stableUserId` / 错误 `matchId` 被拒绝
- 重连成功后不会在中途立即交还控制
- 结算后认领请求被拒绝
- `HumanReclaimPending` 下再次断线后，不会导致 seat 在下一回合开始时卡死
- 快照追赶结束前，本地输入和底层动画展示都保持受控
- 同一 token 的重复 `RpcReconnectPlayer` 请求具备幂等性
- 已认领 token 的重复请求不会重复绑定 `peerId`、重复广播 seat control、重复发送全量快照
- Host 侧 AI 目标选择/弃牌启发式抛异常时，会退化到随机合法选择，而不是阻塞状态机

### Godot Manual Tests

- 2 人 LAN：
  - 玩家 2 在自己回合前断线，AI 正常接手，稍后重连，下回合恢复
- 2 人 LAN：
  - 玩家 2 在自己回合中断线，AI 打完该回合，重连后下回合恢复
- 2 人 LAN：
  - 玩家 2 已重连并进入 `HumanReclaimPending` 后再次断线，seat 回退为 `AiProxyReclaimable`，AI 继续接手，后续仍可再次重连
- 2 人 LAN：
  - 玩家 2 长时间未重连，AI 持续接手直到本局结束
- 3/4 人 LAN：
  - 非当前回合玩家断线，其他玩家流程不被暂停
- `itemMode=true`：
  - 断线发生在目标选择 / 弃牌时，对局仍继续
- `itemMode=true`：
  - 人为注入 AI 目标选择异常后，Host 仍能完成随机合法目标/随机合法弃牌，并继续推进
- 重连发生在 Host 正播放出牌动画、技能动画或结算动画时：
  - 客户端先显示同步遮罩
  - 画面不出现明显闪跳
  - 遮罩移除后局面与 Host 一致
- Host 断线：
  - 本局结束

### Current Verification Assets

- 自动 rules / unit：
  - `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
    - 已覆盖“重连成功后忽略旧连接迟到的 `ServerDisconnected` 事件”规则，防止客户端在 `CLIENT_RECONNECTED` 后被再次打回 `RECONNECTING`
- 自动 smoke 总入口：
  - `bash tests/run_multiplayer_reconnect_smoke_suite.sh`
- 已自动覆盖的 LAN / 本地运行时场景：
  - `tests/run_lan_reconnect_smoke.sh`
    - 2 人 LAN：玩家 2 在自己回合前断线，AI 接手，稍后重连，下回合恢复
  - `tests/run_lan_own_turn_reconnect_smoke.sh`
    - 2 人 LAN：玩家 2 在自己回合中断线，AI 打完该回合，重连后下回合恢复
  - `tests/run_lan_reclaim_pending_smoke.sh`
    - 2 人 LAN：玩家 2 已重连并进入 `HumanReclaimPending` 后再次断线，seat 回退为 `AiProxyReclaimable`
  - `tests/run_lan_no_reconnect_until_match_end_smoke.sh`
    - 2 人 LAN：玩家 2 长时间未重连，AI 持续接手直到本局结束
  - `tests/run_lan_three_player_disconnect_smoke.sh`
    - 3 人 LAN：非当前回合玩家断线，其他玩家流程不暂停
  - `tests/run_lan_pending_discard_smoke.sh`
    - `itemMode=true`：断线发生在手牌上限弃牌时，对局仍继续
  - `tests/run_lan_pending_target_smoke.sh`
    - `itemMode=true`：断线发生在目标选择 / 目标手牌选择时，对局仍继续
  - `tests/run_lan_pending_discard_fallback_smoke.sh`
    - `itemMode=true`：人为注入 AI 弃牌启发式异常后，Host 退化到随机合法弃牌并继续推进
  - `tests/run_lan_pending_target_fallback_smoke.sh`
    - `itemMode=true`：人为注入 AI 目标选择启发式异常后，Host 退化到随机合法目标并继续推进
  - `tests/run_lan_reconnect_during_visual_smoke.sh`
    - 重连发生在 Host 正播放动作表现时，自动验证遮罩、catch-up pending、visual queue 的内部状态链路
  - `tests/run_lan_host_ended_match_smoke.sh`
    - Host 断线：本局结束
- 已自动补齐的回归检查：
  - `tests/run_game_board_mode_regression_smoke.sh`
    - 单机模式不受影响
    - 冒险模式不受影响
  - `tests/run_lan_basic_turn_sync_smoke.sh`
    - 现有多人普通出牌同步不回退
  - `tests/GodotSceneSmoke.gd`
    - 大厅、准备入口、基础场景加载不受影响
- 仍需真实环境 / 人工观察的验证入口：
  - `tests/STEAM_RECONNECT_MANUAL_CHECKS.md`
    - Steam 传输下的 reconnect / reclaim runtime path 手工验证
  - `tests/VISUAL_RECONNECT_MANUAL_CHECKS.md`
    - “画面不出现明显闪跳”的人工视觉验收
    - `tests/run_lan_reconnect_during_visual_manual.sh` 在设置 `CODEX_VISUAL_CAPTURE_DIR` 时会额外落盘 `client_overlay.png` / `client_recovered.png`，可作为人工复核材料

### Regression Checks

- 大厅、准备、开始对局不受影响
- 单机模式不受影响
- 冒险模式不受影响
- 现有多人普通出牌同步不回退

## Open Risks

- Steam 传输下的重连体验可能和 LAN 不完全一致，需要在真实 Steam 环境下通过 `tests/STEAM_RECONNECT_MANUAL_CHECKS.md` 单独验证 `peerId` 重建路径。
- 若断线玩家多次重复连接，需要防止重复认领和脏 peer 绑定。
- 若 AI 代理期间涉及复杂道具链，必须保证“合法推进”优先于“高质量决策”。
- “画面不出现明显闪跳” 仍属于人工视觉质量结论；当前自动 smoke 只验证内部状态链路。虽然 manual harness 已能产出 `client_overlay.png` / `client_recovered.png`，但当前捕获结果还不能单独证明遮罩退出阶段不存在可见跳变，仍需要结合 `tests/VISUAL_RECONNECT_MANUAL_CHECKS.md` 做最终观察确认。

## Recommendation

先不要把“多人道具全量智能决策”和“重连交还机制”混成一个大改动。

建议实际开发顺序是：

1. 先做 seat control 状态和断线即 AI 接管
2. 再做“本局内可重连认领”与下回合交还
3. 再补全私有快照
4. 最后补 AI 对多人道具后续选择的覆盖

这样每一步都可单独验证，不会在一个大分支里同时调网络层、局面快照、回合状态机和 AI 行为。
