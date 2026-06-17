# Resource Baseline

Last updated: 2026-06-17

本基线只冻结本轮冲刺 Week 1 的资源范围，不扩展到其他非阻塞美术问题。

## Scope

当前只把以下反馈特效定义为基线资源：

- `res://art/fx/avatar_feedback_score/run-001/processed/fx-1.png` 到 `fx-4.png`
- `res://art/fx/avatar_feedback_hit/run-001/processed/fx-1.png` 到 `fx-4.png`
- `res://art/fx/avatar_feedback_victory/run-001/processed/fx-1.png` 到 `fx-6.png`
- `res://art/fx/avatar_feedback_defeat/run-001/processed/fx-1.png` 到 `fx-4.png`

这些组当前都满足：

- 从 `fx-1` 开始连续编号
- 没有中间断号
- 已能稳定播放

## Loader Contract

反馈特效加载现在按“从 `fx-1` 开始读取连续帧，遇到第一张不存在的帧就停止”执行。

约束如下：

- 不同反馈特效组允许不同帧数
- 尾部不存在的帧不再视为错误
- 资源探测先做磁盘存在判断，再做 Godot 资源加载，避免把合法的尾部缺帧打成 `ERROR`

对应实现：

- `scripts/CharacterAvatar.Frames.cs`
- `scripts/FrameAnimationHelpers.cs`

## Mandatory Checks

每次改动反馈特效或其加载逻辑，至少执行：

```sh
dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

## Regression Rules

以下任一情况都应直接视为新增回归：

- smoke 日志出现 `avatar_feedback_*` 的 `Resource file not found`
- smoke 日志出现 `avatar_feedback_*` 的 `Error loading resource`
- 反馈特效目录出现中间断号，例如 `fx-1`, `fx-2`, `fx-4`
- 替换 raw sheet 后没有重新导出连续命名的 processed 帧

`tests/Rules.Tests/Program.cs` 已固定一条资源回归检查：

- `avatar feedback effect resources stay contiguous`

## Current Baseline Result

本轮确认结果：

- `dotnet build pokegame.csproj` 通过
- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj` 通过
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh` 通过
- `avatar_feedback_*` 不再产生资源缺图错误

