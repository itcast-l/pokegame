# Steam Reconnect Regression

Last updated: 2026-06-17

本文件定义 Steam reconnect 主链路的手工回归入口。它不是自动 smoke 的替代品，而是 Steam 运行时专用验收手册。

## Goal

确认以下真实链路可重复执行：

1. Host 建立 Steam lobby
2. Client 使用 lobby id 加入
3. 双方 ready 并进入牌局
4. Client 主动触发 reconnect drop
5. Host 观察到 `AiProxyReclaimable`
6. Client 重连
7. Host 观察到 `HumanReclaimPending`

## Required Environment

执行前必须同时满足：

- 两台运行环境，分别登录两个不同 Steam 账号
- 两端都使用同一份当前构建
- Steam 客户端已启动，并且 Godot 所在进程能正常初始化 Steam
- 能运行 Godot Mono
- 两端都从仓库根目录执行脚本

当前仓库内的本地开发前提还包括：

- `project.godot` 的 `steam/initialization/app_id` 必须是可用的测试或正式 app id，不能保持为 `0`

Godot 启动入口：

- Host: `tests/run_steam_reconnect_manual_host.sh`
- Client: `tests/run_steam_reconnect_manual_client.sh`

建议把日志同时归档到本地文件：

```sh
bash tests/run_steam_reconnect_manual_host.sh | tee /tmp/pokegame-steam-host.log
CODEX_STEAM_LOBBY_ID=<lobbyId> bash tests/run_steam_reconnect_manual_client.sh | tee /tmp/pokegame-steam-client.log
```

## Fixed Execution Order

1. Host 机器先执行：

```sh
bash tests/run_steam_reconnect_manual_host.sh
```

2. 记录 Host 输出的 `HOST_STEAM_LOBBY_ID <lobbyId>`。
3. Client 机器再执行：

```sh
CODEX_STEAM_LOBBY_ID=<lobbyId> bash tests/run_steam_reconnect_manual_client.sh
```

## Expected Logs

Host 必须看到：

- `HOST_START`
- `HOST_STEAM_LOBBY_ID`
- `HOST_START_MATCH`
- `HOST_BOARD_INIT`
- `HOST_SAW_AI`
- `HOST_SAW_RECLAIM`
- `HOST_DONE`

Client 必须看到：

- `CLIENT_START`
- `CLIENT_JOIN_LOBBY`
- `CLIENT_READY`
- `CLIENT_BOARD_INIT`
- `CLIENT_FORCE_RECONNECT_DROP`
- `CLIENT_RECONNECTING`
- `CLIENT_RECONNECTED`
- `CLIENT_DONE`

## Pass Criteria

本入口只有在以下条件同时满足时才算通过：

- Host / Client 日志全部完整
- Host 明确观察到 `HOST_SAW_AI -> HOST_SAW_RECLAIM`
- Client 明确观察到 `CLIENT_RECONNECTING -> CLIENT_RECONNECTED`
- 至少成功 1 次

优先目标：

- 连续成功 2 次，证明不是偶然通过

## Failure Classification

### 1. Steam 不可用

表现：

- 脚本直接失败
- `SteamAvailable` 为 `false`
- 没有进入 lobby

优先排查：

- Steam 客户端是否真的已启动
- 当前账号是否已登录
- 当前运行环境是否能让 Godot 进程访问 Steam
- `project.godot` 的 `steam/initialization/app_id` 是否仍然是 `0`
- 是否误用了不带 Steam 运行时的执行方式

这类问题优先归为 Steam 环境问题。

## Current Local Host Readiness

截至 2026-06-17，当前仓库在本机已确认能推进到以下状态：

- Steam 客户端启动后，Host 脚本可成功进入 Steam 路径
- Host 可打印 `HOST_START`
- Host 可进入 lobby，并打印 `HOST_STEAM_LOBBY_ID`

这说明“Host 建 lobby”这一步已经不再被仓库配置阻塞。

仍需要外部双环境才能继续完成的部分：

- 第二个客户端账号加入同一 Steam lobby
- ready / 开局 / reconnect drop / reclaim 全链路验收

### 2. Join 失败

表现：

- Host 已打印 `HOST_STEAM_LOBBY_ID`
- Client 没有进入 `CLIENT_READY`
- 或者 `CLIENT_JOIN_LOBBY` 后无法进入 lobby / in-game

优先排查：

- `CODEX_STEAM_LOBBY_ID` 是否来自当前 Host 进程
- Host 是否还留在同一个 lobby
- 两端是否使用同一构建
- 两端网络/Steam 好友与 join 条件是否允许互相加入

先排 Steam 环境和 lobby 元数据，再看游戏逻辑。

### 3. Lobby 成功但未开局

表现：

- Client 已加入
- 但 Host 没有 `HOST_START_MATCH` 或双方没有 `*_BOARD_INIT`

优先排查：

- 两端 ready 是否都已经发送
- `PLAYER_COUNT` 是否与执行环境人数一致
- Host 是否提前离开 lobby
- lobby state 是否被错误地卡在 `Lobby` / `Starting`

这类问题通常是游戏运行时问题。

### 4. Reconnect 未进入 reclaim

表现：

- Client 已触发 `CLIENT_FORCE_RECONNECT_DROP`
- 但 Host 没有 `HOST_SAW_AI` 或没有 `HOST_SAW_RECLAIM`

优先排查：

- seat control 状态是否从 `HumanConnected` 正确切到 `AiProxyReclaimable`
- reconnect claim 的 `match_id` / `reconnect_token` / `stable_user_id` 是否一致
- Steam lobby owner 是否发生变化，触发 owner-migration abort
- reconnect snapshot 是否实际发送并被客户端确认

这类问题优先归为游戏运行时问题。

## Runtime Facts Frozen By This Doc

当前实现明确依赖以下行为：

- Host 侧 harness 观察 seat 1 的 `SeatControlMode`
- Client 侧 harness 使用 `DebugSimulateLocalReconnectDrop()`
- Steam reconnect 期间如果 lobby owner 变更，当前规则会中止该次重连
- 本入口只验证 Steam 传输下的 reconnect / reclaim 链路，不替代视觉质量手测

实现锚点：

- `tests/SteamReconnectManualHost.gd`
- `tests/SteamReconnectManualClient.gd`
- `tests/STEAM_RECONNECT_MANUAL_CHECKS.md`
- `scripts/NetworkReconnectRules.cs`
- `scripts/SeatControlRules.cs`
