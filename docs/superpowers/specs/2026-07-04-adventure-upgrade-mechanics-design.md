# 56 个冒险强化逐项机制接入方案

## Summary

- 本文定义冒险模式 `3选1` 强化从“展示接入”推进到“真实机制可用”的整体方案。
- 本轮按 `56` 个可用强化实现：`spades_undertow_asphyxia / 暗流窒息` 设为 `enabled=false`，保留数据但不进入候选池。
- 所有机制只在 `ShouldUseAdventureUpgradeSystem()` 为 true 时生效，不影响普通模式、教程模式和现有道具规则。
- Pandora 继续作为强化展示文案来源；C# 侧用明确的强化 ID 映射机制，不从中文文案解析效果。

## Core Architecture

- 新增 `AdventureUpgradeEffectState`，保存跨关生命、Max HP、永久伤害加成、问号累计次数、第三次问号翻倍、治疗溢出余数、已获得强化 ID、跨关待生效状态。
- 升级 `AdventureUpgradeRunSnapshot` 到 v2，把上述 run 级状态序列化。旧 v1 存档迁移为：保留已获得 ID，生命使用默认 `100/100`，其他计数清零。
- 新增 `AdventureUpgradeEffectService`，集中提供获得时、战斗开始、回合开始、出牌计划、收牌计分、收牌后、空打后、问号奖励后、受伤后、治疗后的纯规则计算。
- `GameBoardScene` 只接少量钩子：应用规则结果、播放已有抽牌/治疗/伤害/翻牌/洗桌动画、处理额外行动窗口。
- 问号奖励从硬编码 `suit_collect` 改回 `ItemCardCatalog.RewardIds + RewardResolver`；`再判一次` 使用一个保留/重掷小弹窗，显示失败时保留原奖励。

## Shared Rules

- “旧桌牌”指本次出牌前已经在桌上的基础牌；本次刚打出的牌不是旧桌牌。
- “基础牌”指普通扑克牌，允许带附着道具；不包含纯道具覆盖牌。
- 奖励抽牌使用现有 `DrawCardFromDeckToHand`，受 `MaxTotalHandCards = 8` 和牌堆数量限制，满手或空牌堆不补偿。
- 收牌伤害结算顺序为：确定计分牌 -> 每张牌点数修正 -> 额外复制/半额追加 -> 整次加减/保底 -> 第三问号后翻倍 -> 实际造成伤害 -> 触发受伤/低血/治疗后续。
- 额外行动规则：主玩家进入“一次性额外基础牌行动窗口”；AI 自动打第一张可用基础牌。额外行动结束后再恢复正常回合推进。
- 同一触发点上不同强化允许叠加；布尔类效果合并，伤害加减类累加，位置类效果按更具体的规则覆盖通用规则。

## Hearts

- `hearts_warm_current_supply / 温流补给`：每回合主玩家首次成功收牌后，正常补牌完成后额外抽 `1`。
- `hearts_long_capture_recovery / 长收回春`：主玩家单次最终收牌伤害 `>= 6` 后治疗 `2`。
- `hearts_miss_bandage / 失手包扎`：主玩家空打结束，登记下个主玩家回合开始后治疗 `3` 并额外抽 `1`。
- `hearts_question_mark_first_aid / 问号急救`：每次获得问号奖励后治疗 `4`。
- `hearts_shoal_preparation / 浅湾整备`：战斗开始额外抽 `1`；若开局三选一刚获得，立即补触发一次。
- `hearts_thick_blood / 厚血`：获得时 Max HP `+4`，并治疗 `4`。
- `hearts_chain_tide_breath / 连潮喘息`：同一主玩家回合第二次成功收牌后，额外抽 `1` 并治疗 `2`。
- `hearts_red_tide_return / 红潮返航`：本战首次主玩家生命从 `>=50%` 掉到 `<50%` 后，治疗 `5` 并抽 `2`，可在死亡判定前救回。
- `hearts_supply_cache_return / 补给回箱`：每次获得问号奖励后，治疗 `3` 并抽 `1`。
- `hearts_enchanted_recovery / 附魔回春`：附着道具效果实际触发时治疗 `3`；若本次打出的基础牌成功收牌，收牌补牌后再抽 `1`。
- `hearts_deep_tide_lung / 深潮肺叶`：获得时 Max HP `+8`；每场战斗开始额外抽 `1`，开局获得时立即补触发。
- `hearts_return_tide_heart / 回潮心室`：每回合首次成功收牌后抽 `1`、治疗 `2`；本回合再次成功收牌后再抽 `1`、治疗 `3`。
- `hearts_revival_draw / 起死回牌`：本战首次生命从 `>=25%` 掉到 `<25%` 后，治疗 `8` 并抽 `2`，可在死亡判定前救回。
- `hearts_overflow_growth / 过量生长`：所有治疗计算溢出；每累计 `4` 点溢出转为 Max HP `+1`，并把当前生命抬到新上限。

