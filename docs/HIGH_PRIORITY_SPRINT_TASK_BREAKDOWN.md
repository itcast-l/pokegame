# High Priority Sprint Task Breakdown

Last updated: 2026-06-17

本文件是 [HIGH_PRIORITY_2_TO_3_WEEK_SPRINT_PLAN.md](./HIGH_PRIORITY_2_TO_3_WEEK_SPRINT_PLAN.md) 的执行拆分版。  
目标不是重复高层方案，而是把高优先级事项拆成可以直接照着推进的周计划、日任务、模块边界与验收动作。

默认前提：

- 单人主开发推进
- 周期按 3 周设计，若进展顺利可压缩到 2 周
- 优先追求“底座稳定 + 验收闭环”，不插入新玩法开发

## Summary

本轮冲刺固定分为三段：

1. `Week 1`
   - 清理资源噪音
   - 稳定自动化基线
2. `Week 2`
   - 跑通 Steam 联调
   - 固定联调操作与失败排查
3. `Week 3`
   - 冻结联机权威模型文档
   - 建立新卡效接入门槛

每一周都必须有明确“交付物”和“停止条件”，避免工作发散。

## Week 1 — 资源缺口与 Smoke 清零

### Goal

让当前多人 smoke 保持通过，同时彻底清掉 `avatar_feedback_*` 相关资源报错，把自动化环境变成可靠基线。

### Day 1 — 资源缺口确认与修复范围冻结

任务：

- 复跑一次：
  - `dotnet build pokegame.csproj`
  - `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
  - `bash tests/run_multiplayer_reconnect_smoke_suite.sh`
- 记录当前所有资源加载报错
- 只保留本轮必须修的阻塞项
- 冻结反馈特效缺口清单

重点模块：

- `scripts/CharacterAvatar.Frames.cs`
- `scripts/FrameAnimationHelpers.cs`
- `art/fx/avatar_feedback_*`

交付物：

- 一份确认后的缺口列表
- 一次 smoke 基线日志

当天完成标准：

- 已确认本轮只处理反馈特效缺口，不扩展到其他非阻塞资源

### Day 2 — 代码兜底实现

任务：

- 调整反馈特效加载逻辑
- 保证加载器按“实际存在帧”工作
- 保证帧缺失时不再制造错误级别噪音
- 保证不同反馈特效组允许不同帧数

重点模块：

- `scripts/CharacterAvatar.Frames.cs`
- `scripts/FrameAnimationHelpers.cs`

验收：

- 本地进入主菜单、牌局、结果页时不再因为缺少后续反馈帧报错
- 相关行为不影响已有攻击特效与动作帧加载

### Day 3 — 资源补齐

任务：

- 补齐 `score / hit / defeat / victory` 反馈特效缺失帧
- 保持命名连续
- 保持现有目录结构不变
- 优先补齐“能过验收”的最低可用版本

重点资源：

- `art/fx/avatar_feedback_score/run-001/processed/`
- `art/fx/avatar_feedback_hit/run-001/processed/`
- `art/fx/avatar_feedback_defeat/run-001/processed/`
- `art/fx/avatar_feedback_victory/run-001/processed/`

验收：

- 所有反馈特效至少达到稳定可播放状态
- 没有中间断号

### Day 4 — 自动化回归

任务：

- 复跑：
  - `dotnet build pokegame.csproj`
  - `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
  - `bash tests/run_multiplayer_reconnect_smoke_suite.sh`
- 检查是否仍有资源错误
- 如果还有报错，只允许继续处理本轮冻结范围内的问题

交付物：

- 一份“资源清噪后”的 smoke 验证结果

停止条件：

- `avatar_feedback_*` 不再报错
- multiplayer smoke 保持全绿

### Day 5 — 基线封板

任务：

- 整理本轮资源修复说明
- 记录未来 smoke 若再次出现资源错误时的判定标准
- 明确哪些资源问题属于新增回归

交付物：

- 一份简短的资源基线说明

Week 1 完成标准：

