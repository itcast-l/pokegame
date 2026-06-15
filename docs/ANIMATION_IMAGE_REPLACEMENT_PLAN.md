# Animation Image Replacement Plan

Last audited: 2026-06-15

本文件汇总当前项目中仍由代码绘制的动画/动态视觉，并给出需要生成图像素材的清单与接入调整方案。范围只覆盖“视觉表现可以被透明 PNG 帧动画替换”的内容；纯粹的卡牌位移动画、Tween 运动、shader 后处理、功能性 UI 指示不列为优先作图对象。

## 结论

优先需要作图替换的是战斗 FX、特殊牌触发、桌面洗牌漩涡、磁铁附加环、头像反馈叠加效果。环境动态层也有大量代码绘制内容，但它们是背景氛围系统，建议放在第二批或第三批，避免一次性替换导致资源体积和适配成本过高。

## 作图优先级

| 优先级 | 当前代码生成内容 | 位置 | 需要生成的图像素材 | 接入调整 |
| --- | --- | --- | --- | --- |
| P0 | 叶海龙飞行刀光主体由 `Polygon2D` 拼出，只有命中刀光使用现有 PNG 帧 | `scripts/GameBoardScene.LeafySeadragonBladeFx.cs` | 飞行刀光 4-6 帧，命中爆闪/斩击 4 帧，透明 PNG | 用 `Sprite2D`/`TextureRect` 帧动画替换 `FlightBlade` 下的多个 `Polygon2D`，保留贝塞尔飞行轨迹和命中时机 |
| P0 | 特殊牌触发外圈由 `DrawArc` 画，四角火花是 `ColorRect` | `scripts/GameBoardScene.DeckAnimation.cs`, `scripts/GameBoardScene.SpecialCardAnimations.cs` | 卡牌中心爆发外圈 + 四角火花 6 帧，透明 PNG | 保留卡牌本体和现有中心 FX，删除/旁路 `SpecialCardTriggerRing` 与 `ColorRect` sparks |
| P0 | 桌面洗牌中心漩涡由圆弧、线和圆点绘制 | `scripts/GameBoardScene.DeckAnimation.cs`, `scripts/GameBoardScene.TableShuffleAnimation.cs` | 桌面洗牌漩涡 8 帧，透明 PNG，可做一次性扩散或短循环 | `CreateTableShuffleVortex()` 改成创建帧动画纹理节点，保留 gather/scatter 卡牌运动 |
| P1 | 磁铁附加成功时的扩散环由 `DrawCircle`/`DrawArc` 绘制 | `scripts/GameBoardScene.MagnetAttachments.cs`, `scripts/GameBoardScene.MagnetAttachmentAnimations.cs` | 磁铁附加能量环 6 帧，透明 PNG | 用纹理帧替换 `MagnetAttachmentApplyRing`，保留现有飞到手牌和缩放/淡出节奏 |
| P1 | 磁铁选择倒计时外圈是代码绘制，底部倒计时球已有 PNG | `scripts/GameBoardScene.MagnetAttachmentCountdown.cs` | 倒计时环分段帧：普通 11 帧，紧急 5-11 帧，透明 PNG | 用进度映射选择环纹理，保留数字 Label、提示 Label 和已有 countdown orb |
| P1 | 头像 score/hit/victory/defeat 的外圈、闪光和遮罩由 `_Draw()` 绘制 | `scripts/CharacterAvatar.cs`, `scripts/CharacterAvatar.Actions.cs` | 通用头像反馈叠加帧：score、hit、victory、defeat 各 4-6 帧 | 头像动作帧继续用现有 PNG，只把 `_Draw()` 中的脉冲叠加改成可复用纹理节点 |
| P2 | 主菜单/水下/冒险地图环境里的气泡、光束、水流、闪点、海草由代码绘制 | `scripts/MainMenuOceanDynamics.cs`, `scripts/UnderwaterAmbientLayer.cs`, `scripts/AdventureMapDynamicLayer.cs` | 背景氛围循环素材：气泡、光束、焦散、水流、闪点、海草摆动，分层透明 PNG | 分层替换，不建议一次性删除动态层；先用纹理节点替换最显眼的气泡和焦散 |

