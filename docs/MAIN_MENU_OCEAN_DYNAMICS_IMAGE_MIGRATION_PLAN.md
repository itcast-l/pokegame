# Main Menu Ocean Dynamics Image Migration Plan

Last updated: 2026-06-15

本文档描述 `MainMenuOceanDynamics` 从代码绘制动态层迁移到图像素材动态层的实施方案。目标是把主菜单和多人大厅中当前由 `_Draw()` 生成的环境动态视觉，替换成可复用的透明 PNG 帧素材，同时保留可快速回退的旧实现，避免本次调整效果不理想时难以恢复。

## 结论

本次改造不直接删除旧实现，而是采用“双路径并存”的方式：

- 保留当前 `Procedural` 程序绘制模式。
- 新增 `ImageBased` 图像实例模式。
- 主菜单和多人大厅通过场景参数显式切换使用哪一种模式。
- 如果新效果不满意，只需要把场景参数切回旧模式，即可立即还原。

这种方式的优点是：

- 回退成本低。
- 不需要依赖 git 撤销才能恢复旧效果。
- 能先稳定验证素材质量、密度、层级和性能，再决定是否删除旧逻辑。

## 当前范围

本次只覆盖：

- `scripts/MainMenuOceanDynamics.cs`
- `scenes/main_menu_scene.tscn`
- `scenes/multiplayer_lobby_scene.tscn`

本次不覆盖：

- `scripts/UnderwaterAmbientLayer.cs`
- `shaders/water_caustic_overlay.gdshader`
- `shaders/board_art_dynamics.gdshader`
- 其他 UI 装饰层或环境层

## 当前程序化内容

`MainMenuOceanDynamics` 当前通过 `_Draw()` 实时绘制以下内容：

- sunbeams
- surface wake
- current ribbons
- caustic ribbons
- bubble streams
- glints

这些内容目前依赖：

- `_Process()` 中推进 `_time`
- `NotificationResized` 时重新构建布局参数
- `_Draw()` 中使用 `DrawColoredPolygon`、`DrawPolyline`、`DrawCircle`、`DrawArc` 实时生成画面

当前脚本还被以下两个场景共用：

- `scenes/main_menu_scene.tscn`
- `scenes/multiplayer_lobby_scene.tscn`

## 改造目标

将 `MainMenuOceanDynamics` 改造成“环境贴图实例层”，而不是“整屏序列播放器”。

具体目标：

- 用小块循环透明 PNG 替代 6 类程序绘制内容。
- 代码继续负责：
  - 实例生成
  - 随机排布
  - 漂移
  - alpha 脉冲
  - 帧推进
- 不使用整屏大尺寸序列。
- 不给每个实例单独挂 `AnimationPlayer`。
- 主菜单与多人大厅继续共用脚本和同一套素材。
- `ocean motion` 开关行为保持和当前一致：
  - 开启时显示动态层
  - 关闭时整层隐藏

## 实施策略

### 1. 双路径保留，不直接替换旧逻辑

给 `MainMenuOceanDynamics` 新增导出枚举：

- `Procedural`
- `ImageBased`

建议新增导出属性：

- `RenderMode`

规则固定如下：

- 默认值为 `Procedural`
- 未配置的场景继续使用旧逻辑
- 主菜单和多人大厅场景显式设置为 `ImageBased`

这样可以保证：

- 旧逻辑始终可运行
- 回退时只需要改场景参数
- 本轮不做删除旧逻辑的清理

### 2. 保留现有导出字段含义

以下字段继续保留，不改语义：

- `bubble_count`
- `glint_count`
- `motion_scale`

以下字段保留但在图片模式中不再实际参与绘制节流：

- `redraw_fps`

原因：

- 避免破坏现有场景数据
- 避免 `.tscn` 中已有导出值失效或报错
- 降低改造面

### 3. 图片模式的节点组织

`ImageBased` 模式下，`MainMenuOceanDynamics` 在 `_Ready()` 中动态创建以下 6 个子容器：

- `SunbeamLayer`
- `SurfaceWakeLayer`
- `CurrentRibbonLayer`
- `CausticRibbonLayer`
- `BubbleLayer`
- `GlintLayer`

每个子容器都是全屏 `Control`，仅负责容纳对应类别的实例。

每个实例统一使用：

- `TextureRect`
- `StretchMode = KeepAspectCentered`
- `TextureFilter = Linear`

不在场景里手工摆节点，全部由脚本创建，避免场景文件膨胀。

### 4. 帧播放方式

所有图片实例共享对应类别的帧数组。

不为每个实例挂独立 `AnimationPlayer`，因为：

