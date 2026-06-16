# Underwater Ambient Layer Image Migration Plan

Last updated: 2026-06-15

本文档描述 `UnderwaterAmbientLayer` 的局部图像素材迁移方案。当前目标不是直接实施，而是先形成一份可执行的、可安全回退的实施文档。所有设计都以“效果不理想时，能够完整切回原版本”为第一约束。

## 结论

本次方案必须采用“双路径并存”，禁止直接覆盖旧实现。

固定要求如下：

- 保留当前 `Procedural` 程序绘制路径，且其行为保持与当前版本一致。
- 新增 `ImageBased` 图像实例路径，只替换 `seagrass / bubbles / center caustics / center glints` 四类。
- 三个场景通过场景参数显式切换模式，不通过删代码切换。
- 如果效果不满意，只需要把场景参数切回 `Procedural`，即可安全、完整回退到原版本视觉。
- 在图片模式稳定验证前，不删除任何旧绘制逻辑。

这不是“最终清理方案”，而是“带完整回退保障的迁移方案”。

## 当前范围

本次覆盖：

- `scripts/UnderwaterAmbientLayer.cs`
- `scenes/main_menu_scene.tscn`
- `scenes/game_board_scene.tscn`
- `scenes/adventure_map_scene.tscn`

本次不覆盖：

- `scripts/MainMenuOceanDynamics.cs`
- `scripts/AdventureMapDynamicLayer.cs`
- `shaders/water_caustic_overlay.gdshader`
- `shaders/board_art_dynamics.gdshader`
- 其他 UI 装饰层或战斗特效系统

## 当前程序化内容

`UnderwaterAmbientLayer` 当前通过 `_Draw()` 绘制以下内容：

- `edge depth`
- `sunbeams`
- `seagrass`
- `water currents`
- `center caustics`
- `motes`
- `center glints`
- `bubbles`

其中本次只计划替换：

- `seagrass`
- `bubbles`
- `center caustics`
- `center glints`

以下四类明确保留程序绘制，不作图：

- `edge depth`
- `sunbeams`
- `water currents`
- `motes`

原因固定如下：

- 这四类更偏参数化氛围或遮罩，不值得引入更多素材和实例控制复杂度。
- 即使进入 `ImageBased` 模式，这四类也继续走当前 `_Draw()` 逻辑。

## 改造目标

将 `UnderwaterAmbientLayer` 改造成“局部图像实例层 + 局部程序绘制层”的混合结构，而不是整层全量贴图化。

固定目标：

- 图像提供四类美术内容本体。
- 代码继续负责实例数量、随机分布、漂移、alpha 脉冲、轻微缩放、帧推进和尺寸适配。
- 不使用整屏视频。
- 不给每个实例单独挂 `AnimationPlayer`。
- 主菜单、牌桌、冒险地图共用同一个脚本结构。
- 三个场景使用不同素材档位，不共用同一套环境图。
- 回退时必须不依赖 git 撤销，而是通过场景参数切换恢复旧版视觉。

## 实施策略

### 1. 双路径并存

给 `UnderwaterAmbientLayer` 新增两个导出枚举：

- `AmbientRenderMode`
  - `Procedural`
  - `ImageBased`
- `AmbientVisualProfile`
  - `MainMenu`
  - `GameBoard`
  - `AdventureMap`

固定默认值：

- `RenderMode = Procedural`
- `VisualProfile = GameBoard`

规则固定如下：

- 旧场景在不改 `.tscn` 的情况下，继续走原版程序绘制。
- 只有显式设成 `ImageBased` 的场景才进入新路径。
- 回退时只需要把场景中的 `RenderMode` 改回 `Procedural`。

### 2. 保留旧参数语义

以下现有导出字段全部保留，且不改语义：

- `bubble_count`
- `mote_count`
- `seagrass_blade_count`
- `motion_scale`
- `edge_depth_alpha_scale`
- `redraw_fps`
- `sunbeam_intensity`
- `center_caustic_intensity`
- `center_glint_count`

图片模式下这些字段的含义固定如下：

- `bubble_count`: 图片气泡实例数
- `seagrass_blade_count`: 图片海草实例数
- `center_glint_count`: 图片中心闪点实例数
- `center_caustic_intensity`: 图片中心焦散整体强度
- `motion_scale`: 统一时间推进倍率
- 其他保留原语义或继续服务程序绘制部分

这样可以避免破坏现有场景数据，并保证回退时参数仍然可直接复用。

### 3. 图片模式只新增四个子层

`ImageBased` 模式下，脚本运行时创建以下四个全屏子层：

- `SeagrassLayer`
- `BubbleLayer`
- `CenterCausticLayer`
- `CenterGlintLayer`

这些层全部由代码动态创建，不在 `.tscn` 中手工摆节点。

每个实例统一使用：

- `TextureRect`
- `StretchMode = KeepAspectCentered`
- `TextureFilter = Linear`

不为单个实例挂 `AnimationPlayer`，统一在 `_Process()` 中按 `_time + phase` 推进共享帧。

### 4. 程序绘制与图片模式的分工

`Procedural` 模式：

- `_Draw()` 继续执行当前 8 类绘制，行为等同原版。

`ImageBased` 模式：

- `_Draw()` 只继续画：
  - `edge depth`
  - `sunbeams`
  - `water currents`
  - `motes`
- 图片实例系统负责：
  - `seagrass`
  - `bubbles`
  - `center caustics`
  - `center glints`

要求实现上不要改写旧四类程序绘制算法，只在模式分支上决定是否调用，保证回退时视觉一致。

### 5. 场景档位配置方式

不把素材路径逐项散落到场景文件中，而是在代码中按 `VisualProfile` 绑定。

建议统一路径结构：