## P0 详细方案

### 叶海龙飞行刀光

当前情况：

- `GameBoardScene.cs` 已有 `LeafySeadragonBladeProjectileFramePattern`，路径为 `res://art/fx/player_leafy_seadragon_leafy_mirage_slash/concept-001/processed/fx-{0}.png`，帧数是 4。
- `GameBoardScene.LeafySeadragonBladeFx.cs` 里飞行阶段仍创建 `FlightBlade`，并通过多个 `Polygon2D` 画 trail、leaf fin、body、core。
- 命中阶段只取 `_leafySeadragonBladeProjectileFrames[2]` 当作 `HitSlash`。

建议作图：

| 素材 | 建议路径 | 帧数 | 画布 | 内容 |
| --- | --- | --- | --- | --- |
| 飞行刀光 | `art/fx/player_leafy_seadragon_blade_projectile/run-001/flight/processed/fx-{0}.png` | 4-6 | 256x256 | 横向刀叶能量体，中心对齐，头部朝右 |
| 命中斩击 | `art/fx/player_leafy_seadragon_blade_projectile/run-001/impact/processed/fx-{0}.png` | 4 | 256x256 | 短促斩击爆光，最后一帧消散 |

接入调整：

- 新增两个加载常量，例如 `LeafySeadragonBladeFlightFramePattern` 和 `LeafySeadragonBladeImpactFramePattern`，避免继续复用一个 4 帧序列同时承担飞行和命中。
- `PlayLeafySeadragonBladeProjectileAsync()` 中将 `flightBlade` 改成 `Sprite2D` 或 `TextureRect`，使用 `FrameAnimationHelpers.TweenTextureFrames()` 播放飞行帧。
- 保留现有 `UpdateSpriteBezierFlight()`、三发 stagger、缩放、透明度和命中切换时机。
- 删除或停用 `CreateLeafySeadragonBladePolygon()`，命中时隐藏飞行 sprite，显示 impact sprite。

验收标准：

- 飞行途中不再出现代码多边形刀身。
- 三发刀光方向跟随飞行方向，透明边缘无黑边。
- 命中帧和卡牌破坏/移除节奏一致。

### 特殊牌触发外圈和火花

当前情况：

- `SpecialCardTriggerRing` 在 `GameBoardScene.DeckAnimation.cs` 中用 `DrawArc` 绘制多层圆环。
- `PlaySpecialCardTriggerEffectAsync()` 在 `GameBoardScene.SpecialCardAnimations.cs` 中额外创建四个 `ColorRect` 作为角落火花。
- 中心 `SpecialCardTriggerFx` 已经是 PNG 帧，路径来自 `SpecialCardTriggerEffectFramePattern`。

建议作图：

| 素材 | 建议路径 | 帧数 | 画布 | 内容 |
| --- | --- | --- | --- | --- |
| 特殊牌触发外圈 | `art/fx/special_card_trigger_activation/run-001/processed/fx-{0}.png` | 6 | 320x320 | 卡牌中心扩散的海洋魔法环、四角火花、短促高光 |

接入调整：

- 保留 `SpecialCardTriggerCard` 和现有 `SpecialCardTriggerFx`，先只替换外圈和四角火花。
- 新增 `SpecialCardTriggerActivationFrames`，在 `effectRoot` 下添加一个覆盖卡牌的大尺寸 `TextureRect`。
- 删除/旁路 `SpecialCardTriggerRing` 和 `ColorRect` spark 创建逻辑；Tween 只控制纹理节点的 alpha/scale。
- 敌方能力触发时当前会复用 `_enemySpecialAbilityAvatarFxFrames` 作为中心 flash；替换外圈时不要改变这条分支。

验收标准：

- 特殊牌触发时不再出现代码圆环和矩形火花。
- 新外圈不遮挡卡牌核心信息。
- 普通特殊牌和敌方能力借用触发都能播放。

### 桌面洗牌漩涡

