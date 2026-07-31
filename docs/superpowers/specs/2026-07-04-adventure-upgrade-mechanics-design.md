# 54 个冒险强化逐项机制契约

## Summary

- 本文定义冒险模式 `3选1` 强化从“展示接入”推进到“真实机制可用”的整体方案。
- 有效目录固定为 Pandora 中 `54` 个 `enabled=true` 强化，花色分布为红桃 `14`、方片 `14`、黑桃 `12`、梅花 `14`。
- 所有机制只在 `ShouldUseAdventureUpgradeSystem()` 为 true 时生效，不影响普通模式、教程模式和现有道具规则。
- Pandora 继续作为强化展示文案来源；C# 侧用明确的强化 ID 映射机制，不从中文文案解析效果。

## Core Architecture

- `AdventureUpgradeEffectState` 按 Action、Turn、Battle、Run 明确状态作用域；只有 Run 状态进入冒险存档。
- `AdventureUpgradeRunSnapshot` 使用 v5，只写合法强化 ID、生命、Max HP、问号累计、永久伤害加成和治疗溢出余数。v1-v4 按当前 manifest 迁移；损坏或目录未就绪时保留原 JSON。
- 新增 `AdventureUpgradeEffectService`，集中提供获得时、战斗开始、回合开始、出牌计划、收牌计分、收牌后、空打后、问号奖励后、受伤后、治疗后的纯规则计算。
- `GameBoardScene` 只接少量钩子：应用规则结果、播放已有抽牌/治疗/伤害/翻牌/洗桌动画、处理额外行动窗口。
- 问号奖励从硬编码 `suit_collect` 改回 `ItemCardCatalog.RewardIds + RewardResolver`；`再判一次` 使用一个保留/重掷小弹窗，显示失败时保留原奖励。

## Shared Rules

- “旧桌牌”指本次出牌前已经在桌上的基础牌；本次刚打出的牌不是旧桌牌。
- “基础牌”指普通扑克牌，允许带附着道具；不包含纯道具覆盖牌。
- 奖励抽牌使用现有 `DrawCardFromDeckToHand`，受 `MaxTotalHandCards = 8` 和牌堆数量限制，满手或空牌堆不补偿。
- 收牌伤害结算顺序为：唯一普通计分项 -> 牌面修正与 Double -> 每张牌加成 -> 显式复写项 -> 固定加减值 -> 小额保价 -> 满额/终局加算倍率桶 -> 敌方减伤取较强值 -> 单事件上限 -> 实际伤害。
- 额外行动使用不可变 grant 队列；每个真实回合最多 `8` 次额外基础牌行动，每个 root chain 最多处理 `32` 个强化事件。截拍的正常回合承诺由整条派生链继承。
- “真实回合”从行动权由敌方交给主玩家开始，到成功收牌续行动、额外行动和派生链全部结束并真正把行动权交给敌方为止；成功收牌、补牌和额外行动都不新开真实回合。
- 除固定冲突裁决外，同一触发点上的不同强化允许叠加；盘剥与逆收税分别计算后只取较强减伤。

## Hearts

