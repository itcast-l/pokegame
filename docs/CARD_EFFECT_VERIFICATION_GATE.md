# Card Effect Verification Gate

Last updated: 2026-06-17

本文件定义新增或修改卡效前必须补的最低验证。实现者不需要再自己判断“要不要跑联机回归”。

## Always Required

任何卡效改动，无论大小，至少执行：

```sh
dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

## Gate By Change Type

### 1. 纯规则 / 纯数据

范围：

- 只改规则判断
- 只改分数或条件
- 不改同步字段
- 不改视觉事件
- 不改 pending selection / reconnect

必须执行：

- `dotnet build pokegame.csproj`
- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`

### 2. 桌面结构、公开牌流、牌堆数量变化

范围：

- 桌面牌增删改
- 牌堆 / 手牌数量变化
- table overlay / capture / destroy / shuffle / draw

必须执行：

- build
- `Rules.Tests`
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh`

### 3. pending selection / 目标选择 / 手牌暴露

范围：

- `TargetPlayer`
- `TargetHandCard`
- `HandLimitDiscard`
- `MagnetHandCard`
- 任意会临时暴露目标手牌的改动

必须执行：

- build
- `Rules.Tests`
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh`

重点关注 smoke 子项：

- `lan pending discard smoke`
- `lan pending target smoke`
- `lan pending discard fallback smoke`
- `lan pending target fallback smoke`

### 4. reconnect / reclaim / seat control / snapshot schema

范围：

- reconnect 规则
- reclaim 流程
- seat control 状态迁移
- snapshot 内容或应用顺序
- 视觉队列与快照的先后关系

必须执行：

- build
- `Rules.Tests`
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh`

重点关注 smoke 子项：

- `lan reconnect smoke`
- `lan own-turn reconnect smoke`
- `lan reconnect during visual smoke`
- `lan reclaim pending smoke`
- `lan no-reconnect until match end smoke`
- `lan three-player disconnect smoke`

如果改动涉及 reconnect 的视觉体验，还应补人工检查：

```sh
bash tests/run_lan_reconnect_during_visual_manual.sh
```

### 5. Steam transport / Steam reconnect 路径

范围：

- Steam lobby
- Steam join / invite / reconnect
- Steam owner-migration 防御
- Steam-specific reconnect claim

必须执行：

- build
- `Rules.Tests`
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh`
- 按 [STEAM_RECONNECT_REGRESSION.md](./STEAM_RECONNECT_REGRESSION.md) 完成双环境手工联调

## Existing Smoke Coverage

当前 `tests/run_multiplayer_reconnect_smoke_suite.sh` 已覆盖：

- scene smoke
- game board mode regression smoke
- lan basic turn sync smoke
- lan reconnect smoke
- lan own-turn reconnect smoke
- lan reconnect during visual smoke
- lan reclaim pending smoke
- lan host ended match smoke
- lan no-reconnect until match end smoke
- lan three-player disconnect smoke
- lan pending discard smoke
- lan pending target smoke
- lan pending discard fallback smoke
- lan pending target fallback smoke

## PR / Commit Checklist

提交卡效改动前，至少自问：

1. 这次改动属于哪一类行为？
2. 是否改变了任何快照字段、pending selection 字段、或手牌可见性？
3. 是否新增了只能靠视觉事件表达的状态？
4. 是否会影响 reconnect 后恢复？

如果任一答案为“会”，就不能只跑 `Rules.Tests`。