当前情况：

- `TableShuffleVortex` 在 `GameBoardScene.DeckAnimation.cs` 中用 `DrawArc`、`DrawLine`、`DrawCircle` 绘制。
- `GameBoardScene.TableShuffleAnimation.cs` 负责卡牌 gather/scatter，并对 `vortex_alpha`、`vortex_progress` 做 Tween。

建议作图：

| 素材 | 建议路径 | 帧数 | 画布 | 内容 |
| --- | --- | --- | --- | --- |
| 洗牌漩涡 | `art/fx/table_shuffle_vortex/run-001/processed/fx-{0}.png` | 8 | 320x320 | 中心水流漩涡、卡牌能量牵引线、轻微旋转感 |

接入调整：

- 新增 `TableShuffleVortexFramePattern` 和 `TableShuffleVortexFrameCount`。
- `CreateTableShuffleVortex()` 改为创建纹理帧节点，位置仍以 `globalCenter` 为中心。
- gather 阶段淡入并播放前半段帧；scatter 阶段继续播放后半段并淡出。
- 如果美术帧已经包含完整扩散过程，可以删除 `vortex_progress` 属性，只保留 alpha/scale。

验收标准：

- 洗牌中心不再由代码线条组成。
- 卡牌飞入/飞出路径不变。
- 多人同步事件不需要改 payload，因为只是本地视觉替换。

## P1 详细方案

### 磁铁附加成功能量环

当前情况：

- `MagnetAttachmentApplyRing` 在 `GameBoardScene.MagnetAttachments.cs` 里绘制扩散圆、进度弧、内外高光。
- `GameBoardScene.MagnetAttachmentAnimations.cs` 控制它的 `ring_progress` 和 `ring_alpha`。

建议作图：

| 素材 | 建议路径 | 帧数 | 画布 | 内容 |
| --- | --- | --- | --- | --- |
| 磁铁附加环 | `art/fx/magnet_attach_apply_ring/run-001/processed/fx-{0}.png` | 6 | 256x256 | 蓝绿磁场环、黄色吸附高光、向外扩散 |

接入调整：

- 用 `TextureRect` 或 `Sprite2D` 替代 `MagnetAttachmentApplyRing`。
- 保留现有磁铁卡飞行动画、目标卡缩放、附加 overlay。
- 如果需要保持“进度感”，用帧序号表达扩散，而不是继续画部分圆弧。

### 磁铁倒计时进度环

当前情况：

- `MagnetCountdownFramePattern` 已经加载倒计时球底图：`res://art/fx/magnet_selection_rework/run-001/countdown_orb/processed/fx-{0}.png`。
- `MagnetAttachmentCountdownRing` 仍用 `DrawArc` 画普通/紧急进度环。
- 中间数字和底部提示是 Label，应继续保留。

建议作图：

| 素材 | 建议路径 | 帧数 | 画布 | 内容 |
| --- | --- | --- | --- | --- |
| 普通倒计时环 | `art/fx/magnet_selection_rework/run-002/countdown_ring_normal/processed/fx-{0}.png` | 11 | 128x128 | 100%, 90%, ... 0% 的蓝绿环 |
| 紧急倒计时环 | `art/fx/magnet_selection_rework/run-002/countdown_ring_urgent/processed/fx-{0}.png` | 11 | 128x128 | 同样进度分段，颜色偏金黄/警示 |

接入调整：

- 用纹理节点替换 `MagnetAttachmentCountdownRing`。
- 在 `UpdateMagnetAttachmentCountdown()` 中把 `progress` 映射到 0-10 的帧索引。
- `progress <= 0.35f` 时切换到 urgent ring 序列；数字颜色和提示闪烁逻辑可以保留。
- 不建议为每一百分比生成一帧，11 帧已经足够表达 5 秒倒计时。

### 头像反馈叠加效果

当前情况：

- 头像 idle/score/attack/hit/victory/defeat 动作帧已经从 `art/characters/<avatar>/run-*/<action>/processed/<action>-{n}.png` 加载。
- `CharacterAvatar.cs` 的 `_Draw()` 仍绘制 score ring、hit flash、victory spark、defeat shade。
- `ShowFrame` 的基础头像圆框也是代码绘制，但它更像 UI 框架，不是必须替换的动画。