- `hearts_warm_current_supply / 温流补给`：每个真实回合主玩家首次成功收牌后，正常补牌完成后额外抽 `1`。
- `hearts_long_capture_recovery / 长收回春`：主玩家单次最终收牌伤害 `>= 6` 后治疗 `2`。
- `hearts_miss_bandage / 失手包扎`：只有空打后确认没有续行动且行动权真正交给敌方时，才登记下个真实回合开始治疗 `3` 并额外抽 `1`。
- `hearts_question_mark_first_aid / 问号急救`：每次获得问号奖励后治疗 `4`。
- `hearts_shoal_preparation / 浅湾整备`：战斗开始额外抽 `1`；若开局三选一刚获得，立即补触发一次。
- `hearts_thick_blood / 厚血`：获得时 Max HP `+4`，并治疗 `4`。
- `hearts_chain_tide_breath / 连潮喘息`：同一真实回合第二次成功收牌后，额外抽 `1` 并治疗 `2`。
- `hearts_red_tide_return / 红潮返航`：本战首次主玩家生命从 `>=50%` 掉到 `<50%` 后，治疗 `5` 并抽 `2`，可在死亡判定前救回。
- `hearts_supply_cache_return / 补给回箱`：每次获得问号奖励后，治疗 `3` 并抽 `1`。
- `hearts_enchanted_recovery / 附魔回春`：本动作真实附着道具效果实际触发时治疗 `3`，同一动作多个附着也最多治疗 `3`；若本次打出的基础牌成功收牌，收牌补牌后再抽 `1`。
- `hearts_deep_tide_lung / 深潮肺叶`：获得时 Max HP `+8`；每场战斗开始额外抽 `1`，开局获得时立即补触发。
- `hearts_return_tide_heart / 回潮心室`：每个真实回合首次成功收牌后抽 `1`、治疗 `2`；同一真实回合第二次成功收牌后再抽 `1`、治疗 `3`。
- `hearts_revival_draw / 起死回牌`：本战首次生命从 `>=25%` 掉到 `<25%` 后，治疗 `8` 并抽 `2`，可在死亡判定前救回。
- `hearts_overflow_growth / 过量生长`：所有治疗计算溢出；每累计 `4` 点溢出转为 Max HP `+1`，并把当前生命抬到新上限；run 内 Max HP 最高 `150`，余数最多保留 `3`。

## Diamonds

- `diamonds_small_insurance / 小额保价`：主玩家成功收牌且最终正伤害为 `1-4` 时，改为 `5`；`0` 伤害仍为 `0`。
- `diamonds_question_mark_raise / 问号加注`：获得问号奖励后抽 `1`。
- `diamonds_rejudge_once / 再判一次`：本战首次问号奖励时，先展示原奖励，玩家可保留或重掷一次；无论保留还是重掷都会消费本战唯一机会，重掷结果锁定。
- `diamonds_miss_discount / 空打折现`：主玩家每次空打 `+1` 层；下次成功收牌最终伤害 `+层数`，然后清空。
- `diamonds_first_capture_pressure / 首收加压`：本战主玩家首次成功收牌，本次最终伤害 `+2`。
- `diamonds_question_mark_windfall / 问号外快`：每次获得问号奖励后，run 内问号计数 `+1`（最多 `10`），并按当前奖励序号立刻直伤（单次最多 `5`）。
- `diamonds_bounty_amplify / 赏金放大`：若本次收牌包含问号并会获得奖励，本次最终伤害 `+3`。
- `diamonds_high_return_bonus / 高额返利`：主玩家单次最终收牌伤害在排除本次消费的返利 `2` 点后仍 `>=7`，才登记下次成功收牌最终伤害 `+2`；本次返利不能自行重新武装。
- `diamonds_question_mark_echo / 问号回响`：run 内首次问号奖励完成后，从下一次收牌开始，每张计分牌点数 `+1`；跨敌人保留。
- `diamonds_multi_card_premium / 多张溢价`：成功收牌时，若计分牌 `>=3` 张，这些计分牌本次点数各 `+1`。
- `diamonds_question_mark_interest / 问号升息`：每次获得问号奖励后，run 内永久收牌伤害加成 `+1`（最多 `5`），从下一次收牌开始生效。
- `diamonds_top_prize_repeat / 头筹复写`：成功收牌且至少有 `3` 张计分牌时，最高最终点数的 `1` 张计分牌额外结算 `1` 次；并列取收牌前最靠左，并以带来源的显式重复计分项表示。
- `diamonds_full_payout / 满额派息`：成功收牌且计分牌至少 `6` 张时，整次倍率桶 `+1`。
- `diamonds_final_double / 终局翻倍`：run 内第 `10` 次问号奖励完成后激活，从下一次收牌开始使整次倍率桶 `+1`。与满额派息同时生效为 `x3`，再与牌面 Double 合计最高 `x6`。