- `res://art/fx/environment/underwater_ambient/main_menu/seagrass/run-001/processed/fx-{0}.png`
- `res://art/fx/environment/underwater_ambient/main_menu/bubble/run-001/processed/fx-{0}.png`
- `res://art/fx/environment/underwater_ambient/main_menu/center_caustic/run-001/processed/fx-{0}.png`
- `res://art/fx/environment/underwater_ambient/main_menu/center_glint/run-001/processed/fx-{0}.png`

- `res://art/fx/environment/underwater_ambient/game_board/...`
- `res://art/fx/environment/underwater_ambient/adventure_map/...`

每个 profile 固定提供 4 组素材：

- `seagrass`
- `bubble`
- `center_caustic`
- `center_glint`

### 6. 建议素材规格

固定建议如下：

| 类别 | 帧数 | 画布 | 说明 |
| --- | --- | --- | --- |
| `seagrass` | 8 | 256x512 | 单株或小簇海草摆动，透明背景 |
| `bubble` | 8 | 128x128 | 单体气泡循环，透明背景 |
| `center_caustic` | 8 | 512x512 | 中心区域焦散漂移块，低存在感 |
| `center_glint` | 6 | 96x96 | 单体闪点，短 pulse |

所有素材都必须满足：

- 透明背景 PNG
- 线性过滤下边缘干净
- 无黑边白边
- 主体尽量居中
- 可被重复实例化，不依赖整屏拼接
- 不烘焙场景底图或 UI 内容

## 场景接入方案

固定接入如下：

- `scenes/main_menu_scene.tscn`
  - `RenderMode = ImageBased`
  - `VisualProfile = MainMenu`
- `scenes/game_board_scene.tscn`
  - `RenderMode = ImageBased`
  - `VisualProfile = GameBoard`
- `scenes/adventure_map_scene.tscn`
  - `RenderMode = ImageBased`
  - `VisualProfile = AdventureMap`

注意：

- 场景只切模式和档位，不删除脚本引用。
- 回退时只把这三个场景的 `RenderMode` 改回 `Procedural`。

## 回退策略

### 一级回退：场景级完整回退

如果图片效果不满意，直接把以下三个场景中的 `RenderMode` 全部切回 `Procedural`：

- `scenes/main_menu_scene.tscn`
- `scenes/game_board_scene.tscn`
- `scenes/adventure_map_scene.tscn`

这必须恢复到当前程序绘制版本的行为和视觉结构。

### 二级回退：代码级完整回退

本轮不删除以下旧逻辑：

- `DrawSeagrass()`
- `DrawBubbles()`
- `DrawCenterCaustics()`
- `DrawCenterGlints()`

即使图片模式存在问题，旧逻辑仍完整保留。

### 三级回退：资源缺失时的安全退化

如果 `ImageBased` 模式下某一类素材缺失：

- 仅打印 warning
- 该类自动退回程序绘制
- 不影响其他类别
- 不允许整层报错或失效

如果四类素材都缺失：

- `ImageBased` 视觉应退化成原版程序绘制组合
- 不能出现运行时异常

### 四级回退：版本控制回退

如需彻底撤销本轮，改动应集中在：

- `scripts/UnderwaterAmbientLayer.cs`
- 三个相关 `.tscn`
- `art/fx/environment/underwater_ambient/...`

这样便于整体回滚。

## 验证清单

### 功能验证

- 三个场景均可正常进入
- `Procedural` 模式下视觉与当前版本一致
- `ImageBased` 模式下四类图片内容都能正常显示
- 其余四类程序绘制内容仍正常显示

### 回退验证

- 任一场景切回 `Procedural` 后，无需其他改动即可恢复旧版视觉
- 三场景全部切回 `Procedural` 后，应视为“完整回退到原版本”

### 画质验证

- 深色背景下无黑边
- 高亮区域无白边
- 中心焦散不抢 UI
- 海草贴图不会明显重复穿帮
- 气泡边缘干净，低 alpha 不脏

### 布局验证

- 主菜单 1280x720 / 1920x1080 下正常
- 牌桌下不会遮挡关键牌面信息
- 冒险地图大尺寸视口下不会堆角或错位
- resize 后实例可重建

### 性能验证

- 图片模式不因大量实例挂独立动画器而抖动
- `bubble_count`、`seagrass_blade_count` 较高时仍可接受
- 主菜单设置中的 `ocean motion` 开关语义保持不变

## 不在本轮做的事

以下内容明确不在本轮：

- 删除旧程序绘制路径
- 改 `water_caustic_overlay.gdshader`
- 改 `board_art_dynamics.gdshader`
- 顺手统一 `MainMenuOceanDynamics` 和 `UnderwaterAmbientLayer` 的所有内部结构
- 调整 `AdventureMapDynamicLayer`
- 把环境层改成整屏视频或大序列播放器

## 实施顺序建议

1. 给 `UnderwaterAmbientLayer` 增加 `RenderMode` 和 `VisualProfile`
2. 保持 `Procedural` 默认，确保旧行为不变
3. 接入三档 profile 的素材路径规范
4. 完成四个图片子层与实例更新逻辑
5. 保留其余四类 `_Draw()` 路径不动
6. 三个场景显式切到 `ImageBased`
7. 做三场景视觉与回退验证
8. 只有在图片效果稳定后，才单独规划第二轮“删除旧逻辑”的清理方案

## Assumptions

- 当前阶段只生成文档，不直接实施。
- “完整回退到原版本”定义为：把场景参数切回 `Procedural` 后，恢复当前版本的程序绘制行为，而不是要求清空所有新代码。
- `MainMenuOceanDynamics` 已验证的双路径结构可以作为 `UnderwaterAmbientLayer` 的实现参考。