建议作图：

| 素材 | 建议路径 | 帧数 | 画布 | 内容 |
| --- | --- | --- | --- | --- |
| 得分反馈 | `art/fx/avatar_feedback_score/run-001/processed/fx-{0}.png` | 4-6 | 192x192 | 绿色/青色上扬光环 |
| 受击反馈 | `art/fx/avatar_feedback_hit/run-001/processed/fx-{0}.png` | 4 | 192x192 | 红色冲击闪光和短促震荡感 |
| 胜利反馈 | `art/fx/avatar_feedback_victory/run-001/processed/fx-{0}.png` | 6 | 192x192 | 金色圆环和小星点 |
| 失败反馈 | `art/fx/avatar_feedback_defeat/run-001/processed/fx-{0}.png` | 4 | 192x192 | 暗色柔和罩层，不能完全遮挡头像 |

接入调整：

- 在 `CharacterAvatar` 内新增一个或多个 overlay `TextureRect`，跟随头像控件居中布局。
- `PlayAction()` 或现有 pulse 入口触发对应 overlay 帧动画。
- `_Draw()` 中保留 `ShowFrame` 基础框，先移除 `_scorePulse`、`_hitPulse`、`_victoryPulse`、`_defeatShade` 对应的动画绘制。
- 如果后续也要替换基础圆框，单独生成静态 frame，不和动作反馈混在一起。

## P2 环境动态层方案

环境层当前由代码程序化绘制，优点是适配任意分辨率、资源小、随机性强。只有当目标是“统一为美术生成风格”时才建议替换。

### 主菜单海洋动态层

位置：`scripts/MainMenuOceanDynamics.cs`

代码绘制内容：

- sunbeams
- surface wake
- current ribbons
- caustic ribbons
- bubble streams
- glints

建议拆成以下素材，而不是一张全屏视频式序列：

| 素材 | 建议路径 | 帧数 | 画布 | 接入方式 |
| --- | --- | --- | --- | --- |
| 气泡循环 | `art/fx/environment/main_menu_bubbles/run-001/processed/fx-{0}.png` | 8-12 | 256x512 | 多个 `TextureRect`/`Sprite2D` 随机 x 位置和速度上浮 |
| 水面/水流线 | `art/fx/environment/main_menu_current_ribbon/run-001/processed/fx-{0}.png` | 8 | 512x128 | 横向平铺或多节点漂移 |
| 焦散/闪光 | `art/fx/environment/main_menu_caustic_glints/run-001/processed/fx-{0}.png` | 8 | 512x512 | 低 alpha 叠加，线性过滤 |

### 水下通用氛围层

位置：`scripts/UnderwaterAmbientLayer.cs`

代码绘制内容：

- edge depth
- sunbeams
- seagrass
- water currents
- center caustics
- motes
- center glints
- bubbles

建议先替换最显眼的三类：

| 素材 | 建议路径 | 帧数 | 画布 | 接入方式 |
| --- | --- | --- | --- | --- |
| 海草摆动 | `art/fx/environment/seagrass_sway/run-001/processed/fx-{0}.png` | 8 | 256x512 | 左右边缘多实例，保留随机高度/缩放 |
| 气泡 | `art/fx/environment/ambient_bubbles/run-001/processed/fx-{0}.png` | 8-12 | 256x512 | 多实例上浮 |
| 焦散光纹 | `art/fx/environment/ambient_caustics/run-001/processed/fx-{0}.png` | 8 | 512x512 | 中心区域淡淡叠加 |

### 冒险地图动态层

位置：`scripts/AdventureMapDynamicLayer.cs`

代码绘制内容：

- current ribbons
- sparkles

建议作图：

