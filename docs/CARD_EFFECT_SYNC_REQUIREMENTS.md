# Card Effect Sync Requirements

Last updated: 2026-06-17

本文件按“行为类型”而不是按文件拆分联机卡效约束。任何新增或修改卡效，都先归类，再按该类执行验证。

## Category A — Pure Rule Changes

定义：

- 只改变规则判断或分数计算
- 不引入新的私有信息暴露
- 不改变桌面同步结构
- 不新增视觉事件
- 不影响 reconnect 恢复

要求：

- 仍由 Host 做最终结算
- 结果必须能通过普通快照表达，不新增特殊同步字段
- 最低验证是 `Rules.Tests`

典型场景：

- 规则辅助函数
- 分数计算
- 卡牌定义 metadata 的非联机结构性调整

## Category B — Public Board Structure Changes

定义：

- 会改变桌面、牌堆、公开可见卡牌位置或数量

要求：

- Host 权威结算
- 如果存在动画或临时视觉表现，视觉事件必须携带 `snapshot_id`
- 结算后必须发送能重建最终桌面的权威快照
- reconnect 后仅靠快照也要能恢复正确最终态

典型 effect：

- `place_table_pair_destroy`
- `collect_table`
- `destroy_cards`
- `shuffle_table`
- `steal_deck_cards`
- `draw_card`

## Category C — Target Selection / Private Information

定义：

- 需要选目标玩家、目标手牌，或会短暂暴露本来私有的信息

要求：

- pending selection 状态必须写入快照
- 只能向合法选择发起者暴露目标手牌
- seat mode 不是 `HumanConnected` 时不能继续使用该权限
- 非 Host 客户端只能发请求，不能自行完成权威结算

典型 effect / flow：

- `steal_card`
- `TargetPlayer`
- `TargetHandCard`

## Category D — Attachment / Delayed Trigger

定义：

- 把效果附着到牌上
- 当前回合不一定立即结算
- 后续捕获、翻牌、回合推进时才会触发

要求：

- 附着状态必须进入桌面快照
- reconnect 后不能丢失附着关系
- AI takeover / reclaim 期间不能把待触发状态跳过
- 任何 attached effect 的触发条件都必须能在 snapshot 恢复后继续成立

典型 effect：

- `attach_modifier`
- `magnet_extra_capture`
- `double_collect`
- `extra_turn`

## Category E — Visual Event Coupled Effects

定义：

- 除了规则结算，还依赖视觉事件队列维持可读性

要求：

- Host 广播的视觉事件必须绑定 `snapshot_id`
- 客户端普通 in-game 路径允许先播事件再套快照
- reconnect 客户端必须允许直接快照覆盖并清理旧视觉队列
- 视觉事件永远不能成为唯一状态来源

典型 effect / animation：

- `reveal_cards`
- lens reveal
- bomb pair destroy
- deck shuffle
- table shuffle
- question reward reveal

## Category F — Reconnect-Sensitive Changes

定义：

- 任何改动会影响 reconnect 后恢复结果、overlay 行为、seat reclaim、或 pending selection 继续执行

要求：

- 必须验证 reconnect catch-up
- 必须验证 AI takeover / reclaim 路径
- 必须确认快照可单独恢复最终态

高风险点：

- snapshot schema 变更
- pending selection 字段变更
- seat control 规则变更
- 目标手牌暴露逻辑变更
- 视觉队列与快照先后关系变更

## Current Effect Mapping

当前项目可按以下方式快速归类：

- `place_table_pair_destroy`: Category B + E + F
- `draw_card`: Category B + F
- `steal_card`: Category C + F
- `steal_deck_cards`: Category B + C + F
- `collect_table`: Category B + E + F
- `attach_modifier`: Category D + F
- `reveal_cards`: Category B + E + F
- `shuffle_table`: Category B + E + F
- `double_collect`: Category D + F
- `extra_turn`: Category D + F
- `destroy_cards`: Category B + E + F
- `grant_reward`: 若只改奖励判定，按 Category A；若改奖励展示或附着后续流程，按实际升级

## Non-Negotiable Rule

不要因为“本地看起来能用”就跳过联机验证。

只要卡效属于 Category B 到 F 中任一类，就默认不是纯本地改动。

