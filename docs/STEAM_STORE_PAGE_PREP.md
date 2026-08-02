# Steam Store Page Prep

Last updated: 2026-08-01

本文件用于整理 `Deckland` 的 Steam 商店页准备项，并提供一版可直接继续打磨的首版文案。目标不是立即执行发售，而是把 `Coming Soon` 页面所需的材料、当前仓库事实、承诺边界和缺口一次性写清楚，避免后续在平台、语言、玩法卖点和素材使用上出现口径漂移。

## Summary

当前建议采用以下固定策略：

- 第一阶段只做 `Coming Soon` 页面，不直接按正式发售页承诺
- 本轮只做 Steam 页面相关与素材准备，不做三平台验证
- 商店页文案与素材先围绕当前已经存在、且能稳定演示的玩法组织
- 不提前承诺当前仓库没有验证闭环的功能，例如成就、云存档、正式跨平台支持
- 把“可用于商店陈述的已确认事实”和“仍需补验证的目标能力”严格分开

当前仓库已经能够支持商店页核心定位的内容包括：

- 海洋主题卡牌对战视觉风格
- 2-4 人本地牌桌玩法
- 主菜单多人入口、LAN 流程与 Steam lobby hooks
- 线性冒险推进
- 角色头像与解锁进度
- 简中、繁中、英文完成度较高的本地化基础

当前不应直接作为已公开承诺写入商店页的内容包括：

- `Windows + macOS + Linux` 三平台正式支持
- “游戏内多语言已完整支持到大量语种”
- 成就、云存档、正式在线服务能力
- 已完成的正式发售日期

当前这轮工作的直接交付目录固定为：

- `/Users/cakeju/Downloads/person/game`

## Current Repo Facts

截至 2026-06-17，仓库内已确认事实如下：

- 项目名为 `Deckland`
- 引擎为 Godot 4.6 C# 项目
- `README.md` 当前定义的主定位是 card battler prototype，包含 local card-table play、item-card effects、character avatars、early adventure progression flow
- 本地对战支持 `2-4` 人
- 主菜单多人入口已接入 prototype lobby，支持 LAN host/search、ready/start flow，并在 Steam 可用时走 Steam lobby hooks
- 冒险模式当前是线性敌人阶梯推进
- 角色头像选择和冒险解锁进度会写入存档
- `project.godot` 当前启用了 `zh_CN`、`zh_TW`、`en` 三个翻译资源
- `export_presets.cfg` 当前存在 `Windows Desktop`、`Linux` 和 `macOS` 导出预设
- 仓库中存在 `asset/video/logo_video.mp4`，但当前尚未确认它可直接作为 Steam 预告片使用

这些事实意味着：

- 商店页可以稳定宣传“海洋主题卡牌对战 + 冒险推进 + 本地/Steam 多人入口”
- 商店页当前不应直接宣传“已完成三平台发售支持”；macOS 原生启动已验证，Linux 和三平台同 build ID 联机矩阵仍未完成
- 商店页当前不应直接宣传“游戏内已完成多语种支持”，除非后续做完单独审计和实机校对

## Store Positioning Draft

### One-line Pitch

`Deckland` is an ocean-fantasy card battler where you build momentum through fast table matches, unlock character avatars, and push through a growing adventure path solo or with friends.

### Short Description Draft

Enter a luminous undersea card table in `Deckland`, a stylish card battler featuring local multiplayer, adventure progression, unlockable avatars, and Steam-enabled lobby play.

### Long Description Draft

`Deckland` is a colorful ocean-fantasy card battler built around fast matches, expressive avatar presentation, and a bright undersea table atmosphere.

Play local matches with `2-4` players, turn on item cards for extra chaos, and push through an early adventure path where each win unlocks the next opponent. Between battles, unlock character avatars and build a stronger profile as you progress.

The current game structure already supports:

- Local card-table battles for `2-4` players
- Optional item-card rules for more volatile matches
- A linear adventure ladder with opponent unlock progression
- Character avatar selection and unlock flow
- Multiplayer lobby flow with LAN support and Steam lobby integration hooks

`Deckland` is being prepared for a `Coming Soon` Steam presence first. The store page should present the current game honestly: strong atmosphere, readable card-table action, and a clear foundation for both solo progression and multiplayer sessions.

### Key Feature Bullets Draft

- `2-4 Player Card Battles`: Play compact table matches with flexible player count and score targets.
- `Ocean-Fantasy Presentation`: Fight across a luminous undersea board with ornate card backs, animated effects, and stylized avatar frames.
- `Adventure Progression`: Climb a linear battle path, defeat new opponents, and unlock the next challenge.
- `Unlockable Avatars`: Collect character avatars and personalize your player identity.
- `Item Card Mode`: Add extra rule twists through data-driven item card effects.
- `Multiplayer Foundations`: Launch LAN sessions now, with Steam lobby integration already part of the game flow.

