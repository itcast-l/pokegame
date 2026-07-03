# RESOURCE EXPORT AUDIT

这个仓库提供一个只读的资源/导出审计命令：

```bash
bash tools/audit_export_resources.sh
```

用途：

- 快速发现被 git 跟踪的 `generated-images/**`、`outputs/**` 文件。
- 快速发现被 git 跟踪的 `*.debug-sse.log`。
- 快速发现被 git 跟踪的 raw/source 大图和源文件。
- 检查 `project.godot` autoload 是否指向 `export_presets.cfg` 的 `exclude_filter` 排除路径。
- 检查 `export_presets.cfg` 是否遗漏生成目录和 debug 目录排除规则。

行为约束：

- 脚本只报告风险，不自动删除、不自动修改。
- 脚本不会执行 `git rm`、`rm -rf` 或其他破坏性操作。
- 若发现风险项，脚本返回非零状态，便于在本地或 CI 中快速发现问题。
- 脚本也会检查 `.gitignore` 是否覆盖 upgrade popup 的 source-only 资产。

当前基线：

- 截至 `2026-07-02`，清理完成后的基线运行结果为 `no risk checks`。
- 当前允许继续被跟踪的生成资产只剩 `generated-images/upgrade-popup-assets/**/processed/*.png` 与对应 `.import`。

当前仓库的特殊说明：

- `generated-images/**` 不能整体排除，因为 `generated-images/upgrade-popup-assets/**/processed/*.png` 目前仍被运行时资源引用。
- `generated-images/upgrade-popup-assets/**/processed/*.png` 与对应 `.import` 当前属于运行时允许资产。
- `generated-images/upgrade-popup-assets/raw/**`、`generated-images/upgrade-popup-assets/transparent-processing-meta.json`、以及根目录概念图 PNG 属于 source-only 资产，应视为导出风险并被明确排除。
- 因此审计脚本会把 processed 目录视为 allowlist，把 preview/debug/input/output/final-candidates 与 source-only 资产视为风险，而不是盲目要求整个 `generated-images/**` 被导出排除。
- `.gitignore` 也应忽略这些 source-only 资产，避免后续继续误加入版本控制。

建议在以下场景运行：

- 修改 `project.godot` autoload。
- 修改 `export_presets.cfg`。
- 新增生成素材、导出对比图、调试日志。
- 调整运行时资源路径或图片资产目录结构。