- 气泡和闪点实例数很多
- 独立播放器会增加节点和运行时开销
- 当前项目已有 `FrameAnimationHelpers`，但这里更适合手动按时间算帧索引

建议帧推进逻辑：

- 在 `_Process()` 中推进 `_time`
- 每个实例有自己的 `phase`
- 用 `(time * fps + phaseOffset)` 计算当前帧索引
- 将帧索引映射到共享 `Texture2D[]`

这样能保证：

- 视觉上不同步，不会所有实例同时跳帧
- 不需要创建很多动画对象
- 行为更接近当前程序化随机动态感

### 5. 仍由代码负责的内容

本次替图并不意味着完全取消代码控制。以下逻辑继续由代码负责：

- 实例生成数量
- 列分布
- 随机偏移
- 漂移速度
- alpha 脉冲
- 轻微缩放
- Y 层分布
- 不同场景尺寸下的重排

也就是说：

- “视觉内容”来自图片
- “动态组织方式”仍由代码控制

## 素材方案

### 素材加载方式

统一使用：

- `FrameAnimationHelpers.LoadTextureFrames(..., preferImageFile: true)`

命名规则统一为：

- `processed/fx-1.png`
- `processed/fx-2.png`
- ...

不使用 GIF，不使用整屏视频。

### 建议素材路径与规格

| 类别 | 建议路径 | 帧数 | 画布 | 用途 |
| --- | --- | --- | --- | --- |
| sunbeams | `res://art/fx/environment/main_menu_sunbeam/run-001/processed/fx-{0}.png` | 6 | 384x720 | 顶部光束 |
| surface wake | `res://art/fx/environment/main_menu_surface_wake/run-001/processed/fx-{0}.png` | 8 | 640x96 | 水面波纹条 |
| current ribbons | `res://art/fx/environment/main_menu_current_ribbon/run-001/processed/fx-{0}.png` | 8 | 512x128 | 中层水流带 |
| caustic ribbons | `res://art/fx/environment/main_menu_caustic_ribbon/run-001/processed/fx-{0}.png` | 8 | 512x128 | 下层焦散带 |
| bubbles | `res://art/fx/environment/main_menu_bubble/run-001/processed/fx-{0}.png` | 8 | 128x128 | 单体气泡 |
| glints | `res://art/fx/environment/main_menu_glint/run-001/processed/fx-{0}.png` | 6 | 96x96 | 单体闪点 |

### 素材要求

所有环境素材都必须满足：

- 透明背景 PNG
- 不带烘焙底色
- 不带黑边、白边
- 线性过滤下边缘干净
- 主体尽量居中
- 小块循环素材可重复实例化，不依赖整屏拼接
- 不在素材中烘焙主菜单文字、按钮或背景底图

## 运行时映射规则

### 1. Sunbeams

图片模式下创建 5 个 `sunbeam` 实例，对应旧逻辑中的 5 条光束。

代码仍负责：

- 横向位置漂移
- alpha 轻微脉动
- 宽度缩放
- 分层排列

### 2. Surface Wake

图片模式下创建 6 个 `surface wake` 实例，对应旧逻辑中的 6 条波纹条。

代码仍负责：

- Y 位置分层
- 横向轻微偏移
- alpha 递减
- 小幅上下摆动

### 3. Current Ribbons

图片模式下创建 8 个 `current ribbon` 实例，对应旧逻辑中的 8 条水流带。

代码仍负责：

- 从左向右漂移
- 轻微纵向波动
- alpha 轻微起伏
- 纵向层次分布

### 4. Caustic Ribbons

图片模式下创建 7 个 `caustic ribbon` 实例，对应旧逻辑中的 7 条焦散带。

代码仍负责：

- 纵向分层
- 轻微横向或局部漂移
- 比 current ribbons 更弱的 alpha
- 更低存在感，避免喧宾夺主

### 5. Bubbles

图片模式下实例数继续使用 `bubble_count`。

代码仍保留：

- 当前列分布逻辑
- 上浮速度
- 横向摆动
- alpha 波动
- 不同大小和漂移参数
- 超出上边界后循环回到底部

### 6. Glints

图片模式下实例数继续使用 `glint_count`。

代码仍保留：

- pulse 闪烁
- 小幅漂移
- 尺寸变化
- 相位错开
- 低密度点缀逻辑

## 场景接入方案

### `scenes/main_menu_scene.tscn`

将 `OceanDynamicsLayer` 保留为当前脚本，但增加：

- `RenderMode = ImageBased`

其他现有导出值：

- `bubble_count`
- `glint_count`

先保持不变，避免本次同时改参数和改结构。