| 素材 | 建议路径 | 帧数 | 画布 | 接入方式 |
| --- | --- | --- | --- | --- |
| 地图水流带 | `art/fx/environment/adventure_map_current/run-001/processed/fx-{0}.png` | 8 | 512x128 | 多条横向漂移 |
| 地图闪点 | `art/fx/environment/adventure_map_sparkle/run-001/processed/fx-{0}.png` | 6 | 128x128 | 多实例随机闪烁 |

## 建议保留代码绘制

以下内容不是第一批作图对象。它们更偏功能 UI、布局适配、低成本装饰或 shader 效果，替换为 PNG 帧动画的收益较低。

| 内容 | 位置 | 原因 |
| --- | --- | --- |
| 分数头像圆形计量器 | `scripts/ScoreAvatarMeter.cs` | 功能型仪表，需要根据分数/状态实时变化 |
| 冒险地图路线和节点 | `scripts/AdventureMapRouteLayer.cs` | 路线状态动态变化；如果地图固定，可另做静态底图 |
| 新手教程指示线/高亮 | `scripts/GameBoardScene.AdventureTutorial.Overlay.cs` | 功能性指引，代码线条更容易适配目标位置 |
| UI 面板和按钮装饰 | `scripts/OceanMenuButtonFrame.cs`, `scripts/OceanDialogPanelArt.cs`, `scripts/PlayerCountDialogFrameArt.cs`, `scripts/MainMenuPlayerCountDialogPanelArt.cs` | 装饰随控件尺寸变化，代码绘制更灵活 |
| 小型 UI 图标/分隔线 | `scripts/MainMenuPlayerCountIconBadge.cs`, `scripts/PearlDivider.cs`, `scripts/DraggableAvatarArea.cs`, `scripts/ScoreEffectTarget.cs` | 体量小、动态需求弱 |
| 卡牌禁用/提示标记 | `scripts/GameBoardScene.CardRenderHelpers.cs` | 功能状态标记，实时绘制更可靠 |
| 场景转场和屏幕 shader | `scripts/CircularSceneTransition.cs`, `shaders/*.gdshader` | shader 更适合全屏过渡、焦散、模糊等效果 |

## 图像素材规格

- 格式：透明背景 PNG 帧序列，不使用 GIF。
- 命名：沿用现有一基索引：`processed/fx-1.png`, `processed/fx-2.png`。
- 帧数：短促战斗 FX 4-6 帧，漩涡/环境循环 8-12 帧，倒计时分段 11 帧。
- 画布：单体 FX 优先 192x192 或 256x256；卡牌中心覆盖和桌面漩涡优先 320x320；环境层按 256x512、512x128、512x512 拆分。
- 对齐：主体居中，方向类素材默认朝右，代码里再按飞行方向旋转。
- 背景：不能烘焙深色背景、卡牌、数字或 UI 文案。
- 边缘：半透明边缘要干净，避免黑边、白边和残留色块。
- 过滤：FX 通常使用 `CanvasItem.TextureFilterEnum.Linear`；卡牌本体继续按现有逻辑使用 `Nearest`。
- 导入：资源加入 Godot 后确认 `.import` 生成且运行时无 missing resource warning。

## 推荐实施顺序

1. 先做 P0 三项：叶海龙飞行刀光、特殊牌触发外圈、桌面洗牌漩涡。
2. 第二批做磁铁附加环和磁铁倒计时环，因为已有磁铁倒计时底图，接入范围可控。
3. 第三批做头像反馈叠加，先只替换动作反馈，不替换基础头像圆框。
4. 环境动态层最后做，并且按单层替换验证：先气泡/焦散，再水流/海草。

## 验证清单

- 启动 Godot 后确认没有新增缺失纹理 warning。
- 本地触发特殊牌、桌面洗牌、磁铁附加、磁铁选择倒计时。
- 冒险模式测试叶海龙相关战斗表现，确认飞行方向、命中节奏、遮挡层级正常。
- 测试 2/3/4 人布局，确认 FX 不随控件缩放产生错位。
- 检查透明 PNG 边缘，深色背景和浅色背景上都不能有明显边框。
- 对比替换前后性能；环境层如果改成大量纹理实例，需要特别看低端设备帧率。
