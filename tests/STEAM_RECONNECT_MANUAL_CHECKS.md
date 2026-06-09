# Steam Reconnect Manual Checks

这些脚本不加入自动 smoke suite，因为它们依赖：

- Steam 正常初始化
- 两个不同的 Steam 账号
- 两个能互相加入同一个 Steam lobby 的运行环境

## 1. Host 侧

在 Host 机器执行：

```bash
bash tests/run_steam_reconnect_manual_host.sh
```

期望日志包含：

- `HOST_START`
- `HOST_STEAM_LOBBY_ID <lobbyId>`
- `HOST_START_MATCH`
- `HOST_BOARD_INIT`
- `HOST_SAW_AI`
- `HOST_SAW_RECLAIM`
- `HOST_DONE`

## 2. Client 侧

拿到 Host 输出的 `HOST_STEAM_LOBBY_ID` 后，在 Client 机器执行：

```bash
CODEX_STEAM_LOBBY_ID=<lobbyId> bash tests/run_steam_reconnect_manual_client.sh
```

期望日志包含：

- `CLIENT_START`
- `CLIENT_JOIN_LOBBY <lobbyId>`
- `CLIENT_READY`
- `CLIENT_BOARD_INIT`
- `CLIENT_FORCE_RECONNECT_DROP`
- `CLIENT_RECONNECTING`
- `CLIENT_RECONNECTED`
- `CLIENT_DONE`

## 3. 关注点

- `CLIENT_FORCE_RECONNECT_DROP` 通过 `DebugSimulateLocalReconnectDrop()` 主动制造“客户端传输中断但保留重连上下文”，便于验证 Steam 下的重连认领链路。
- 如果 Host 能观察到 `HOST_SAW_AI -> HOST_SAW_RECLAIM`，说明 Steam 运行时至少走通了：
  - 断线后 seat 进入 `AiProxyReclaimable`
  - 重新认领后 seat 进入 `HumanReclaimPending`
- 这个 harness 只验证 Steam 传输下的 reconnect / reclaim runtime path，不覆盖“画面不出现明显闪跳”的人工视觉质量判断。