## Steam Page Requirements

以下内容属于 Steam 商店页准备时必须覆盖的基础项。具体尺寸、格式和审核规则以后续 Steamworks 页面为准，当前整理的是工作层面的交付物集合。

### Store Text

至少准备：

- 游戏名
- 短描述
- 长描述
- 5-7 条核心卖点
- 玩法标签初稿
- 类型定位
- 联机方式说明
- 语言声明
- 平台声明
- 系统需求草稿

### Visual Assets

至少准备：

- Capsule 图组
- Header / library 资产
- 商店截图
- 预告片封面图
- 预告片视频

### Product Metadata

至少准备：

- 开发者名
- 发行者名
- 发售状态
- `Coming Soon` 时间口径
- 支持控制方式
- 单人 / 多人 / 在线联机等功能勾选
- 内容分级或敏感内容调查

### Review Readiness

至少确认：

- 商店页中的所有承诺都能被当前版本或明确的近期待办支撑
- 截图来自真实游戏运行画面
- 视频内容与截图内容不误导用户
- 语言和平台声明与实际能力一致

## Recommended Claim Boundary For First Page

这是当前最重要的边界控制项。即使后续目标是更大范围，也不建议在第一页超前承诺。

第一版 `Coming Soon` 页面建议公开声明：

- 平台：`Windows`
- 游戏内语言：`Simplified Chinese`, `Traditional Chinese`, `English`
- 商店页文案语言：可以追加更多语言翻译，但不能因此等同于“游戏内完整支持”
- 玩法模式：单人、本地多人、在线多人入口正在建设或已接入 Steam lobby 流程

第一版不建议公开声明：

- `macOS` 已支持
- `Linux` 已支持
- 大量游戏内语言已经完整可玩
- 成就、云存档、Steam Deck Verified
- 正式发售日期

如果后续坚持公开 `Windows + macOS + Linux`，则必须把以下工作改成上架前置条件：

- 使用同一 `--build-id` 生成三平台正式候选
- 各平台真机启动验证
- 输入、字体、分辨率、Steam 初始化路径验证
- 至少一次各平台基础联机或单机冒烟验证

截至 2026-08-01，三平台导出适配器和包审计已经就绪，当前统一候选为 `deckland-rc-20260801-host-migration-5`。macOS universal 候选已通过原生 executable smoke、独立哈希证据和直接挂载 Release PCK 的三玩家 Host 硬杀迁移门禁；Windows 候选通过 CrossOver smoke，但仍等待 Windows 原生证据；Linux x86_64 候选完成静态与 PCK 审计，但仍等待 Linux 目标机运行。该进展不改变首版公开平台边界；本地单设备回环门禁不能替代双实机 LAN，只有统一候选完成 Windows/Linux/macOS 目标机证据和双实机 LAN 矩阵后，才能修改商店声明。

如果后续坚持公开更多游戏内语言，则必须把以下工作改成上架前置条件：

- 逐语种完成度审计
- UI 溢出检查
- 新手引导与关键按钮实机检查
- 商店页与游戏内语言声明逐项对齐

## Asset Readiness Review

当前仓库中已经存在一些可作为商店页制作参考的素材，但不是所有素材都适合直接上传 Steam。

### Good Candidate References

- 图标基础：`assets/app_icon/deckland_app_icon_1024.png`
- 对局视觉：`outputs/game_board_score_avatar_runtime.png`
- 开场牌桌视觉：`outputs/game_board_fallback_runtime.png`

这些素材适合作为：

- capsule 构图参考
- 截图重拍镜头参考
- 商店视觉风格板参考

### Should Not Be Uploaded Directly

- 编辑器截图，例如 `outputs/main_menu_editor_view.png`、`outputs/adventure_map_editor_view.png`
- 当前已确认是黑屏的运行图，例如 `outputs/main_menu_avatar_frame_runtime.png`、`outputs/adventure_map_avatar_frame_runtime.png`、`outputs/adventure_map_route_runtime.png`
- 纯 UI 提示弹窗图，例如 `outputs/avatar_unlock_prompt_runtime.png`
- 没有明确展示实际玩法的信息图

### Screenshot Direction

第一版建议至少准备 `5-8` 张真实运行截图，优先覆盖：

1. 核心牌桌对局画面
2. 手牌与出牌状态
3. 多头像同屏对战状态
4. 冒险地图或关卡选择画面
5. 头像解锁或头像选择画面
6. 多人 lobby 画面

截图选择标准：

- 先展示玩法，再展示 UI 美术细节
- 避免大量纯文字遮挡
- 避免纯黑过场、空牌桌、无行动状态
- 至少有 2-3 张能一眼看懂“这是卡牌对战游戏”

