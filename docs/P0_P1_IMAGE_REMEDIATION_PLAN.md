# P0/P1 Image Remediation Plan

Last updated: 2026-06-16

本文件定义第一批图像化整改范围。目标是把当前项目中仍由代码运行时绘制、且暴露度高的动画/动态视觉，替换为图像素材驱动的实现，同时保留明确的回退路径。

本文件只覆盖第一批 `P0/P1` 项，不覆盖后续批次，也不覆盖本轮明确排除的 shader 和 UI 装饰项。

## Summary

本批次按以下范围执行：

- `P0`
  - `UnderwaterAmbientLayer` 中当前仍在运行的程序化 `sunbeams / water currents / motes / bubbles`
  - `AdventureMapDynamicLayer` 中的 `current ribbons / sparkles`
  - `AdventureMapRouteLayer` 的路线主体，改为烘焙进地图底图
- `P1`
  - `ScoreAvatarMeter` 的表盘主体图片化，进度仍由代码驱动
  - `CharacterAvatar` 的常驻底框/底盘图片化

本批次明确不做：

- `shaders/water_caustic_overlay.gdshader`
- `shaders/board_art_dynamics.gdshader`
- `scripts/MainMenuOceanDynamics.cs`
- 弹窗/按钮类 UI 装饰脚本
- `scripts/CircularSceneTransition.cs`

## Current State

当前项目中与本批次直接相关的实现如下：

- `scripts/UnderwaterAmbientLayer.cs`
  - 已有 `AmbientRenderMode { Procedural, ImageBased }`
  - 现有 `ImageBased` 只接管 `seagrass / center caustics / center glints`
  - 即使在 `ImageBased` 下，`DrawEdgeDepth`、`DrawSunbeams`、`DrawWaterCurrents`、`DrawMotes`、`DrawBubbles` 仍然继续程序化绘制
- `scripts/AdventureMapDynamicLayer.cs`
  - 目前只有 `_Draw()` 路径
  - 当前水流 ribbon 与 sparkle 全部是程序绘制
- `scripts/AdventureMapRouteLayer.cs`
  - 当前通过 `DrawPolyline` + `DrawCircle` 绘制路线和节点发光
  - 路线点位来自 `AvatarLayer` 中可见节点的中心位置
- `scripts/ScoreAvatarMeter.cs`
  - 当前通过 `_Draw()` 逐层绘制底盘、填充、液面线、边框和 pulse
  - 外部接口为 `Configure(Color fillColor, Color glowColor)` 和 `SetScore(int score, int maxScore, bool animate)`
- `scripts/CharacterAvatar.cs`
  - 动作反馈帧资源已经存在并接入：
    - `art/fx/avatar_feedback_score/run-001`
    - `art/fx/avatar_feedback_hit/run-001`
    - `art/fx/avatar_feedback_victory/run-001`
    - `art/fx/avatar_feedback_defeat/run-001`
  - 当前 `_Draw()` 中剩余的是常驻头像底框、底盘阴影和 accent 外环

## Asset Scope

### P0.1 Underwater Ambient Layer

新增四类图片动画，按 profile 拆分：

- `art/fx/environment/underwater_ambient/main_menu/sunbeam/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/main_menu/water_current/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/main_menu/mote/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/main_menu/bubble/run-001/processed/fx-{n}.png`

- `art/fx/environment/underwater_ambient/game_board/sunbeam/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/game_board/water_current/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/game_board/mote/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/game_board/bubble/run-001/processed/fx-{n}.png`

- `art/fx/environment/underwater_ambient/adventure_map/sunbeam/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/adventure_map/water_current/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/adventure_map/mote/run-001/processed/fx-{n}.png`
- `art/fx/environment/underwater_ambient/adventure_map/bubble/run-001/processed/fx-{n}.png`

固定规格：

