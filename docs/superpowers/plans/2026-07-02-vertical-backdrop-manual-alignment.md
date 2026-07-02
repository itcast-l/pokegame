# Vertical Backdrop 手工校准方案

## Summary
- 目标：停止继续用坐标换算逼近视觉效果，改为把 `VerticalBackdrop` 当作人工校准资源处理，让顶部头像环、左右竖槽、底部头像环在**实际运行视图**里按你肉眼想要的位置对齐。
- 方案核心：保持运行时 `LayoutAreas` 逻辑不再新增计算分支；把竖版背景资源本身改成与当前运行时布局相匹配的构图。编辑器里不再依赖会改写主场景状态的 `[Tool]` 预览脚本，而是提供一个单独的作者校准预览/导出工具。
- 关键决定：本方案只保证项目默认运行视口 `1080x720` 下的视觉一致性。`960x900` 这类更高视口继续视为非目标，不为它增加新的布局换算。

## Key Changes
- 清理当前实验性预览接线
  - 在 `scenes/game_board_scene.tscn` 里恢复主场景的默认显示状态：
    - `BoardArt` 默认可见
    - `VerticalBackdrop` 默认隐藏
    - `UltraWideBackdrop` 默认隐藏
    - `LayoutAreas` 恢复 FullRect 默认锚点
  - 从 `LayoutAreas` 上移除 `GameBoardLayoutAreasEditorPreview.cs` 的挂载，避免 `[Tool]` 在编辑器里持续把运行时状态写回场景文件。
  - `GameBoardLayoutGeometry.cs` 可以保留，作为运行时内部 helper；本方案不再扩展它，也不再让它参与编辑器手工校准流程。

- 建立单独的竖版背景校准工作流
  - 新增一个**非 shipping 的校准预览脚本**，推荐放在 `tests/` 下，例如 `tests/VerticalBackdropAuthoringPreview.gd`。
  - 这个脚本固定：
    - 视口为 `1080x720`
    - 实例化 `res://scenes/game_board_scene.tscn`
    - 强制进入当前会显示 `VerticalBackdrop` 的模式
    - 在屏幕上叠加关键运行时区域的可视化框：
      - `LayoutAreas/TopPlayerArea/Player2AvatarArea`
      - `LayoutAreas/Player2HandArea`
      - `LayoutAreas/Player2CardPileArea`
      - 对称地也可叠加底部和左右玩家区域，方便一次校准整张图
  - 这个预览脚本只用于出 guide 和人工对照，不修改 shipping 场景，不保存任何运行时计算结果回 `.tscn`。

- 把 `VerticalBackdrop` 资源改成真正的人工对齐资产
  - 继续沿用同一路径：`art/maps/underwater_card_table_vertical_backdrop_1920x1080.png`
  - 先把这张图规范化为**真实的 `1920x1080` 文件**；当前它的文件名写的是 `1920x1080`，实际像素是 `1536x1024`，这会持续制造理解偏差。
  - 以默认运行视图 `1080x720` 的校准 guide 为准，手工移动这张图里的装饰结构，使其在实际游戏画面中对齐运行时区域：
    - 顶部头像环对齐 `Player2AvatarArea`
    - 顶部长槽视觉上包住 `Player2HandArea`
    - 顶部右侧竖槽入口/槽位对齐 `Player2CardPileArea`
    - 左右和底部同理，按对应 avatar / hand / pile 区域一起校准
  - 重要约束：这一步是**改背景图**，不是改运行时布局节点。布局节点保持当前游戏逻辑已有的数值，竖版背景去适配它们。

- 提供一个可重复的导出参考
  - 让校准预览脚本支持导出一张带 guide 的参考截图到仓库外或 `generated-images/`，例如 `generated-images/vertical-backdrop-authoring-guide-1080x720.png`。
  - 这张图是竖版背景美术调整的唯一标尺；后续任何人重做 `VerticalBackdrop` 都必须先对这张 guide，而不是再回去凭 `BoardArt` 猜坐标。

## Test Plan
- 自动验证
  - `dotnet build pokegame.sln`
  - 保留现有 `VerticalBackdrop` 模式切换逻辑的 smoke，继续验证“竖版模式会显示 `VerticalBackdrop`”这一行为；不要新增基于几何换算的断言。
  - 新增一个轻量资源审计，验证 `art/maps/underwater_card_table_vertical_backdrop_1920x1080.png` 的实际尺寸确实为 `1920x1080`，防止文件名和像素尺寸再次不一致。

- 手工验收
  - 运行新的竖版校准预览脚本，在 `1080x720` 下确认 guide 位置稳定。
  - 用 guide 调整 `VerticalBackdrop` 资源后，再运行真实 `game_board_scene.tscn`：
    - 顶部头像环与 `Player2AvatarArea` 视觉重合
    - 顶部长槽与顶部手牌区域视觉重合
    - 顶部右侧牌堆槽与 `Player2CardPileArea` 视觉重合
    - 左右与底部对应区域不出现明显错位
  - 验收标准按肉眼截图比对，不再以 `BoardArt` 坐标推导作为通过条件。

## Public Interfaces
- 无 gameplay API 变化。
- 新增的仅是非 shipping 的校准辅助入口，例如 `tests/VerticalBackdropAuthoringPreview.gd` 或同类 guide 导出脚本。
- 运行时仍然只有一套布局数据；没有新增“第二套 LayoutAreas”或新的布局切换接口。

## Assumptions
- “第二种”按本方案解释为：放弃继续通过计算修正竖版对齐，改为以 `VerticalBackdrop` 资源手工校准为主。
- 目标视口固定为项目默认 `1080x720`；更高/更窄视口的视觉精确对齐不在本次处理范围。
- 当前分支里已经存在的 editor preview 试验性改动需要先收口，避免继续污染主场景保存状态。
- 最终视觉位置以真实游戏截图为准，不以 `BoardArt` 或 `LayoutAreas` 的理论几何一致性为准。