## Diamonds

- `diamonds_small_insurance / 小额保价`：主玩家成功收牌且最终正伤害为 `1-4` 时，改为 `5`。
- `diamonds_question_mark_raise / 问号加注`：获得问号奖励后抽 `1`。
- `diamonds_rejudge_once / 再判一次`：本战首次问号奖励时，先展示原奖励，玩家可重掷一次；重掷结果锁定。
- `diamonds_miss_discount / 空打折现`：主玩家每次空打 `+1` 层；下次成功收牌最终伤害 `+层数`，然后清空。
- `diamonds_first_capture_pressure / 首收加压`：本战主玩家首次成功收牌，本次最终伤害 `+2`。
- `diamonds_question_mark_windfall / 问号外快`：每次获得问号奖励后，run 内问号计数 `+1`，并立刻对敌人造成等于计数的直伤。
- `diamonds_bounty_amplify / 赏金放大`：若本次收牌包含问号并会获得奖励，本次最终伤害 `+3`。
- `diamonds_high_return_bonus / 高额返利`：主玩家单次最终收牌伤害 `>=7` 后，登记下次成功收牌最终伤害 `+2`。
- `diamonds_question_mark_echo / 问号回响`：本战首次获得问号奖励后，从后续收牌开始，每张计分牌点数 `+1`。
- `diamonds_multi_card_premium / 多张溢价`：成功收牌时，若计分牌 `>=3` 张，这些计分牌本次点数各 `+1`。
- `diamonds_question_mark_interest / 问号升息`：每次获得问号奖励后，run 内永久收牌伤害加成 `+1`，影响后续收牌。
- `diamonds_top_prize_repeat / 头筹复写`：成功收牌时，最高最终点数的 `1` 张计分牌额外结算 `1` 次；并列取最靠左。
- `diamonds_full_payout / 满额派息`：成功收牌且计分牌 `>=3` 张时，额外造成这些牌原始点数和一半的伤害，向上取整。
- `diamonds_final_double / 终局翻倍`：run 内第 `3` 次获得问号奖励后，后续所有主玩家收牌最终伤害翻倍。

## Spades

- `spades_dark_harbor_reveal / 暗港显形`：敌人空打后，登记敌人下一张基础牌正面入桌。
- `spades_wrong_shore_lineup / 错岸入列`：敌人空打后，登记敌人下一张基础牌插入桌面最左。
- `spades_missed_beat_blunt_hurt / 失拍钝伤`：敌人空打后，登记敌人下一张基础牌若成功收牌，最终伤害 `-3`。
- `spades_miss_bleed / 失手流血`：敌人空打后若桌面至少 `4` 张，敌人受 `2` 点直伤。
- `spades_trace_hunt / 追痕`：你成功收牌后，标记当前桌面最右旧桌牌；下次你收走它时最终伤害 `+4`。
- `spades_reverse_capture_tax / 逆收税`：敌人成功收牌时，中间每有 `1` 张旧桌牌，最终伤害 `-1`，最多 `-3`。
- `spades_undertow_asphyxia / 暗流窒息`：本轮禁用，不进入候选池，也不实现运行时效果。
- `spades_tail_pressure / 压尾`：敌人成功收牌且总收牌数 `>=3` 时，本次敌人打出的尾牌不进牌堆，留在桌面并翻正。
- `spades_return_tide_misorder / 回潮错列`：敌人空打后若已有旧桌牌，登记敌人下一张基础牌插到最早旧桌牌左侧。
- `spades_exaction / 盘剥`：敌人成功收牌时，若中间包含旧桌牌，最终伤害 `-4`。
- `spades_hunt_order / 追猎令`：你的回合开始时，标记最早旧桌牌；下次你收走它时最终伤害 `+6`。
- `spades_intercept_beat / 截拍`：敌人空打后，你立刻获得一次额外基础牌行动；完成后仍正常进入你的回合。
- `spades_black_ledger / 黑账`：你收旧桌牌时旧桌牌都计分；敌人收旧桌牌时旧桌牌都不计分。

## Clubs

