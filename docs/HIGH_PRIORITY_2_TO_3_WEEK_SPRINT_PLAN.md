# High Priority 2-3 Week Sprint Plan

Last updated: 2026-06-17

本文件定义当前项目高优先级事项的 2-3 周冲刺方案。目标是把项目从“LAN 联机原型已稳定”推进到“联机底座可持续扩展、Steam 联调可重复执行、自动化与工程资料可支撑继续开发”的状态。

本方案只覆盖当前四个高优先级任务：

- 资源缺口与 smoke 清零
- Steam 联调闭环
- 联机权威模型文档冻结
- 新卡效接入门槛建立

本方案不覆盖以下内容：

- Steam 商店页、成就、云存档、正式发版流程
- 新卡牌内容扩张
- 纯美术 polish 型低优先级替换

## Summary

本轮采用以下固定策略：

- 资源问题采用“代码兜底 + 资源补齐并行”
- Steam 侧按“具备双账号 / 双运行环境”处理，列为本冲刺强验收项
- LAN 联机底座视为已稳定，不重写 `NetworkManager` 主架构

执行顺序按依赖关系固定为：

1. 清理资源缺口与 smoke 噪音
2. 完成 Steam 联调闭环
3. 冻结联机权威模型文档
4. 建立新卡效接入门槛

## Current State

截至 2026-06-17，已确认的事实如下：

- `dotnet build pokegame.csproj` 可通过
- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj` 实测 `87 passed, 0 failed`
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh` 可通过，覆盖：
  - scene smoke
  - game board mode regression
  - LAN basic turn sync
  - LAN reconnect
  - LAN own-turn reconnect
  - LAN reconnect during visual
  - LAN reclaim pending
  - LAN host ended match
  - LAN no-reconnect until match end
  - LAN three-player disconnect
  - LAN pending discard / target / fallback

已确认的主要短板：

- smoke 日志仍有反馈特效缺图报错
- Steam 运行路径已接入，但仍依赖双账号双环境人工联调
- README 当前引用了缺失文档：
  - `docs/ROADMAP.md`
  - `docs/SPECIAL_CARD_SYSTEM.md`

## Scope A — 资源缺口与 Smoke 清零

### Problem

当前自动 smoke 虽然通过，但存在运行时资源加载错误，主要集中在 `avatar_feedback_*` 反馈特效帧数不一致问题。

当前已确认资源现状：

- `avatar_feedback_score`: 仅有 `fx-1..4`
- `avatar_feedback_hit`: 仅有 `fx-1..4`
- `avatar_feedback_defeat`: 仅有 `fx-1..4`
- `avatar_feedback_victory`: 仅有 `fx-1..6`

当前加载逻辑位于：

- `scripts/CharacterAvatar.Frames.cs`
- `scripts/FrameAnimationHelpers.cs`

### Decisions

- 不再把“后续帧缺失”视为必须抛出错误的情况
- 反馈特效加载必须按“实际存在帧”工作
- 同时补齐当前已知缺失资源，避免演出时长和观感不稳定

### Work Items

1. 调整反馈特效加载逻辑
   - `LoadNamedFxFrames()` 继续按顺序加载
   - 遇到缺失帧时停止收集，不再制造错误级别噪音
   - 调用侧允许不同反馈特效拥有不同帧数

2. 补齐当前缺失反馈帧
   - `score / hit / defeat` 至少补到统一可接受帧数
   - `victory` 补齐到最终预期帧数
   - 命名必须连续，避免中间断号

3. 建立资源缺口基线
   - 记录本轮修复对象
   - 后续 smoke 若再出现资源错误，直接视为新增回归

### Acceptance

- `bash tests/run_multiplayer_reconnect_smoke_suite.sh` 全通过
- 日志中不再出现 `avatar_feedback_*` 缺图报错
- 反馈演出在牌局与结果展示中可正常播放

## Scope B — Steam 联调闭环

### Problem

Steam 运行时主路径已经具备，但当前仍属于“代码路径存在，产品验证不闭环”。现有自动化不能替代真实 Steam 双账号联调。

当前已存在入口：

- `tests/run_steam_reconnect_manual_host.sh`
- `tests/run_steam_reconnect_manual_client.sh`
- `tests/SteamReconnectManualHost.gd`
- `tests/SteamReconnectManualClient.gd`
- `tests/STEAM_RECONNECT_MANUAL_CHECKS.md`

### Decisions

- 本轮不扩展为完整 Steam 上线方案
- 只把 Steam 联机主链路做成“可重复联调、可明确验收、可记录失败”
- Host / Client 日志关键字视为正式验收接口，不随意改名

### Work Items

1. 固定 Steam 联调前置条件
   - 两个不同 Steam 账号
   - 两个可互相加入 lobby 的运行环境
   - 启动方式、环境变量、日志保存方式写入文档

2. 完整执行当前手工联调链路
   - Host 建房并输出 lobby id
   - Client 使用 lobby id 加入并 ready
   - 双方进入牌局
   - Client 模拟断线
   - Host 观察到 `AiProxyReclaimable`
   - Client 重连成功
   - Host 观察到 `HumanReclaimPending`

