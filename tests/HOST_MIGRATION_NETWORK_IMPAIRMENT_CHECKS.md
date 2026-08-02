# Host Migration Network Impairment Checks

该套件由用户在隔离 Linux 网络环境中手动执行，在三人 LAN 完整对局上注入真实 UDP 链路损伤，并在链路恢复后强杀原 Host，验证迁移后的最终局面一致性。它覆盖：

- 350 ms 延迟与 75 ms 抖动；
- 15% 随机丢包；
- 20% 重复包；
- 4 秒完全断网及原 Host 重连；
- 原 Host 与已签名候选同时强杀后的有限安全失败。

前四项必须使用外部链路损伤器。仓库提供 Linux `tc netem` 驱动；最后一项直接强杀两个进程。

当前待验收构建为 `deckland-rc-20260801-host-migration-5`，结果必须写入 `outputs/HOST_MIGRATION_RC5_RELEASE_EVIDENCE.pending.json` 对应的五个 `network_impairment_cases` 条目。运行前确认被测程序与清单中的 RC5 PCK 哈希一致；源码编辑器结果不能替代发行候选证据。

## Linux 隔离环境

只在专用 VM、network namespace 或其他可丢弃的隔离网卡上执行。驱动会替换目标网卡的 root qdisc，不应指向日常开发或远程登录使用的网卡。

```bash
sudo -v
export CODEX_NETEM_INTERFACE=lo
export CODEX_NETEM_ISOLATED_INTERFACE=1
export CODEX_LAN_MIGRATION_ADDRESS=127.0.0.1
bash tests/run_lan_host_migration_network_impairment_suite.sh
```

非 root 用户必须允许 `sudo -n tc ...`。执行过程中断时，手工清理：

```bash
sudo tc qdisc del dev "$CODEX_NETEM_INTERFACE" root
```

单独重跑某项：

```bash
NETWORK_IMPAIRMENT_ONLY_CASE=packet_loss \
KEEP_NETWORK_IMPAIRMENT_TMP=1 \
bash tests/run_lan_host_migration_network_impairment_suite.sh
```

套件会把完整终端输出写入保留目录中的 `suite.log`。也可用
`CODEX_NETWORK_IMPAIRMENT_SUITE_LOG=/absolute/evidence/suite.log` 将总日志直接写入发行证据目录。

## 驱动契约

其他平台或实验室网络设备可通过 `CODEX_NETEM_DRIVER` 接入。驱动必须支持：

```text
<driver> apply latency
<driver> apply packet_loss
<driver> apply duplicate_packet
<driver> apply short_disconnect
<driver> clear
```

驱动返回成功前，损伤必须已经生效；`clear` 返回成功前，链路必须恢复。Windows 可使用隔离 VM 中的 WAN 模拟器，macOS 可使用 Network Link Conditioner 或实验室路由器适配器，但不得把界面勾选截图作为唯一证据。

## 通过条件

每个成功恢复场景都必须出现：

- `NETWORK_IMPAIRMENT_APPLIED` 与 `NETWORK_IMPAIRMENT_CLEARED`；
- 损伤清除后的新 `MIGRATION_PLAN_READY`；
- 两个客户端均进入 `MIGRATING_HOST_STATE`，恢复后到达 winner；
- Host 期望结果与两个客户端的 fingerprint、snapshot、分数、牌堆和手牌完全一致；
- 无 `SCRIPT ERROR`、未处理异常或 `MIGRATION_FAILED`。

短断网还必须出现 `InGame -> Reconnecting -> InGame`。同时掉线场景必须在 35 秒内进入 `ConnectTimeout`，显示失败 UI，并清除迁移产物，不允许无限重连。