- `sunbeam`
  - `asset_type=fx`
  - `action=hover`
  - `sheet=2x3`
  - `frames=6`
  - `art_style=project-native`
  - 画布建议 `384x720`
  - 透明背景，单束或小簇光柱，适合低 alpha 叠加
- `water_current`
  - `asset_type=fx`
  - `action=hover`
  - `sheet=2x4`
  - `frames=8`
  - `art_style=project-native`
  - 画布建议 `512x128`
  - 透明背景，带轻微流动弧线和柔和层次
- `mote`
  - `asset_type=fx`
  - `action=hover`
  - `sheet=2x3`
  - `frames=6`
  - `art_style=project-native`
  - 画布建议 `96x96`
  - 透明背景，小体积颗粒/星点，不带大 halo
- `bubble`
  - `asset_type=fx`
  - `action=hover`
  - `sheet=2x4`
  - `frames=8`
  - `art_style=project-native`
  - 画布建议 `128x128`
  - 透明背景，单体循环气泡，边缘干净

### P0.2 Adventure Map Dynamic Layer

新增两类图片动画：

- `art/fx/environment/adventure_map_dynamic/current_ribbon/run-001/processed/fx-{n}.png`
- `art/fx/environment/adventure_map_dynamic/sparkle/run-001/processed/fx-{n}.png`

固定规格：

- `current_ribbon`
  - `asset_type=fx`
  - `action=hover`
  - `sheet=2x4`
  - `frames=8`
  - `art_style=project-native`
  - 画布建议 `512x128`
- `sparkle`
  - `asset_type=fx`
  - `action=hover`
  - `sheet=2x3`
  - `frames=6`
  - `art_style=project-native`
  - 画布建议 `96x96`

### P0.3 Adventure Map Route

路线不走 AI 生图，不走 sprite sheet。

新增或替换以下底图资源：

- `art/maps/adventure_map_route_overlay_v1.png`
  - 透明 PNG
  - 包含路线主体和节点主体
- `art/maps/adventure_map_base_with_route_v1.png`
  - 由当前底图与路线 overlay 合成得到

固定要求：

- 路线必须严格对齐当前 `AvatarLayer` 节点中心
- 节点发光主体直接烘入，不依赖运行时圆点绘制
- 第一批不做多状态路线分支，不做动态高亮

### P1.1 Score Avatar Meter

新增四个 UI 资源：

- `art/ui/score_meter/run-001/processed/base.png`
- `art/ui/score_meter/run-001/processed/fill.png`
- `art/ui/score_meter/run-001/processed/over.png`
- `art/ui/score_meter/run-001/processed/pulse.png`

固定语义：

- `base.png`
  - 深色底盘和内圈阴影
- `fill.png`
  - 完整圆盘/圆环填充，用于运行时径向进度裁切
- `over.png`
  - 外圈 rim、细高光和顶部反光
- `pulse.png`
  - 仅用于得分时的额外 pulse 环

### P1.2 Character Avatar Frame

新增两个 UI 资源：

- `art/ui/avatar_frame/run-001/processed/base.png`
- `art/ui/avatar_frame/run-001/processed/accent.png`

固定语义：

- `base.png`
  - 中性底盘、内圈阴影、头像承托区域
- `accent.png`
  - 可由 `AccentColor` 调制的外圈高光

本批次不改现有动作反馈帧，不新增新的 `avatar_feedback_*` 资源。

## Image Generation Workflow

### Primary Workflow

所有 AI 图像生成统一优先使用 `$generate2dsprite`。

固定规则：

- 主生图链路必须符合 `generate2dsprite` 技能约束
- 原始生成图必须来自内置 `image_gen`
- 后处理统一使用技能目录下的 processor 做：
  - magenta cleanup
  - frame split
  - alignment
  - QC metadata
  - transparent sheet export
  - GIF export

每组资产都要产出：