## Spades

- `spades_dark_harbor_reveal / 暗港显形`：敌人成功收牌后，登记敌人下一张基础牌正面入桌；敌人空打不触发。
- `spades_wrong_shore_lineup / 错岸入列`：敌人成功收牌后，登记敌人下一张基础牌插入桌面最左；敌人空打不触发。
- `spades_missed_beat_blunt_hurt / 失拍钝伤`：敌人空打后，登记敌人下一张基础牌若成功收牌，最终伤害 `-3`。
- `spades_miss_bleed / 失手流血`：敌人空打后若桌面至少 `6` 张，敌人受 `2` 点直伤。
- `spades_trace_hunt / 追痕`：你成功收牌后，标记当前桌面最右旧桌牌；下次你收走它时最终伤害 `+4`。
- `spades_reverse_capture_tax / 逆收税`：敌人成功收牌时，中间每有 `1` 张旧桌牌，最终伤害 `-1`；与盘剥同时生效时分别计算并只取较强减伤。
- `spades_tail_pressure / 压尾`：敌人成功收牌且总收牌数恰好为 `7` 时，本次敌人打出的尾牌不进牌堆，留在桌面并翻正。
- `spades_return_tide_misorder / 回潮错列`：敌人空打后若已有旧桌牌，登记敌人下一张基础牌插到最早旧桌牌左侧。
- `spades_exaction / 盘剥`：敌人成功收牌时，若中间包含旧桌牌，最终伤害 `-4`；与逆收税同时生效时分别计算并只取较强减伤。
- `spades_hunt_order / 追猎令`：你的真实回合开始时，标记最早旧桌牌；下次你收走它时最终伤害 `+6`。
- `spades_intercept_beat / 截拍`：敌人空打后，你立刻获得一次额外基础牌行动；整条派生链排空后仍必须正常开始你的真实回合。
- `spades_black_ledger / 黑账`：敌人成功收牌后，其中最高分的 `1` 张计分牌转入你的牌堆；敌人本次伤害照常结算。

## Clubs

- `clubs_short_branch_return / 短枝回手`：每个真实回合一次，若完整收牌集合恰好含 `2` 张稳定身份不同的基础牌，且手牌容量足以容纳本动作全部唯一回手候选，则两张回手；否则照常进牌堆。
- `clubs_gap_rooting / 夹缝生根`：每个真实回合一次，成功收牌且中间没有计分牌时，额外收走收牌区间左侧最近 `1` 张基础牌；扩展牌进入完整收牌集合但不计分。
- `clubs_primary_root_return / 主根复返`：本次打出的基础牌成功收牌且手牌有空位时，该牌结算后回手。
- `clubs_miss_climb_growth / 失手攀枝`：主玩家每次空打 `+1` 层；数字基础牌保留原点数匹配，并额外增加“原点数 `+层数`”候选；下次成功收牌后清空。
- `clubs_borrow_suit_set / 借花成套`：获得问号奖励后保持一次 armed，同一动作重复获得只刷新、不叠加；本真实回合下一张数字基础牌在完全没有点数或替代点数候选时才可按花色兜底一次；合法出牌后消费，真实回合或战斗结束时清除。
- `clubs_hollow_burst / 空腔爆开`：完整收牌集合仅含首尾两张可见端点时，把桌面剩余所有盖牌翻正；磁吸和夹缝生根扩展都计入完整集合。
- `clubs_hanging_reverse / 倒挂`：主玩家空打时，本次打出的基础牌插入桌面最左。
- `clubs_wild_branch / 乱枝`：主玩家空打后，立刻随机重排桌面，复用现有洗桌动画。
- `clubs_canopy_reveal / 透棚`：主玩家空打时，本次牌成为透棚锚点；它离场前，之后落在它右侧的牌都正面朝上。
- `clubs_continue_branch / 续枝`：主玩家空打且未收取结算后的桌面有 `6` 张以上牌时，立刻获得一次额外基础牌行动；每个真实回合最多触发一次，额外行动不能重入。
- `clubs_linked_stems / 连株`：同一真实回合第二次成功收牌后，立刻获得 `1` 次问号奖励，并触发所有问号相关强化。
- `clubs_cross_graft / 横嫁`：持续生效；数字基础牌可按有效点数的上下相邻点数匹配。与攀枝叠加时，先计算攀枝有效点数，再取相邻点。
- `clubs_crown_canopy / 冠棚`：持续生效；你的基础牌成功收牌时，首尾端点各取得一次普通计分资格，不通过独立通道生成重复普通计分项。

