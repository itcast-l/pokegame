# Tracked Generated Asset Inventory

Last updated: 2026-07-15

本清单记录当前仓库内“已被 git 跟踪的生成/导出类资产”快照，以及最近一轮手工清理后的结果。

## Current Category Summary

- `upgrade_popup_processed`: `0`
  说明：正式卡图及对应 `.import` 已迁移到 `art/ui/adventure_upgrade_cards/*/processed/`
- `upgrade_popup_raw`: `0`
  说明：source-only raw 图已取消跟踪，仍保留在本地工作区
- `upgrade_popup_meta`: `0`
  说明：`transparent-processing-meta.json` 已取消跟踪，仍保留在本地工作区
- `upgrade_popup_root_concepts`: `0`
  说明：根目录概念图已取消跟踪，仍保留在本地工作区
- `board_top_preview`: `0`
  说明：vertical/backdrop 顶部延展预览已取消跟踪，仍保留在本地工作区
- `generated_misc`: `0`
  说明：其他样张、概念图、局部预览图已取消跟踪，仍保留在本地工作区
- `outputs`: `0`
  说明：当前没有被 git 跟踪的 `outputs/**`
- `debug_log`: `0`
  说明：`*.debug-sse.log` 已取消跟踪，仍保留在本地工作区

## Tracking Guidance

- `upgrade_popup_processed`
  结论：生成目录中保持为 `0`
  原因：正式 processed 资产已迁移到 `art/ui/adventure_upgrade_cards/` 并继续保留跟踪

- `upgrade_popup_raw`
  历史结论：候选取消跟踪
  当前状态：已取消跟踪，并由 `.gitignore` / `exclude_filter` 保护
  原因：source-only，不应进入导出包，也不应继续新增跟踪

- `upgrade_popup_meta`
  历史结论：候选取消跟踪
  当前状态：已取消跟踪，并由 `.gitignore` / `exclude_filter` 保护
  原因：处理流程记录，不属于运行时正式资产

- `upgrade_popup_root_concepts`
  历史结论：候选取消跟踪
  当前状态：已取消跟踪，并由 `.gitignore` / `exclude_filter` 保护
  原因：概念图，只用于设计/对照，不属于运行时正式资产

- `board_top_preview`
  历史结论：候选取消跟踪
  当前状态：已取消跟踪，并由 `.gitignore` / `exclude_filter` 保护
  原因：明显属于 authoring/debug preview 产物

- `generated_misc`
  历史结论：逐项审查后候选取消跟踪
  当前状态：已逐项审查并取消跟踪
  原因：大多属于样张、预览、设计稿，而不是运行时消费者

- `debug_log`
  历史结论：候选取消跟踪
  当前状态：已取消跟踪，并由 `.gitignore` / `exclude_filter` 保护
  原因：调试日志不应进入运行时包，也不应继续被跟踪

## How To Use This Inventory

当前快照说明：

1. 当前不再保留任何 `generated-images/**` 运行时正式资产；强化卡图在 `art/ui/adventure_upgrade_cards/` 中保留跟踪。
2. 其他生成/预览/source-only 资产已经取消跟踪，但仍保留在本地工作区，供设计对照或后续再生成使用。
3. 若未来审计脚本再次报出同类风险，可参考下方批次重新执行人工审查。

## Suggested Manual Cleanup Batches

以下内容保留为未来复发时的人工审查与手动执行建议。  
本轮清理已执行这些批次；如果后续再次出现同类已跟踪文件，仍应先 `review first`。

### Batch 1: board-top-extension-preview

- 范围：`board_top_preview`，当前 `0` 个已跟踪文件
- 说明：主要是 `generated-images/board-top-extension-preview/**` 下的 input、output、final-candidates 与 debug 产物
- 建议：这是最适合先清理的一批，因为语义最明确，且明显属于 authoring preview

先审查：

```bash
git ls-files -- 'generated-images/board-top-extension-preview/**'
```

手动取消跟踪命令预览（review first）：

```bash
git rm --cached -- <reviewed generated-images/board-top-extension-preview paths>
```

### Batch 2: debug log

- 范围：`debug_log`，当前 `0` 个已跟踪文件
- 说明：`art/maps/four_player_arena_prototype_v3.debug-sse.log`
- 建议：高确定性的非运行时文件，可以单独处理，风险最低

先审查：

```bash
git ls-files -- 'art/maps/*.debug-sse.log'
```

手动取消跟踪命令预览（review first）：

```bash
git rm --cached -- <reviewed debug log paths>
```

### Batch 3: upgrade popup raw

- 范围：`upgrade_popup_raw`，当前 `0` 个已跟踪文件
- 说明：`generated-images/upgrade-popup-assets/*/raw/*.png` 与对应 `.import`
- 建议：如果当前仓库不需要继续把 raw 图放在版本控制里，这一批可在 processed 资产确认无误后处理

先审查：

```bash
git ls-files -- 'generated-images/upgrade-popup-assets/*/raw/**'
```

手动取消跟踪命令预览（review first）：

```bash
git rm --cached -- <reviewed generated-images/upgrade-popup-assets raw paths>
```

### Batch 4: upgrade popup metadata and root concepts

- 范围：`upgrade_popup_meta` + `upgrade_popup_root_concepts`，当前 `0` 个已跟踪文件
- 说明：`transparent-processing-meta.json` 与 upgrade popup 根目录概念图/对应 `.import`
- 建议：这批通常不属于运行时正式资产，但应先确认是否仍需要作为设计对照留在仓库

先审查：

```bash
git ls-files -- \
  'generated-images/upgrade-popup-assets/transparent-processing-meta.json' \
  'generated-images/upgrade-popup-assets/*.png' \
  'generated-images/upgrade-popup-assets/*.png.import'
```

手动取消跟踪命令预览（review first）：

```bash
git rm --cached -- <reviewed upgrade popup meta and concept paths>
```

### Batch 5: generated misc

- 范围：`generated_misc`，当前 `0` 个已跟踪文件
- 说明：其他样张、概念图、局部预览图；混合度最高
- 建议：逐项 review first，不建议批量一把处理

先审查：

```bash
git ls-files -- 'generated-images/**'
```

手动取消跟踪命令预览（review first）：

```bash
git rm --cached -- <reviewed generated misc paths>
```

### Batch 0: do not touch formal adventure upgrade cards

- 范围：`art/ui/adventure_upgrade_cards/*/processed/`，共 12 张 PNG 与 12 个 `.import`
- 说明：这是当前允许进入运行时导出的 processed 正式资产，不属于 generated cleanup 范围
- 建议：不要把这批混入上面的手工清理命令

保护性审查：

```bash
git ls-files -- 'art/ui/adventure_upgrade_cards/*/processed/*.png' \
  'art/ui/adventure_upgrade_cards/*/processed/*.png.import'
```

## Relationship To Audit Script

运行：

```bash
bash tools/audit_export_resources.sh
```

会输出同一套分类摘要和风险项列表。  
本文件是当前快照的人工整理版；脚本输出是实时状态。
