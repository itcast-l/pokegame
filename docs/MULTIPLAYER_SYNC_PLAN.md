# 多人对局内状态同步 v1 调整方案

## Summary

- 采用 **Host 权威 + 动作请求 + 表现事件 + 最终快照**。
- 客户端请求不携带 `seatIndex`；Host 通过 `Multiplayer.GetRemoteSenderId()` 映射到真实座位。
- v1 先支持普通手牌出牌、回合推进、分数/桌面/手牌数量同步；道具、弃牌、重连放到后续阶段。

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
  - `NetworkPlayerInputHandler` 只发送 RPC 请求，并在等待 Host 回应时锁定对应手牌 UI。
- 拒绝与回滚：
  - `RejectBoardAction` 释放本地 UI 锁定状态。
  - 若卡牌已进入预备/悬浮/隐藏状态，播放轻量回退动画并恢复原手牌位置。
  - 播放错误提示音，避免玩家误判为网络卡顿。

## Implementation Notes

- 网络扩展集中放在 `NetworkManager` 和 `GameBoardScene.Network`，棋盘交互入口只依赖 `IPlayerInputHandler`，避免到处写 `if (multiplayer)`。
- `GameBoardScene` 保留现有单机/冒险流程；多人模式下只有 Host 执行权威结算。
- 客户端应用 snapshot 时不直接打断当前动画；统一经过 Visual Queue 的“事件完成后对齐”入口。
- `snapshot_id` 单调递增，用于丢弃过期 snapshot 和处理乱序表现事件。

## Test Plan

- 规则测试：
  - 请求不含 `seatIndex`，Host 能从 sender peer 解析真实座位。
  - 玩家伪造他人座位不再可能影响动作归属。
  - 非当前回合、越界手牌、过期 generation 都被拒绝。
  - 快照只包含本客户端自己的具体手牌，其他玩家只有数量。
- Godot 手测：
  - LAN Host + Client 完成加载并进入同一棋盘。
  - 玩家 0 出牌：玩家 1 先看到动画，再看到桌面/分数/回合状态一致。
  - 被拒绝动作会回弹手牌并播放错误提示。
  - 单人和冒险模式出牌行为不变。
- 回归检查：
  - 大厅、准备、加载握手、名字头像同步不受影响。
  - 2/3/4 人座位轮转仍按现有规则执行。

## Assumptions

- v1 使用可靠 RPC，不做帧同步。
- v1 不新增序列化 NuGet 依赖；若后续 payload 变大，再评估 byte[] + MessagePack。
- v1 道具牌仍只在本地/冒险模式保持现状，多人道具同步作为第二阶段。
- Host 是唯一权威状态源，客户端状态最终以 Host snapshot 为准。
