# Steam Three-Account Host Migration Release Gate

该验收由用户手动执行，使用三个真实 Steam 账号、三个独立 Steam 会话和同一 Release 候选构建。源码 harness 只负责驱动与记录；不能用一个账号的多进程、Steam 离线模式或 LAN 结果替代。

## 通用准备

先按 `tests/RELEASE_TARGET_RUNTIME_CHECKS.md` 验证各平台 Release 候选。三个账号使用的程序必须来自证据清单引用的同一 `build_id`，不能用源码编辑器运行结果代替目标包。

当前待验收构建为 `deckland-rc-20260801-host-migration-5`，统一清单位于 `outputs/HOST_MIGRATION_RC5_RELEASE_EVIDENCE.pending.json`。开始前先确认三台设备使用该清单固定的 RC5 发行物哈希；每完成一个场景，就把三个账号、原始日志、正常退出结果和 UTC 时间线写入该场景条目，禁止只把 `status` 改为 `passed`。

三个设备分别设置唯一标签，并固定同一个构建标识：

```bash
export CODEX_BUILD_ID="<release-build-id-or-git-sha>"
export CODEX_STEAM_MIGRATION_PEER_LABEL="account-a-windows"
export CODEX_STEAM_MIGRATION_RESULT="$PWD/evidence/account-a.json"
export CODEX_STEAM_MIGRATION_LOG="$PWD/evidence/account-a.log"
```

Host 额外设置：

```bash
export CODEX_STEAM_MIGRATION_ROLE=host
export CODEX_STEAM_MIGRATION_SCENARIO=source_crash
export CODEX_STEAM_MIGRATION_EXPECTED_OUTCOME=source_exit
bash tests/run_steam_host_migration_manual_peer.sh
```

从 Host 日志取得 `kind=LOBBY id=<lobbyId>`。两个 Peer 分别设置：

```bash
export CODEX_STEAM_MIGRATION_ROLE=peer
export CODEX_STEAM_LOBBY_ID=<lobbyId>
export CODEX_STEAM_MIGRATION_SCENARIO=source_crash
export CODEX_STEAM_MIGRATION_EXPECTED_OUTCOME=continue
bash tests/run_steam_host_migration_manual_peer.sh
```

每个场景必须保存三个日志、所有正常退出进程的 JSON 结果、外部强杀时间线和平台/设备信息。日志中的 `steam_id` 必须对应三个不同账号。

## 验收矩阵

### 1. 主动 Owner 转移

- 三端使用 `active_owner_transfer`。
- Host 使用 `source_exit`，两个 Peer 使用 `continue`。
- harness 在检查点和迁移计划可用后调用 `RequestActiveHostMigration()`。
- 新 Host 必须记录 `MIGRATION_HOST_BOUND`、`MIGRATION_CHECKPOINT_RESTORED`、`MIGRATION_RECOVERY_READY` 与 `kind=BECAME_HOST`。
- 两个 Peer 都必须记录 winner 和 `kind=DONE outcome=continue`。

### 2. Host 强杀后的 Owner 收敛

- 三端使用 `source_crash`；Host 使用 `source_exit`，Peer 使用 `continue`。
- 等待 Host 日志同时出现 `MIGRATION_PLAN_READY phase=InGame` 和 `kind=SOURCE_KILL_READY`。
- 使用任务管理器或 `kill -9 <pid>` 强制终止 Host，禁止正常关闭。
- 记录强杀的 UTC 时间、PID、执行设备和操作者；Host 日志不得出现正常 `kind=DONE`。
- 两个存活 Peer 必须恢复并完成同一 winner。

### 3. 自动 Owner 非签名候选后的二次转移

- 三端使用 `automatic_owner_second_transfer`。
- 先根据 Host 的 `candidate_seat` 和两个 Peer 的 `kind=LOCAL_SEAT` 确认签名候选。
- 将非候选 Peer 的 Steam ID 配置到 Host：`CODEX_STEAM_SECONDARY_OWNER_ID=<followerSteamId>`。
- Host 记录 `kind=SECONDARY_OWNER_REQUEST ... accepted=true` 和目标 owner 后，等待 `kind=SOURCE_KILL_READY` 再强杀 Host。
- 存活日志必须证明 owner 时间线为 `source -> non-candidate -> signed candidate`，不能只记录最终 owner。
- 签名候选升任，两个 Peer 完成同一 winner。

### 4. Owner 转移超时

- 三端使用 `owner_timeout`；Host 和 Peer 都使用 `safe_end`。
- 使用实验室 Steam 故障代理或防火墙规则阻断 owner 更新/转移，不能修改生产超时常量。
- Host 主动请求迁移后，必须在有限时间出现 `MIGRATION_ACTIVE_FAILED reason=SteamOwnerTimeout`。
- 三端最终进入安全结束，不得有新 Host、winner、无限重试或残留迁移产物。

### 5. 候选退出

- 三端使用 `candidate_exit`。
- 等待 Host 的 `MIGRATION_PLAN_READY`，按 `candidate_seat` 找到候选进程并强杀。
- Host 必须撤销旧计划并选择仍在线 Peer；保存旧、新两个候选席位和计划修订时间线。
- 新计划 ready 后再强杀 Host。剩余 Peer 必须继续完成，或在候选已不可用时于有限时间安全结束；实际结果必须与证据清单的预期一致。

## 日志禁止项

任何日志或 JSON 出现以下内容即失败：

- 房间密码、席位原始凭证、私钥、会话密钥、nonce 字节或完整检查点；
- `SCRIPT ERROR`、未处理异常、双 Host 或权威代次回退；
- 结果仅写“通过”而没有账号角色、时间线、日志路径和最终状态。

完成后用不带 `--allow-pending` 的 `tools/validate_host_migration_release_evidence.py` 对统一证据清单执行强校验。
