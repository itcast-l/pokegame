# Adventure Upgrade Visual Resource Baseline

Last updated: 2026-07-01

本基线只冻结 adventure upgrade 弹窗卡面资源治理这一轮，不扩展到其他 UI 或战斗美术问题。

## Scope

当前纳入治理范围的资源与消费者如下：

- `res://generated-images/upgrade-popup-assets/<suit>/processed/*.png` 这 12 张已处理卡面 PNG
- `res://scenes/adventure_upgrade_selection_overlay.tscn` 运行时三选一强化覆盖层
- `res://scenes/upgrade_choice_popup_asset_scene.tscn` 编辑器预览场景
- `res://scripts/GameBoardScene.AdventureUpgrades.cs` 运行时强化选择逻辑
- `res://scripts/UpgradeChoicePopupPreview.gd` 编辑器预览绑定逻辑

## Current Failure Signature

初始失败特征已完成治理，当前保留为回归对照：

- 历史上 `scenes/upgrade_choice_popup_asset_scene.tscn` 一度膨胀到约 `262089921` bytes
- 历史上该场景曾包含 12 个 `Image` subresource
- 历史上该场景曾包含 12 个 `ImageTexture` subresource
- 历史上该场景曾包含 12 处 `PackedByteArray(...)`
- 这些内嵌像素负载现已移除，任何重现都应直接视为回归

## Current Affected Files

- `scenes/upgrade_choice_popup_asset_scene.tscn`
- `scripts/UpgradeChoicePopupPreview.gd`
- `scenes/adventure_upgrade_selection_overlay.tscn`
- `scripts/GameBoardScene.AdventureUpgrades.cs`
- `generated-images/upgrade-popup-assets/transparent-processing-meta.json`

额外确认：

- 运行时 overlay 已经使用外部 `Texture2D` / `AtlasTexture` 作为卡面引用模式
- `generated-images/upgrade-popup-assets/raw/` 当前不被运行时代码直接引用
- raw 素材现在只被 `.import` 文件和 `transparent-processing-meta.json` 间接关联

## Mandatory Commands

每次修改 adventure upgrade 卡面资源治理逻辑，至少执行：

```sh
dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
python3 tools/audit_adventure_upgrade_visual_resources.py
wc -c scenes/upgrade_choice_popup_asset_scene.tscn
```

## Regression Rules

以下任一情况都应直接视为回归：

- `upgrade_choice_popup_asset_scene.tscn` 再次出现内嵌 `Image` / `ImageTexture` / `PackedByteArray`
- 新的 adventure upgrade 预览场景把卡面像素直接序列化进 `.tscn`
- 任意 project-owned `.tscn` / `.tres` 再次出现 built-in `Image` subresource
- 任意 project-owned `.tscn` / `.tres` 再次出现 built-in `ImageTexture` subresource
- 任意 popup/editor scene 重新把卡面像素序列化进 `PackedByteArray(...)`
- `upgrade_choice_popup_asset_scene.tscn` 文件体积显著异常增长，超出 repo 审计阈值
- 运行时 overlay 不再从外部 processed PNG 解析卡面
- 新增卡面资源绕开统一 catalog，重新回到脚本硬编码路径表
- raw 目录资源被运行时代码直接加载作为正式卡面

## Success Thresholds After Migration

迁移完成后的实测结果：

- popup 预览场景不再包含 built-in card image payload
- `scenes/upgrade_choice_popup_asset_scene.tscn` 当前体积为 `11608` bytes
- popup 场景当前 `Image` / `ImageTexture` / `PackedByteArray(...)` 计数均为 `0`
- runtime overlay 与 editor preview 现在都通过同一份 catalog 元数据解析卡面
- 12 张 processed 卡面 PNG 仍全部可用，且 suit / tier / region 元数据可审计
- 后续新增或替换卡面时，可通过审计脚本、rules 测试与 smoke 提前拦截回归

## Current Baseline Result

本轮最终确认结果：

- `python3 tools/audit_adventure_upgrade_visual_resources.py` 通过
- `bash tests/run_adventure_upgrade_visual_resource_audit.sh` 通过
- `dotnet build pokegame.csproj` 通过
- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj` 通过
- `bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh` 通过
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh` 通过
- `bash tools/export_preflight.sh` 通过
