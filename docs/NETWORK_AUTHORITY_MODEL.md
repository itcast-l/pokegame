# Network Authority Model

Last updated: 2026-06-17

本文件冻结当前联机实现的权责边界。后续联机改动应以这里的规则为准，而不是靠读散落代码临时判断。

## 1. Host Authority

Host 是以下状态的唯一权威来源：

- `match_id`
- `snapshot_id`
- `turn_sequence_generation`
- 当前回合归属
- 胜负状态
- 分数
- 牌堆数量
- 手牌与道具手牌的真实内容
- 桌面牌状态
- `pending item selection`
- `seat control states`

实现锚点：

- `GameBoardScene.BuildNetworkBoardSnapshotForSeat`
- `NetworkManager.SendBoardSnapshotsFromHost`

## 2. Client Prediction Boundary

客户端允许做有限的本地预测，但预测永远不能替代 Host 结算。

允许的预测行为：

- 本地出牌的临时视觉表现
- 本地道具牌播放的临时视觉表现
- 本地 pending selection 的 UI 推进
- reconnect overlay / 恢复提示这类纯展示层状态

不允许客户端自行决定：

- 规则结算结果
- 桌面最终结构
- 真实手牌内容
- pending selection 的权威完成
- seat control 状态迁移

预测一旦被拒绝或被快照覆盖，必须回滚。

实现锚点：

- `GameBoardScene.Network.cs`
- `RollbackPredictedNetworkHandCardPlay`
- `RollbackPredictedNetworkItemCardPlay`
- `RejectNetworkBoardAction`

## 3. Snapshot Privacy Rules

每个接收 seat 收到的快照内容并不完全相同。

固定规则：

- 本地 seat 收到自己的真实 `own_hand_cards`
- 本地 seat 收到自己的真实 `own_item_hand_cards`
- 其他玩家手牌默认只收到数量，不收到真实内容
- 只有当目标手牌处于合法的 pending reveal 场景时，才会向发起选择的 seat 暴露真实目标手牌
- shared snapshot 中不传播 `reconnect_token`

实现锚点：

- `BuildNetworkBoardSnapshotForSeat`
- `ShouldRevealTargetHandToSeat`
- `SeatControlRules.RedactReconnectTokenForSharedSnapshot`

## 4. Snapshot Send Timing

Host 必须在以下时机发送快照：

- 权威出牌或道具结算完成后
- pending selection 的权威阶段推进后
- reconnect claim 被接受且需要把当前局面发给客户端时
- 任何会改变桌面、手牌、牌堆、回合、seat control、胜负状态的权威变更后

当前实现里，视觉事件通常与 `snapshot_id` 成对出现，随后再由 Host 发送对应快照。

## 5. Snapshot Apply Timing

普通 in-game 客户端：

- 若还有同 `snapshot_id` 的视觉事件队列未消费，不应立刻覆盖
- 若视觉队列仍在处理，较新的快照可以暂存，待队列结束后再应用

reconnect 客户端：

- `NetworkSessionState.Reconnecting` 时应立即应用快照
- reconnect catch-up 会先清空旧视觉队列，再用快照重建当前权威棋盘
- 只有在视觉队列完成后，才能通知 `NotifyReconnectSnapshotApplied`

实现锚点：

- `ApplyNetworkBoardSnapshot`
- `ApplyNetworkBoardSnapshotNow`
- `ProcessNetworkVisualQueueAsync`
- `CompleteReconnectCatchupIfReady`
- `NetworkReconnectRules.ShouldApplyBoardSnapshotImmediatelyForClient`

## 6. Seat Control State Machine

当前 seat control 只冻结三种状态：

- `HumanConnected`
- `AiProxyReclaimable`
- `HumanReclaimPending`

固定流转：

```text
HumanConnected
  -> (disconnect during active match)
AiProxyReclaimable
  -> (valid reconnect claim)
HumanReclaimPending
  -> (that seat reaches its next turn start)
HumanConnected
```

约束：

- 只有非 Host 断线才进入 AI takeover 流
- `HumanReclaimPending` 表示人已经认领成功，但控制权要等到其下一个回合开始才恢复
- 相同 peer 的重复 reconnect claim 允许幂等接受
- 不同 peer 抢同一 seat 必须拒绝

实现锚点：

- `SeatControlRules.TryMarkDisconnected`
- `SeatControlRules.ApplyReconnectClaim`
- `SeatControlRules.TryPromoteAtTurnStart`

## 7. Input And Permission Rules

本地输入只有在以下条件同时满足时才允许：

- 当前是联机模式
- `NetworkSessionState == InGame`
- 找到了本地 seat control state
- 本地 seat mode 为 `HumanConnected`

远端动作只有在 `HumanConnected` 时才可接受。

pending target hand 只有在 `HumanConnected` 时才可见。

实现锚点：

- `LocalSeatControlRules`
- `NetworkSeatControlRules`

## 8. Pending Selection Rules

当前 pending selection 只使用以下模式：

- `TargetPlayer`
- `TargetHandCard`
- `MagnetHandCard`
- `HandLimitDiscard`

固定规则：

- pending selection 的权威状态必须进入快照
- 非 Host 客户端只能在 `InGame` 且没有 request in-flight 时发起 pending selection 请求
- `TargetHandCard` 的真实目标手牌只暴露给当前合法发起者
- Host 最终完成权威选择，并在必要时发送新快照

实现锚点：

- `GameBoardModels.PendingItemSelectionMode`
- `GameBoardScene.TargetItemSelection.cs`
- `NetworkSeatControlRules.CanSendPendingSelectionRequest`

## 9. Reconnect Presentation Rules

客户端 reconnect 展示层的固定语义：

- overlay 只在非 Host 客户端显示
- 触发条件：等待初始化、正在 reconnect、或尚未应用任何有效快照
- reconnect 完成后，如果本地 seat 进入 `HumanReclaimPending`，显示“下回合恢复控制”的 notice

实现锚点：

- `GameBoardScene.NetworkUi.cs`
- `NetworkReconnectPresentationRules`

