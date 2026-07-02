# Adventure Upgrade Failure Path Hardening Plan

## Summary
将当前 adventure upgrade 发放流程改成“两阶段提交”：

- 第一阶段：生成 offer 并尝试展示 3 选 1。
- 第二阶段：只有在选择结果真正落地后，才把对应 trigger 标记为已发放。

如果 overlay 无法展示或初始化失败，采用自动兜底策略：从当前 offer 中自动选 1 个并完成发放，保证不挂流程、不吞奖励。

## Key Changes
- 调整 `ResolvePendingAdventureUpgradeTriggersInternalAsync()`：
  - 触发出队后，不立即调用 `MarkAdventureUpgradeTriggerAsGranted(...)`。
  - 先构建 offer。
  - 如果 offer 为空，直接跳过本次 trigger，不记账。
  - 调用升级选择流程，拿到显式结果后再决定是否发放与记账。
- 调整升级选择返回值：
  - 将 `ShowAdventureUpgradeSelectionAsync(...)` 从 `Task<int>` 改成明确结果类型，至少区分：
    - `Selected(index)`
    - `CanceledOrCleared`
    - `PresentationFailed`
  - 不再用 `-1` 同时表达“被清理”“展示失败”“无选择”三种语义。
- 调整 overlay 展示函数：
  - `ShowAdventureUpgradeSelectionOverlay(...)` 改成返回 `bool` 或结果对象。
  - 如果场景缺失、实例化失败、节点结构异常，直接向上返回 `PresentationFailed`，不能只 `PushError` 后静默 `return`。
- 引入展示失败兜底：
  - 当结果为 `PresentationFailed` 时，不再等待用户输入。
  - 使用当前 offer 自动选 1 个并发放，采用“取第一个已生成 option”的确定性策略。
  - 自动选择后立即：
    - `AdventureUpgradeRunState.AcquireUpgrade(selected.Id)`
    - `MarkAdventureUpgradeTriggerAsGranted(triggerSource)`
- 调整取消和清理路径：
  - `CancelAdventureUpgradeSelection()` 只表达“当前等待被终止”，不要再隐式等同于“玩家放弃奖励”。
  - `ClearSelectionOverlay()` 继续负责 UI 清理，但不再承担业务决策含义。
- 保持 victory 结算正确：
  - 对 `Victory` trigger，只有在奖励真正落地后才允许后续 `DeclareWinner(...)`。
  - 如果走自动兜底，也要先完成发放，再继续胜利结算。

## Internal API Changes
- `ShowAdventureUpgradeSelectionAsync(...)` 返回类型变更，不再返回裸 `int`。
- 新增一个内部结果类型，例如 `AdventureUpgradeSelectionResult`，包含：
  - `Status`
  - `SelectedIndex`
  - `WasAutoResolved`
- `ShowAdventureUpgradeSelectionOverlay(...)` 需要向调用方返回展示成功或失败的显式状态。

## Test Plan
- 保留并继续通过现有：
  - `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
  - `bash tests/run_adventure_upgrade_run_start_smoke.sh`
  - `bash tests/run_adventure_upgrade_big_capture_smoke.sh`
  - `bash tests/run_adventure_upgrade_victory_smoke.sh`
  - `bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh`
- 新增最小覆盖：
  - 选择 overlay 场景加载失败时，run-start trigger 仍能获得 1 个 upgrade，且 `ShouldGrantRunStartUpgrade` 被正确消费。
  - victory trigger 展示失败时，仍能自动获得 1 个 upgrade，随后正常进入 winner phase。
  - big-capture 和 victory 连续触发时，如果第一或第二次展示失败，顺序仍然稳定，最终获得对应数量的 upgrade。
  - 清理和取消路径不会导致 resolver 永久挂起。

## Acceptance Criteria
- 不再存在“奖励未发但 trigger 已消费”。
- 不再存在“overlay 没出来但异步一直等待”。
- victory 奖励失败兜底后，结果界面仍按原顺序出现。
- 自动兜底路径不引入额外随机性，结果可稳定复现。

## Assumptions
- overlay 展示失败属于异常兜底路径，不需要向玩家暴露额外提示。
- 自动兜底选择采用“当前 offer 的第一个 option”，优先保证确定性、稳定性和测试可写性，而不是额外引入随机或权重逻辑。
- `RunStart` 和 `Victory` 的“已发放”状态，语义上等同于“奖励已真正落地”，不是“已尝试展示过”。
