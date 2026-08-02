# Release Target Runtime Checks

每个平台的发行候选必须在对应操作系统上运行真实导出程序。跨平台 PCK harness、Wine/CrossOver 和人工填写的 `runtime_passed=true` 都不能替代目标机证据。

## 统一候选构建

Windows、Linux 和 macOS 导出必须使用同一个候选构建标识：

```bash
BUILD_ID="deckland-rc-YYYYMMDD-N"

python3 tools/release_export.py \
  --godot /absolute/path/to/Godot \
  --preset "Linux" \
  --build-id "$BUILD_ID" \
  --allow-target-runtime-pending \
  --output /absolute/path/to/release-linux
```

在原生平台导出时不需要 `--allow-target-runtime-pending`。从其他平台生成的候选报告状态只能是 `target_runtime_pending`，不得作为可发布结论。

将整个发行目录复制到目标机，至少保留：

```text
release-audit.json
package/**
```

不要单独复制 EXE 或 PCK；验证器会从报告目录解析相对路径，并重新计算两者 SHA-256。

## 目标机执行

Linux/macOS：

```bash
python3 tools/verify_release_target_runtime.py \
  /absolute/release/release-audit.json \
  --device-id <stable-physical-device-label> \
  --output /absolute/evidence/<platform>/target-runtime.json
```

Windows PowerShell：

```powershell
py -3 tools/verify_release_target_runtime.py `
  C:\absolute\release\release-audit.json `
  --device-id <stable-physical-device-label> `
  --output C:\absolute\evidence\windows\target-runtime.json
```

验证器固定执行：

```text
<release executable> --headless --audio-driver Dummy -- --release-package-smoke
```

通过时同时生成 JSON 和原始 `.log`，并输出：

```text
RELEASE_TARGET_RUNTIME_OK platform=<platform> build_id=<build-id>
```

## Release PCK Host 迁移门禁

目标机验证通过后，使用同一审计报告和证据直接挂载 Release PCK，执行三玩家 Host 外部硬杀迁移：

```bash
bash tests/run_release_lan_host_migration_gate.sh \
  /absolute/release/release-audit.json \
  /absolute/release/target-runtime-evidence.json \
  /absolute/release/release-pack-lan-gate
```

门禁要求精确 Release 构建 marker、真实网络席位解析、原 Host 外部 `SIGKILL`、候选晋升、跟随者恢复、两个客户端退出码为 0、最终权威结果和私有视图指纹一致，并拒绝任何 `ERROR:`、`SCRIPT ERROR` 或资源加载阻断。输出 `release-pack-lan-gate.json` 绑定审计报告、目标机证据、EXE、PCK、harness 和逐 Peer 原始日志哈希。

该门禁证明审计绑定的玩家 PCK 可以完成迁移，但作用域仍是单设备回环，不能替代发布清单要求的双物理设备 LAN 场景。

当前本地基线为 `deckland-rc-20260801-host-migration-5`：macOS 目标机证据和 `release-pack-lan-gate-v8/release-pack-lan-gate.json` 均为 `passed`；Windows 原生目标机、Linux 原生目标机和三平台双实机 LAN 仍待外部验收。

执行责任固定如下：多用户 LAN、Steam 三账号与网络损伤矩阵由用户手动验收；Windows/Linux 原生 executable smoke 由对应目标设备执行。代理完成状态只覆盖仓库实现和当前机器可验证的自动门禁，不改变这些项目的发布阻断属性。

RC5 统一接力清单位于 `outputs/HOST_MIGRATION_RC5_RELEASE_EVIDENCE.pending.json`。当前结构校验命令为：

```bash
python3 tools/validate_host_migration_release_evidence.py \
  outputs/HOST_MIGRATION_RC5_RELEASE_EVIDENCE.pending.json \
  --allow-pending
```

该命令应输出五个 Steam、三个 LAN 和五个异常网络场景，共 13 个 pending 警告。外部证据全部填入后，删除候选文件名中的 `.pending` 并运行不带 `--allow-pending` 的同一校验器；任何 pending、缺失原始日志、设备标签不匹配或哈希漂移都必须阻断发布。

## 证据接入

每个平台的 `real_lan_runs` 条目必须引用这两个文件：

```json
{
  "id": "linux",
  "status": "passed",
  "release_audit": "release/linux/release-audit.json",
  "target_runtime_evidence": "runtime/linux/target-runtime.json"
}
```

`target-runtime.json` 的 `device_id` 必须等于该 LAN 条目的 `host_device_id` 或 `client_device_id`。最终校验会重新核对：

- Release 配置、网络协议和统一 build ID；
- release audit 文件 SHA-256；
- EXE/PCK 的审计哈希与目标机实际哈希；
- 目标操作系统和物理设备标签；
- 精确平台 marker、退出码、有限时长和原始日志哈希；
- 包审计、源完整性、pack harness 和所有阻断日志计数。

运行统一门槛：

```bash
python3 tools/validate_host_migration_release_evidence.py /absolute/evidence/manifest.json
```
