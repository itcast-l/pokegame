# 多人对局内状态同步 v2 调整方案

## Summary

- 采用 **Host 权威 + 客户端立即起手表现 + 表现事件 + 最终快照**。
- 客户端请求不携带 `seatIndex`；Host 通过 `Multiplayer.GetRemoteSenderId()` 映射到真实座位。
- 已覆盖普通手牌、普通道具、目标型道具、弃牌、磁铁附着、回合推进，以及对应的表现事件与最终快照对齐。

## Key Changes

- 协议精简与安全：
  - `RequestPlayHandCard(handIndex, turnSequenceGeneration)`，不信任客户端座位号。
  - Host 维护 `peerId -> seatIndex` 映射，所有动作都按真实 sender 解析。
  - Host 校验：当前回合、手牌索引、回合代数、对局状态；非法动作只回发 `RejectBoardAction(reason, turnSequenceGeneration, rejectedHandIndex)`。
- 事件与快照解耦：
  - 成功出牌后 Host 广播 `CardPlayedEvent(actorSeatIndex, handIndex, playedCardCode, turnSequenceGeneration, snapshotId)`。
  - 客户端将事件放入 Visual Queue，先播放出牌动画，再应用对应 `BoardSnapshot` 对齐底层状态。
  - 如果动画队列积压或 snapshot 更新较新，允许跳过旧动画并直接对齐最新 snapshot。
- 快照传输格式：
  - v1 不引入 MemoryPack/MessagePack，使用 Godot RPC 稳定支持的 `Godot.Collections.Dictionary` / `Array` / `int` / `string` / `bool`。
  - Host 按客户端分别 `RpcId` 发送快照，避免泄露他人手牌。
  - 快照包含：`protocol_version`、`match_id`、`snapshot_id`、`turn_sequence_generation`、`current_turn_player_index`、`winner_player_index`、`scores`、`deck_counts`、`hand_counts`、`item_hand_counts`、`table_cards`、本客户端可见的 `own_hand_cards`。
- 输入层分层：
  - 新增 `IPlayerInputHandler`，由棋盘交互层统一调用。
  - `LocalPlayerInputHandler` 继续直接走现有本地逻辑。
  - `NetworkPlayerInputHandler` 负责发送 RPC 请求，但本地主视角先立即进入起手表现；等待权威确认只用于防重复输入，不再复用“禁用牌”视觉语义。
- 拒绝与回滚：
  - `RejectBoardAction` 释放本地 UI 锁定状态。
  - 若客户端已经开始本地表现，则恢复手牌/道具可见状态并回滚本地预测态。
  - 播放错误提示音，避免玩家误判为网络卡顿。

## Implementation Notes

- 网络扩展集中放在 `NetworkManager` 和 `GameBoardScene.Network`，棋盘交互入口只依赖 `IPlayerInputHandler`，避免到处写 `if (multiplayer)`。
- `GameBoardScene` 保留现有单机/冒险流程；多人模式下只有 Host 执行权威结算，但客户端不再等待 Host 才开始看见自己的动作。
- 客户端应用 snapshot 时不直接打断当前动画；统一经过 Visual Queue 的“事件完成后对齐”入口。
- `snapshot_id` 单调递增，用于丢弃过期 snapshot 和处理乱序表现事件。
- 对于目标型道具，客户端本地可立即推进选择阶段；若下一阶段依赖对方隐藏手牌的真实内容，则仍以 Host 权威揭示和快照内容为准。

## Test Plan

- 规则测试：
  - 请求不含 `seatIndex`，Host 能从 sender peer 解析真实座位。
  - 玩家伪造他人座位不再可能影响动作归属。
  - 非当前回合、越界手牌、过期 generation 都被拒绝。
  - 快照只包含本客户端自己的具体手牌，其他玩家只有数量。
- Godot 手测：
  - LAN Host + Client 完成加载并进入同一棋盘。
  - Host / Client 各自出普通手牌与普通道具时，自己的主视角都应立即进入起手表现。
  - 非 Host 不应再出现“先禁用/打灰/禁止标记，再过一拍出牌”的视觉。
  - 目标型道具第一步、弃牌、磁铁附着在非 Host 侧应立即推进到下一步或立即完成本地反馈。
  - 被拒绝动作会恢复本地预测态并播放错误提示。
  - 单人和冒险模式出牌行为不变。
- 回归检查：
  - 大厅、准备、加载握手、名字头像同步不受影响。
  - 2/3/4 人座位轮转仍按现有规则执行。

## Assumptions

- 继续使用可靠 RPC，不做帧同步。
- 不新增序列化 NuGet 依赖；若后续 payload 变大，再评估 byte[] + MessagePack。
- Host 是唯一权威状态源，客户端状态最终以 Host snapshot 为准。
- Host 是唯一权威状态源，客户端状态最终以 Host snapshot 为准。
