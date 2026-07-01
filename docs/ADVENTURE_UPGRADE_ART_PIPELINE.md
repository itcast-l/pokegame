# Adventure Upgrade Art Pipeline

Last updated: 2026-07-01

## Purpose

本文档定义 adventure upgrade 卡面资源的唯一治理路径，确保：

- 运行时和编辑器预览都从同一份视觉元数据读取卡面
- 运行时代码只消费 processed 贴图
- 场景文件永远不再序列化卡面像素字节

## Directory Layout

- `generated-images/upgrade-popup-assets/<suit>/raw/`
  说明：源图，仅供处理流程与人工校对使用
- `generated-images/upgrade-popup-assets/<suit>/processed/`
  说明：运行时允许引用的正式 PNG
- `generated-images/upgrade-popup-assets/transparent-processing-meta.json`
  说明：raw -> processed 的处理记录
- `resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres`
  说明：12 张卡面的 suit / tier / texture path / crop region 元数据

## Runtime-Allowed Assets

运行时代码、运行时资源、运行时场景只允许引用：

- `res://generated-images/upgrade-popup-assets/.../processed/*.png`
- `res://resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres`

当前正式消费者：

- `scripts/AdventureUpgradeCardVisualResolver.cs`
- `scripts/GameBoardScene.AdventureUpgrades.cs`
- `scripts/UpgradeChoicePopupPreview.gd`

## Source-Only Assets

本轮采用 Strategy A：

- `raw/` 保留在仓库内
- 但 `raw/` 不得被 gameplay code、gameplay scenes、resource catalog 直接引用

允许 `raw/` 的位置：

- 处理脚本
- 处理记录 JSON
- 美术工作流文档

## How Crop Regions Are Governed

卡面 crop region 的唯一治理源为：

- `resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres`

含义：

- suit / tier 到 processed PNG 的映射在此定义
- `Rect2(x, y, width, height)` 的裁切区域也只在此定义
- runtime resolver 和 editor preview 都必须从这里读取，而不是各自复制一份 path map 或 switch table

## How To Add A New Suit/Tier Visual

1. 把新源图放入对应 `raw/` 目录
2. 生成或替换对应 `processed/*.png`
3. 更新 `transparent-processing-meta.json`
4. 在 `adventure_upgrade_card_visual_catalog.tres` 中新增或更新对应条目
5. 运行：

```sh
python3 tools/audit_adventure_upgrade_visual_resources.py
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

6. 如有预览或运行时 smoke，继续执行：

```sh
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
"${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}" --headless --path . --script res://tests/UpgradeChoicePopupAssetSceneVisualAudit.gd
```

## What Must Be Updated If A Processed PNG Is Replaced

替换已有 processed PNG 时，至少同时更新：

- 对应 processed 文件本身
- `transparent-processing-meta.json` 的 source/output 记录
- 如裁切边界变化，更新 `adventure_upgrade_card_visual_catalog.tres` 中对应 `Region`

如果只是像素内容变更、裁切未变：

- 不需要改 runtime 代码
- 不需要改 popup scene 布局

## What Must Never Be Serialized Into Scenes

以下内容永远不应再写入项目 owned `.tscn` / `.tres`：

- built-in `Image` subresource
- built-in `ImageTexture` subresource
- 大型 `PackedByteArray(...)` 卡面像素数据

特别说明：

- 预览场景必须绑定 imported textures 或运行时生成的 `AtlasTexture`
- 不允许把 generated image bytes 直接烘进 scene 文件
- `upgrade_choice_popup_asset_scene.tscn` 现在是治理基准样例，后续 popup/editor scenes 也必须遵守相同规则