### Trailer Direction

第一版预告片建议控制在短时长内，目标是把“类型、氛围、玩法、多人入口”快速讲清楚。

建议结构：

1. `0-5s`: Logo + 海洋主题牌桌氛围
2. `5-15s`: 对局核心镜头，展示出牌、牌桌、头像
3. `15-25s`: 冒险地图 / 解锁 / 头像内容
4. `25-35s`: 多人 lobby 或多人对局入口
5. `35s+`: 收尾 slogan + `Wishlist Now`

当前 `asset/video/logo_video.mp4` 只能先视为素材池，不应在未复核内容前直接当作 Steam trailer 定稿。

## Steam Metadata Draft

### Genre / Category Draft

建议首版定位：

- Genre: `Strategy`, `Card Game`, `Casual`
- Additional flavor: `Fantasy`, `Stylized`, `Party-friendly`

### Feature Flags Draft

根据当前仓库现状，首版可考虑的功能口径：

- Single-player
- Shared / Split Screen 不应乱勾，除非后续确认为同屏真实支持方式
- Local Multiplayer
- LAN PvP
- Online PvP 只有在 Steam 路径完成更稳定验证后再决定是否前置公开

### Tag Draft

首版可考虑：

- Card Battler
- Strategy
- Casual
- Local Multiplayer
- Stylized
- Fantasy
- PvP
- Party Game

最终标签需要以后续 Steam 相似品类调研再收敛，当前只作为内部草稿。

## Current Deliverables

本轮目标不是补齐所有未来审计项，而是先把 Steam 页面与素材准备包交付出来。

当前已经整理并输出到 `/Users/cakeju/Downloads/person/game` 的交付物包括：

1. `DECKLAND_STEAM_COMING_SOON_PACK.md`
2. `DECKLAND_STEAM_ASSET_MANIFEST.md`
3. `deckland_app_icon_1024.png`
4. `game_board_score_avatar_runtime.png`
5. `game_board_fallback_runtime.png`
6. `logo_video.mp4`

这些交付物覆盖了：

- 首版商店页文案草稿
- 截图 shot list
- capsule 基础尺寸清单
- trailer 规格与结构草稿
- `Coming Soon` 提审自检表
- 当前可用 / 不可用参考素材列表

本轮没有把以下内容作为交付目标：

- `macOS` 验证
- `Linux` 验证
- 三平台对外公开支持声明
- 更多语言实机审计

## Store Copy Polish Tasks

首版文案还需要继续打磨的点：

- 确认“card battler”还是“table card battler”作为主定位
- 决定是否突出“party-friendly”还是偏“strategy”
- 决定 `Steam-enabled lobby play` 的措辞是否改为更保守表述
- 明确“冒险模式”在商店里写到什么程度，避免给人内容量过大的预期

## Recommended Next Deliverables

按最小可执行顺序，后续建议输出以下交付物：

1. 重拍 `1920x1080` 实机截图，补满 `5-8` 张正式 Steam 截图
2. 基于现有牌桌视觉出一版 Header / Small / Main / Vertical capsule 成图
3. 录制真实 gameplay trailer，并把 `logo_video.mp4` 只保留为片头素材池
4. 补全开发者名、发行者名、系统需求等仍为空的 Steam metadata
5. 在实际提交前再次核对 Steamworks 后台中的 library 资产要求

如果后续要升级成更激进的公开承诺，再新增以下延期项：

1. `macOS` 导出与真机验证
2. `Linux` 导出与真机验证
3. 更多游戏内语言完成度审计
4. Online PvP 对外公开口径验证

## External References

以下官方页面属于后续实际提交前必须复核的入口：

- [Steamworks Graphical Assets](https://partner.steamgames.com/doc/store/assets)
- [Steamworks Trailers](https://partner.steamgames.com/doc/store/trailer)
- [Steamworks Coming Soon](https://partner.steamgames.com/doc/store/coming_soon)
- [Steamworks Supported Languages](https://partner.steamgames.com/doc/store/localization/languages)
- [Steamworks Store Page Description](https://partner.steamgames.com/doc/store/page/description)
- [Steamworks Supported Platforms](https://partner.steamgames.com/doc/store/application/platforms)

## Frozen Decision For This Draft

本文件当前固定采用以下口径：

- 文档类型：准备清单 + 首版文案
- 文件用途：本地策划与提审准备，不代表已经执行上架
- 第一优先级：先把 `Coming Soon` 页面准备完整
- 当前执行范围：只做 Steam 页面相关和素材准备
- 默认推荐承诺范围：`Windows + 简中/繁中/英文`
- 当前不做三平台验证；如果后续要改成三平台和多语公开，再单独补审计任务