3. 补齐失败排查说明
   - Steam 不可用
   - lobby id 无效
   - join 成功但未开局
   - reconnect 未进入 reclaim

### Acceptance

Host 端必须出现：

- `HOST_START`
- `HOST_STEAM_LOBBY_ID`
- `HOST_START_MATCH`
- `HOST_BOARD_INIT`
- `HOST_SAW_AI`
- `HOST_SAW_RECLAIM`
- `HOST_DONE`

Client 端必须出现：

- `CLIENT_START`
- `CLIENT_JOIN_LOBBY`
- `CLIENT_READY`
- `CLIENT_BOARD_INIT`
- `CLIENT_FORCE_RECONNECT_DROP`
- `CLIENT_RECONNECTING`
- `CLIENT_RECONNECTED`
- `CLIENT_DONE`

## Scope C — 联机权威模型文档冻结

### Problem

当前联机逻辑已经复杂到不能继续依赖阅读散落代码来理解。后续继续加卡效时，如果没有统一文档，极易破坏同步、重连和 pending selection。

### Decisions

- README 中引用缺失的联机/卡效文档必须补回
- 文档要描述行为模型，不写成纯文件索引
- 后续卡效实现默认以该文档为准，而不是由实现者自行判断

### Work Items

1. 补回联机权威模型文档
   - Host 权威状态源
   - 客户端预测边界
   - 快照覆盖时机
   - 视觉事件与状态快照职责分离
   - reconnect catch-up 流程

2. 补回特殊卡效同步文档
   - 哪些卡效只允许 Host 结算
   - 哪些卡效会产生视觉事件
   - 哪些卡效会进入 pending selection
   - 哪些卡效会影响重连后的局面恢复

3. 更新 README 引用
   - 移除无效路径
   - 指向新文档

### Acceptance

- README 不再引用缺失文档
- 新文档能够独立说明：
  - Host 权威
  - 客户端预测
  - pending selection
  - AI takeover / reclaim
  - 新卡效同步边界

## Scope D — 新卡效接入门槛

### Problem

当前最大风险已经不是“玩法不够”，而是“后续加内容时把联机底座打穿”。必须给后续卡效开发设定硬门槛。

### Decisions

- 任何新增或修改卡效，必须同时补规则验证
- 只要涉及同步、选择、视觉回放、重连中的任一项，就必须补联机验证
- 先使用仓库内现有 `Rules.Tests` 和 smoke 脚本，不额外引入更重的 CI 体系

### Work Items

1. 定义卡效测试门槛
   - 纯规则型：补 `Rules.Tests`
   - 桌面结构变更型：补规则 + smoke
   - 目标选择型：补规则 + pending selection 验证
   - 附着 / 延迟触发型：补规则 + reconnect 恢复验证
   - 视觉事件型：补规则 + visual smoke 或手测

2. 写入文档与开发约束
   - 新卡效 PR 或提交前必须跑哪些检查
   - 哪类问题不能只靠本地对战验证

3. 对现有高风险卡效做一轮映射盘点
   - 标记哪些已有卡效未来一旦修改必须跑哪套回归

### Acceptance

- 有一份明确的“卡效类型 -> 必跑验证”映射
- 后续实现者无需自行判断测试范围
- 新卡效不再允许“只在本地打通就算完成”

## Milestones

### Week 1 — 清噪音、稳基线

- 完成反馈特效加载兜底
- 补齐当前反馈帧缺口
- 重新跑 build / rules / multiplayer smoke

验收：

- build 通过
- `Rules.Tests` 全通过
- multiplayer smoke 全通过
- 无 `avatar_feedback_*` 缺图报错

### Week 2 — Steam 联调收口

- 完成双环境 Steam reconnect 联调
- 固定日志留存与失败排查方式
- 收口手工联调文档

验收：

- Host / Client 全链路通过一次
- 联调记录可复跑
- Steam 问题与 LAN 问题边界清晰

### Week 3 — 文档冻结与扩展门槛

- 补回联机权威模型文档
- 补回卡效同步约束文档
- 固定新卡效接入测试门槛

验收：

- README 文档引用有效
- 联机与卡效边界不再依赖口头传递
- 项目进入“可控扩展”状态

## Required Verification Commands

本轮必须至少执行并记录以下检查：

```sh
dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

Steam 联调：

```sh
bash tests/run_steam_reconnect_manual_host.sh
CODEX_STEAM_LOBBY_ID=<lobbyId> bash tests/run_steam_reconnect_manual_client.sh
```

## Assumptions

- 本轮不处理 Steam 商店上线素材与正式发版流程
- 当前 LAN 联机架构不重写，只做收口与门槛建设
- 生图资源只补“挡验收”的部分，非阻塞性 polish 资源延后
- 如果 Steam 双环境在执行中临时不可用，则 Week 2 只允许降级为“准备完成 + 文档齐备”，不允许替换成纯 LAN 自证