- `clubs_short_branch_return / 短枝回手`：每回合一次，若原始收牌只收走两张基础牌且手牌可容纳，这两张进手牌；否则照常进牌堆。
- `clubs_gap_rooting / 夹缝生根`：每回合一次，成功收牌且中间没有计分牌时，额外收走收牌区间左侧最近 `1` 张基础牌，不计分。
- `clubs_primary_root_return / 主根复返`：以基础牌成功收牌时，本次打出的基础牌结算后回手；手牌满则照常进牌堆。
- `clubs_miss_climb_growth / 失手攀枝`：主玩家每次空打 `+1` 层；数字基础牌按原点数 `+层数` 用于匹配；下次成功收牌后清空。
- `clubs_borrow_suit_set / 借花成套`：获得问号奖励后，本回合下一张数字基础牌可按花色匹配一次，出牌后失效。
- `clubs_hollow_burst / 空腔爆开`：本次成功收牌仅收走首尾两张可见牌时，把桌面剩余所有盖牌翻正。
- `clubs_hanging_reverse / 倒挂`：主玩家空打时，本次打出的基础牌插入桌面最左。
- `clubs_wild_branch / 乱枝`：主玩家空打后，立刻随机重排桌面，复用现有洗桌动画。
- `clubs_canopy_reveal / 透棚`：主玩家空打时，本次牌成为透棚锚点；它离场前，之后落在它右侧的牌都正面朝上。
- `clubs_continue_branch / 续枝`：主玩家空打且桌面已有 `4` 张以上牌时，立刻获得一次额外基础牌行动。
- `clubs_linked_stems / 连株`：同一主玩家回合第二次成功收牌后，立刻获得 `1` 次问号奖励，并触发所有问号相关强化。
- `clubs_climb_canopy / 攀棚`：附着道具效果实际生效且本次打出的基础牌成功收牌时，该基础牌结算后回手。
- `clubs_cross_graft / 横嫁`：持续生效；数字基础牌可按有效点数的上下相邻点数匹配。与攀枝叠加时，先计算攀枝有效点数，再取相邻点。
- `clubs_crown_canopy / 冠棚`：持续生效；你的基础牌成功收牌时，首牌和尾牌也计分一次。

## Implementation Notes

- 方片、黑桃、梅花大量效果会改变收牌分数或收牌范围，优先下沉到规则层，避免动画显示分数和实际扣血不一致。
- 红桃依赖跨关生命，因此必须先让冒险玩家生命上限和当前生命由 run state 驱动，而不是每场战斗固定重置为 `100`。
- 黑桃和梅花都需要“出牌前桌面快照”，用于判断旧桌牌、最早旧桌牌、最右旧桌牌、尾牌是否为本次刚打出的牌。
- 额外行动窗口要限制只能打基础牌，不能打道具牌，避免和当前道具选择、网络同步、手牌弃置流程交叉。
- 可先不新增复杂特效资源，所有强化触发反馈复用现有抽牌、治疗/伤害数字、翻牌、洗桌、道具触发光效。

## Test Plan

- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
  - active 强化数为 `56`，四花色各 `14`，`暗流窒息` 不入池。
  - `AdventureUpgradeRunSnapshot` v2 能保存并恢复生命、Max HP、问号计数、永久加成、已获得强化。
  - 问号奖励随机、重掷、问号次数、第三次后翻倍均可纯规则验证。
  - 收牌伤害顺序可验证：点数修正、复制、半额追加、保底、加减、翻倍。
- Godot smoke：
  - 红桃跨关生命、Max HP、低血救回、治疗溢出。
  - 方片三次问号后翻倍、问号外快、下次伤害加成。
  - 黑桃敌方空打 pending、追猎标记、黑账旧桌牌计分。
  - 梅花额外行动、回手、邻点匹配、短收翻桌。
- 回归命令：
  - `dotnet build pokegame.csproj`
  - `bash tests/run_adventure_upgrade_run_start_smoke.sh`
  - `bash tests/run_adventure_upgrade_victory_smoke.sh`
  - `bash tests/run_adventure_upgrade_big_capture_smoke.sh`
  - `bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh`

## Locked Decisions

- 本轮目标是 `56` 个可用强化。
- 禁用项为 `spades_undertow_asphyxia / 暗流窒息`。
- 旧桌牌按“本次出牌前已在桌上”判定。
- 主玩家额外行动不自动打牌，必须给玩家一次可操作窗口。
- 抽牌满手或空牌堆时不补偿。
- 半分伤害使用向上取整。