- `raw-sheet.png`
- `raw-sheet-clean.png`
- `sheet-transparent.png`
- per-frame PNGs
- `animation.gif`
- `prompt-used.txt`
- `pipeline-meta.json`

### Fallback Workflow

如果内置 `image_gen` 不可用，则同一组资产改走 `$test-image`。

固定规则：

- `$test-image` 只负责替代 raw image 生成
- 提示词、尺寸、动作、sheet 规格与 `$generate2dsprite` 主流程保持完全一致
- 生成后的 raw 图仍进入 `$generate2dsprite` 的本地 processor 流程
- 不允许因为 fallback 改变资源目录结构、帧数或命名规则

### Prompt Policy

固定参考图来源：

- 主菜单风格参考：
  - `art/menu/main_menu_concept_10.png`
- 牌桌风格参考：
  - `art/maps/underwater_card_table_no_deck_pads_v1_1920x1080.png`
- 冒险地图风格参考：
  - 现有 `art/fx/environment/underwater_ambient/adventure_map/*`
  - 与 `adventure_map_scene` 的整体海洋地图风格保持一致

固定提示词约束：

- 背景必须是纯 `#FF00FF`
- 不得带 UI、文字、边框、网格
- 不得跨出 cell 边界
- 同一 sheet 内主体尺度必须稳定
- 环境类素材统一使用 `art_style=project-native`
- `sunbeam / water_current / mote / bubble / sparkle / ribbon` 都按 `fx` 资产处理

## Implementation Changes

### UnderwaterAmbientLayer

修改 `scripts/UnderwaterAmbientLayer.cs`：

- 保留现有 `AmbientRenderMode` 与 `AmbientVisualProfile`
- 在 `ImageBased` 路径下新增四个 layer：
  - `SunbeamLayer`
  - `WaterCurrentLayer`
  - `MoteLayer`
  - `BubbleLayer`
- 新增 profile 资源加载函数：
  - `sunbeam`
  - `water_current`
  - `mote`
  - `bubble`
- `ImageBased` 且对应资源存在时：
  - 不再调用 `DrawSunbeams()`
  - 不再调用 `DrawWaterCurrents()`
  - 不再调用 `DrawMotes()`
  - 不再调用 `DrawBubbles()`
- `edge depth` 继续保留程序绘制
- 对于单类资源缺失，只对该类回退到旧 `_Draw()` 逻辑，不影响其它已图片化类别

场景配置保持 `ImageBased`：

- `scenes/main_menu_scene.tscn`
- `scenes/game_board_scene.tscn`
- `scenes/adventure_map_scene.tscn`

### AdventureMapDynamicLayer

修改 `scripts/AdventureMapDynamicLayer.cs`：

- 新增 `RenderMode { Procedural, ImageBased }`
- 默认值设为 `Procedural`
- `ImageBased` 下动态创建：
  - `CurrentRibbonLayer`
  - `SparkleLayer`
- 帧推进方式采用共享帧数组 + 实例 `phase`
- 漂移、alpha 脉冲、位置扰动继续由代码控制
- 资源存在时不再调用旧 `DrawCurrentRibbons()` / `DrawSparkles()`
- 资源缺失时整体回退到 `Procedural`

修改 `scenes/adventure_map_scene.tscn`：

- 给 `MapDynamicLayer` 显式配置 `RenderMode = ImageBased`

### AdventureMapRouteLayer

修改 `scripts/AdventureMapRouteLayer.cs`：

- 不删除原实现
- 保留现有 `_Draw()` 作为回退路径

修改 `scenes/adventure_map_scene.tscn`：

- `MapBaseTexturePath` 改为 `art/maps/adventure_map_base_with_route_v1.png`
- `RouteLayer` 默认 `visible = false`

固定回退方式：

- 恢复旧底图
- 把 `RouteLayer` 改回可见

### ScoreAvatarMeter

修改 `scripts/ScoreAvatarMeter.cs`：