- build 通过
- `Rules.Tests` 全通过
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh` 全通过
- 无反馈特效缺图报错

## Week 2 — Steam 联调闭环

### Goal

把已有 Steam 运行时路径变成“可重复联调、可归档结果、可明确失败点”的真实验证流程。

### Day 1 — 环境准备

任务：

- 确认两台运行环境都能正常登录不同 Steam 账号
- 确认 Godot 启动路径、Steam 客户端状态、日志保存位置
- 固定 Host / Client 执行顺序

重点入口：

- `tests/run_steam_reconnect_manual_host.sh`
- `tests/run_steam_reconnect_manual_client.sh`
- `tests/STEAM_RECONNECT_MANUAL_CHECKS.md`

交付物：

- 一份可复跑的联调准备清单

### Day 2 — 首次全链路联调

任务：

- Host 启动并输出 lobby id
- Client 使用 lobby id 加入
- 双方 ready 并进入牌局
- Client 主动触发 reconnect drop
- Host 观察 AI takeover
- Client 重连
- Host 观察 reclaim pending

验收日志：

- Host：
  - `HOST_START`
  - `HOST_STEAM_LOBBY_ID`
  - `HOST_START_MATCH`
  - `HOST_BOARD_INIT`
  - `HOST_SAW_AI`
  - `HOST_SAW_RECLAIM`
  - `HOST_DONE`
- Client：
  - `CLIENT_START`
  - `CLIENT_JOIN_LOBBY`
  - `CLIENT_READY`
  - `CLIENT_BOARD_INIT`
  - `CLIENT_FORCE_RECONNECT_DROP`
  - `CLIENT_RECONNECTING`
  - `CLIENT_RECONNECTED`
  - `CLIENT_DONE`

### Day 3 — 失败点固化

任务：

- 如果首次联调失败，归类失败位置：
  - Steam 不可用
  - join 失败
  - lobby 成功但未开局
  - reconnect 未进入 reclaim
- 为每类失败写首要排查步骤
- 区分“Steam 环境问题”和“游戏运行时问题”

交付物：

- 一份失败分类表

### Day 4 — 复跑确认

任务：

- 再跑一次 Host / Client 手工联调
- 验证不是偶然成功
- 保留最终通过日志

停止条件：

- 至少 1 次完整成功
- 若条件允许，优先做到 2 次成功

### Day 5 — 文档封板

任务：

- 整理最终 Steam 联调文档
- 明确前置条件、执行步骤、预期日志、失败排查
- 把它定为后续 Steam 改动的标准回归入口

Week 2 完成标准：

- Steam reconnect 主链路至少成功通过一次
- Host / Client 验收日志完整
- 手工联调流程可由未来执行者直接照做

## Week 3 — 文档冻结与卡效接入门槛

### Goal

把“靠读代码理解同步规则”切换成“靠文档执行开发”，并正式建立后续卡效扩展的底线约束。

### Day 1 — 联机权威模型梳理

任务：

- 盘点 Host 权威、客户端预测、快照覆盖、视觉事件队列
- 抽出真正稳定的同步行为
- 排除仅用于临时调试的实现细节

文档必须覆盖：

- Host 权威状态源
- 客户端允许的预测行为
- 何时发送快照
- 何时应用快照
- reconnect catch-up 的状态转换

### Day 2 — pending selection / AI takeover / reclaim 梳理

任务：

- 梳理 pending item selection 权威归属
- 梳理 seat control 状态流转：
  - `HumanConnected`
  - `AiProxyReclaimable`
  - `HumanReclaimPending`
- 明确谁能发动作、谁能看目标手牌、何时恢复人类控制

验收：

- 文档中每个状态都有明确定义与进入条件

### Day 3 — 特殊卡效同步约束文档

任务：

- 按卡效行为分类，而不是按文件分类
- 明确以下类型的同步要求：
  - 纯规则类
  - 桌面结构变化类
  - 目标选择类
  - 附着 / 延迟触发类
  - 会产生视觉事件类
  - 会影响重连恢复类

交付物：

- 一份“卡效类型 -> 同步要求”文档

### Day 4 — 新卡效接入门槛文档

任务：

- 定义“新增或修改卡效必须补哪些验证”
- 固定最低门槛：
  - 纯规则变化：`Rules.Tests`
  - 涉及同步 / 选择 / 重连 / 视觉恢复：补 smoke 或手测脚本

验收：

- 实现者无需再自己判断“需不需要联机回归”

### Day 5 — README 与入口收口

任务：

- 修复 README 中失效引用
- 指向新文档
- 确保后续开发者从 README 能找到：
  - 联机权威模型
  - 卡效同步约束
  - Steam 联调入口
  - 多人 smoke 入口

Week 3 完成标准：

- README 不再引用失效文档
- 联机权威模型文档完成
- 卡效同步约束文档完成
- 新卡效接入测试门槛明确

## Module-Level Breakdown

### 模块 A — 反馈特效与资源加载

输出：

- 加载器容错
- 缺帧补齐
- smoke 清噪

影响范围：

- `CharacterAvatar`
- 结果演出
- 主菜单/牌局启动 smoke

### 模块 B — Steam 联机验证

输出：

- 标准化联调流程
- reconnect 主链路人工验收
- 失败定位方法

影响范围：

- `NetworkManager`
- `SteamManager.gd`
- 手工验证脚本

### 模块 C — 联机权威规则文档

输出：

- Host / Client 权责边界
- snapshot / visual event / reconnect 关系说明

影响范围：

- 后续所有联机卡效开发

### 模块 D — 卡效开发门槛

输出：

- 卡效类型到验证要求映射
- 后续开发底线

影响范围：

- `Rules.Tests`
- multiplayer smoke
- 未来新增测试脚本

## Mandatory Verification

每周结束前至少执行：

```sh
dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Week 1 和 Week 3 必跑：

```sh
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

Week 2 必跑：

```sh
bash tests/run_steam_reconnect_manual_host.sh
CODEX_STEAM_LOBBY_ID=<lobbyId> bash tests/run_steam_reconnect_manual_client.sh
```

## Done Definition

本轮冲刺只有在以下条件全部满足时才算完成：

- 自动化基线干净，没有当前已知反馈特效资源报错
- LAN 联机 smoke 仍然全通过
- Steam reconnect 手工联调至少成功一次
- 联机权威模型文档补齐
- 新卡效接入测试门槛补齐
- README 文档入口有效

## Not In Scope

以下事项明确不插入本轮拆分：

- 新卡牌内容设计
- 商店页文案与 capsule
- 成就、云存档、发版 CI
- 大规模 UI polish
- 非阻塞性美术重做