## Implementation Notes

- 方片、黑桃、梅花大量效果会改变收牌分数或收牌范围，优先下沉到规则层，避免动画显示分数和实际扣血不一致。
- 红桃依赖跨关生命，因此必须先让冒险玩家生命上限和当前生命由 run state 驱动，而不是每场战斗固定重置为 `100`。
- 黑桃和梅花都需要“出牌前桌面快照”，用于判断旧桌牌、最早旧桌牌、最右旧桌牌、尾牌是否为本次刚打出的牌。
- 额外行动窗口要限制只能打基础牌，不能打道具牌，避免和当前道具选择、网络同步、手牌弃置流程交叉。
- 可先不新增复杂特效资源，所有强化触发反馈复用现有抽牌、治疗/伤害数字、翻牌、洗桌、道具触发光效。

## Test Plan

- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
  - active 强化数为 `52`，稀有度 `24/18/10`，花色 `14/13/12/13`，退役或未知 ID 均被拒绝。
  - `AdventureUpgradeRunSnapshot` v5 能迁移 v1-v4，只持久化 Run 状态，并对所有数值执行统一钳制。
  - 红桃跨战生命、Max HP `150` 上限、低血救回并行汇总和治疗溢出饱和计算可纯规则验证。
  - 问号奖励、重掷、`1/5/10` 次边界和“下一次收牌生效”可纯规则验证。
  - 收牌伤害顺序可验证：身份去重、显式复写、保底、倍率桶、减伤与单事件上限。
  - 固定冲突裁决可验证：盘剥/逆收税取较强值、倒挂/乱枝顺序、低血救援同快照汇总、借花成套点数优先及抽牌容量截断。
- Godot smoke：
  - 两个续枝闭环、截拍派生链、真实回合边界和失手包扎登记。
  - 附魔回春命中/空打、空腔爆开完整收牌集合、冠棚端点计分身份和各类回手容量。
  - 方片第十次问号完成后从下一次收牌生效、再判机会、敌方 pending 与 Battle 状态清理。
  - run 恢复、v4 到规范 v5 的实际持久化迁移，以及 54 项 Pandora catalog。
- 回归命令：
  - `dotnet build pokegame.csproj`
  - `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
  - `bash tests/run_adventure_upgrade_interaction_smoke_suite.sh`
  - `bash tests/run_adventure_upgrade_run_resume_localization_layout_smoke.sh`
  - `bash tests/run_game_board_verbose_leak_suite.sh`
  - `bash tests/run_multiplayer_reconnect_smoke_suite.sh`
  - `git diff --check`

## Locked Decisions

- 本轮目标是 Pandora 中 `54` 个 enabled 强化。
- 血潮共鸣、万利归一、黑潮统御、枝繁叶茂已完整退役，不得通过存档、debug 或非标准入口重新获得。
- 旧桌牌按“本次出牌前已在桌上”判定。
- 主玩家额外行动不自动打牌，必须给玩家一次可操作窗口。
- 抽牌满手或空牌堆时不补偿。