### `scenes/multiplayer_lobby_scene.tscn`

同样保留当前脚本，并设置：

- `RenderMode = ImageBased`

这样主菜单和大厅继续共用一套新实现。

## 设置系统兼容要求

当前 `ocean motion` 的行为已经存在于主菜单设置逻辑中，必须保持不变。

要求：

- 继续通过 `MainMenuScene.ApplySettings()` 控制该层显隐和处理状态
- 图片模式下不新增新的设置项
- 不改变 `GameSaveStore.OceanMotionKey`
- 关闭 `ocean motion` 时：
  - `OceanDynamicsLayer.Visible = false`
  - `OceanDynamicsLayer.SetProcess(false)`

即：

- 不是停在第一帧
- 不是保留静态装饰
- 而是继续保持当前“整层隐藏”的语义

## 容错策略

如果某一类素材缺失：

- 仅打印 warning
- 跳过该类别实例创建
- 不影响其他类别
- 不导致整个层失效

如果 `RenderMode = ImageBased` 但所有素材都缺失：

- 层应保持可运行但视觉为空
- 不应抛出运行时错误

## 回退方案

### 一级回退：场景级快速回退

如果图片效果不满意，直接把以下两个场景中的 `RenderMode` 改回 `Procedural`：

- `scenes/main_menu_scene.tscn`
- `scenes/multiplayer_lobby_scene.tscn`

这样可以：

- 立刻恢复旧视觉
- 不需要删除新素材
- 不需要撤销脚本主体改造

### 二级回退：代码级回退

本轮不删除旧 `_Draw()` 路径，所以即使图片模式出问题，旧逻辑仍可用。

### 三级回退：版本控制回退

如果需要彻底撤销本轮改造，git 改动应尽量集中在：

- `scripts/MainMenuOceanDynamics.cs`
- `scenes/main_menu_scene.tscn`
- `scenes/multiplayer_lobby_scene.tscn`
- `art/fx/environment/main_menu_*`

这样便于整体回滚。

## 不在本轮做的事

以下内容明确不在本轮执行范围内：

- 删除 `MainMenuOceanDynamics` 的旧程序绘制逻辑
- 顺手重构主菜单设置系统
- 调整 `UnderwaterAmbientLayer`
- 调整 `WaterCausticOverlay`
- 调整 `MainMenuBackgroundAnimator`
- 为主菜单和大厅拆分两套不同素材
- 把环境层改成整屏视频或超大序列

## 验证清单

### 功能验证

- 主菜单可以正常进入
- 多人大厅可以正常进入
- `RenderMode = Procedural` 时，视觉与旧版本一致
- `RenderMode = ImageBased` 时，6 类视觉都正常显示

### 设置验证

- `ocean motion` 开启时环境层正常播放
- `ocean motion` 关闭时环境层整层隐藏
- 切回开启后可以继续正常播放

### 布局验证

- 1280x720 下位置正常
- 1920x1080 下位置正常
- 全屏切换后不会错位
- 窗口拉伸后不会堆到左上角
- 不会出现明显裁切

### 画质验证

- 深色背景下无黑边
- 浅色高亮区域无白边
- 透明边缘干净
- 低 alpha 时没有脏边
- 多实例同时存在时不会显得贴图重复感过重

### 性能验证

- 主菜单保持稳定
- 多人大厅保持稳定
- 气泡和闪点高实例数下没有明显卡顿
- 不因给每个实例挂 `AnimationPlayer` 而增加节点和抖动

### 回退验证

- 场景切回 `Procedural` 后，无需其他改动即可恢复旧效果
- 图片素材保留但不影响旧模式运行

## 后续建议

如果图片模式验证通过，再考虑第二阶段清理：

- 删除旧 `_Draw()` 路径
- 删除旧参数结构中仅服务程序绘制的部分
- 收敛 `MainMenuOceanDynamics` 为纯图片实例层

但这一步必须单独提交，不与本次改造混在一起。

## 实施顺序建议

1. 先给 `MainMenuOceanDynamics` 加双路径结构和 `RenderMode`
2. 保持 `Procedural` 默认，确保旧行为不变
3. 接入 6 类素材加载
4. 完成图片模式实例层
5. 在两个场景中切到 `ImageBased`
6. 做主菜单和大厅视觉验证
7. 如果效果不好，直接把两个场景切回 `Procedural`

## Assumptions

- 主菜单和多人大厅本轮共用一套环境素材
- 视觉内容由图片提供，但动态组织仍由代码控制
- 本轮优先保证可回退，而不是一次性清理旧实现
- 本轮不要求对其他环境层做统一重构
