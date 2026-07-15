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
- 正式强化卡图已经迁移到 `art/ui/adventure_upgrade_cards/**/processed/`；`generated-images/**` 不再允许承载运行时正式素材。

当前仓库的特殊说明：

- `generated-images/**` 可以作为 source-only 生成区整体排除；运行时正式素材必须进入 `art/`、`asset/`、`assets/` 或 `resources/`。
- `art/ui/adventure_upgrade_cards/**/processed/*.png` 与对应 `.import` 是当前强化卡图正式资产。
- `generated-images/upgrade-popup-assets/raw/**`、`generated-images/upgrade-popup-assets/transparent-processing-meta.json`、以及根目录概念图 PNG 属于 source-only 资产，应视为导出风险并被明确排除。
- 因此审计脚本会把任何被 git 跟踪的 `generated-images/**` 文件视为风险，不再保留运行时 allowlist。
- `.gitignore` 也应忽略这些 source-only 资产，避免后续继续误加入版本控制。

建议在以下场景运行：

- 修改 `project.godot` autoload。
- 修改 `export_presets.cfg`。
- 新增生成素材、导出对比图、调试日志。
- 调整运行时资源路径或图片资产目录结构。