- 保留公有接口：
  - `Configure(Color fillColor, Color glowColor)`
  - `SetScore(int score, int maxScore, bool animate)`
- 新增导出开关：
  - `UseTexturedMeter = true`
- 新增节点结构：
  - `TextureRect BaseTexture`
  - `TextureProgressBar FillProgress`
  - `TextureRect OverTexture`
  - `TextureRect PulseTexture`
- `FillProgress` 使用径向或等效进度显示，不再逐线画液面
- `PulseTexture` 只在得分增长时播放缩放 + alpha Tween
- 当资源缺失或 `UseTexturedMeter = false` 时，继续走旧 `_Draw()` 路径

### CharacterAvatar

修改 `scripts/CharacterAvatar.cs` 与相关布局代码：

- 保留现有：
  - 动作帧
  - `AttackFx`
  - `FeedbackFx`
- 新增导出开关：
  - `UseTexturedFrame = true`
- 新增两个 `TextureRect`：
  - `FrameBase`
  - `FrameAccent`
- `FrameBase` 放在头像后方
- `FrameAccent` 始终跟随 `AccentColor` 调制
- 当资源缺失或 `UseTexturedFrame = false` 时，继续使用当前 `_Draw()` 绘制的底框/底盘逻辑

## Acceptance Criteria

### P0 Acceptance

- 主菜单、牌桌、冒险地图中的 `UnderwaterAmbientLayer`：
  - `sunbeams / water currents / motes / bubbles` 在资源齐全时全部来自图片实例
  - 不再依赖对应 `_Draw()` 动态成像
- `AdventureMapDynamicLayer`：
  - `current ribbons / sparkles` 全部来自图片实例
  - 旧 `_Draw()` 只作为回退逻辑存在
- `AdventureMapRouteLayer`：
  - 正常路径下路线主体由底图承载
  - 禁用 `RouteLayer` 后视觉仍完整
  - 头像节点位置与路线节点视觉中心不发生对齐偏差

### P1 Acceptance

- `ScoreAvatarMeter`
  - `0 -> max`
  - `max -> 0`
  - 得分增长 pulse
  - 冒险模式减血
  以上场景都保持接口不变、动画连续、不闪烁、不锯齿
- `CharacterAvatar`
  - 主菜单头像入口
  - 冒险地图头像
  - 解锁弹窗头像
  三处都显示统一底框
  - `AccentColor` 变化正确反映到外环
  - 现有 `score/hit/victory/defeat` 反馈帧继续正常播放

### Regression Criteria

- 不修改任何现有外部调用签名
- 不改变 `PlayAction()`、`PlayAttackFx()`、`PlayFeedbackFx()`、`SetScore()` 的使用方式
- `Procedural` 模式仍可完整恢复旧表现

## Rollback

固定回退策略如下：

- `UnderwaterAmbientLayer`
  - 单类资源缺失：仅该类回退程序绘制
  - 全量回退：场景切回旧资源集或代码开关关闭
- `AdventureMapDynamicLayer`
  - 把 `RenderMode` 改回 `Procedural`
- `AdventureMapRoute`
  - 恢复旧底图
  - 把 `RouteLayer` 改回可见
- `ScoreAvatarMeter`
  - `UseTexturedMeter = false`
- `CharacterAvatar`
  - `UseTexturedFrame = false`

本批次所有回退都不依赖 git 撤销，必须能通过资源或导出开关恢复。

## Assumptions

- 第一批以你确认的口径为准，不采用 `docs/ANIMATION_IMAGE_REPLACEMENT_PLAN.md` 中那套战斗 FX `P0/P1`
- `water_caustic_overlay.gdshader` 与 `board_art_dynamics.gdshader` 暂不进入本批
- `CharacterAvatar` 的反馈帧资源已经足够，不需要第一批继续补动作反馈图
- `AdventureMap` 路线拓扑当前可视为固定，不需要第一批做多状态路线图层
