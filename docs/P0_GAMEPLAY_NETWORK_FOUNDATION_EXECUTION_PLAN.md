# Deckland P0 可执行实施计划（复审增强版）

## 复审结论

上一版方向正确，但还未达到“Agent 无需补做架构设计”的程度，主要缺口是：异步选择流程无法直接序列化、旧/新 Host 消息隔离规则不完整、checkpoint 落点不明确、测试进程编排不具体。本版补齐这些决策，作为上一版的完整替代。

P0 仅包含玩法数据底座、正式冒险进度、LAN 完整牌局和 LAN 房主迁移。Steam、Demo、Coming Soon、新关卡、新道具、新数值不进入 P0。

## 强制执行规则

- 每个 Wave 独立提交；开始前保存 `git status --short`，不得修改或提交现有布局改动。
- 前一 Wave 的验收命令全部通过后才能进入下一 Wave。
- 新增纯 C# 规则文件必须链接进 `tests/Rules.Tests/Rules.Tests.csproj`。
- `Starting/LoadingScene` 阶段不迁移；Host 此时消失统一返回多人菜单。只迁移稳定 Lobby 和未结束的 InGame。
- 所有网络状态比较使用 `(authority_epoch, snapshot_id)`；epoch 增加后允许 snapshot/generation 回退到最后已确认 checkpoint。

## Wave 0：恢复零失败基线

- 修正 `UPGRADE_DESC_HEARTS_WARM_CURRENT_SUPPLY` 的简中、繁中、英文，使其明确表示“每个实际回合首次收牌后抽 1”；Pandora 和规则代码不变。
- 验收：build 零警告错误、Rules.Tests 291/291、现有 reconnect suite 通过。
- 命令：`dotnet build pokegame.csproj`；`dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`；`bash tests/run_multiplayer_reconnect_smoke_suite.sh`。
- 提交：`Fix current-supply upgrade localization baseline`。

## Wave 1：封板 Pandora 与可调玩法契约

- 新增 `RewardDeliveryMode { None, Hand, ImmediateAttachment }`。问号奖励中 `attach_modifier` 必须为 `ImmediateAttachment + PlayableFromHand=false`；其他可领取奖励必须为 `Hand + PlayableFromHand=true`；非奖励卡为 `None`。
- 将 `MagnetHandCard` 重命名为通用 `AttachmentRewardHandCard`，同步更新输入、AI、快照、网络预测和提示；现有立即附着流程保持行为不变。
- Pandora 独占玩法字段：启用、奖励资格、权重、稀有度、玩法 tags、delivery、play presentation、trigger、effect type/values、priority、attached effect、description key。`.tres` 独占美术字段，但保存一份由 Pandora 生成的完整玩法 fallback。
- Pandora 已加载时，禁止从 `.tres` 补单个缺失玩法字段；缺字段、重复 ID、未知 effect/presentation、非法 delivery、未知 value key 均使整个 manifest 不可用。只有 Pandora 整体不可用时才加载 `.tres` fallback。
- 新增 `ItemCardEffectContractRegistry`，登记每种 effect 的允许 trigger、允许/必需 value key、范围和 delivery 约束；`SpecialCardEffectResolver` 与 presentation handler 初始化后必须审计 key 集合与 registry 完全一致，删除 `ItemCardEffectSupport` 的重复支持列表。
- Pandora 冒险强化新增 `effect_values`。`AdventureUpgradeRuntimeMetadata` 为每个现有强化登记允许字段、当前硬编码值作为默认值及范围；未知/缺失字段禁用整个强化 manifest，不得静默使用代码常量。
- 数值字段命名统一为 `amount/threshold/count/multiplier/maximum/step/stack_limit`，多阶段效果使用 `first_*`、`second_*`；count/threshold 范围 0–52，伤害/治疗 0–999，multiplier 1–10，stack 1–99，生命上限 1–999。
- 将可调数字对应的本地化描述改成参数模板，Pandora 声明有序 `description_value_keys`；所有语言占位符数量必须一致，界面始终使用当前 manifest 数值格式化。
- 新增 `tools/ItemCardManifestSync.gd --check|--write`：`--write` 只更新 fallback 玩法字段；`--check` 比较 Pandora、fallback、handler、描述占位符并生成稳定 SHA-256 gameplay manifest hash。
- 提交：`Centralize gameplay manifest contracts`。

## Wave 2：模式策略与正式冒险 12 关进度

- 新增 `GameModeKind`：`LocalStandard`、`SoloStandard`、`AdventureTutorial`、`AdventureMain`、`NetworkStandard`。保留现有导出布尔字段兼容 `.tscn`，在 `_Ready` 中一次性推导不可变 `GameModePolicy`。
- 网络 session 优先推导为 `NetworkStandard`；随后依次判断 AdventureTutorial、AdventureMain、Solo、Local。只有两个 Adventure policy 可以读取、结算或保存 `AdventureRunSessionStore`，三选一入口也必须检查该 policy。
- 新增共享 `AdventureCampaignCatalog`：教学固定为 `fish/lanternfish`，正式固定为 `shrimp...boss_k` 共 12 关；菜单、解锁、run 和测试不得再各自维护关卡数组。
- profile schema 升为 v2：字段改为 `HighestCompletedMainAdventureStage`（0–12）和 `CompletedAdventureTutorialIdsCsv`。v1 的 0–2 映射正式进度 0，3–14 映射 `stage-2`，越界 clamp；教学完成默认空。
- Adventure run snapshot 升为 v6，保存 `CampaignId + MainStageIndex + EnemyAvatarId`；旧 v1–v5 的全局 stage 3–14 转为 1–12，其他 stage 保留原 JSON、报告不可恢复并不启动 run。
- 正式第一关始终解锁，后续按 1–12 顺序；教学完成不增加正式进度、不进入正式通关率、不产生正式头像待解锁项。旧头像 ID 队列保留、去重并删除教学 ID。
- 验收：旧档边界 0/2/3/14/越界、重复迁移、v5 run 转 v6、损坏 run、两个教学独立完成、正式 12 关依次解锁；NetworkStandard 残留冒险 run 时不得出现三选一或写档。
- 提交：`Separate game modes and main adventure progress`。

## Wave 3：真实请求驱动的完整 LAN 牌局

- 新增复用脚本 `LanFullMatchPeer.gd`，参数包含 role、seat、player count、item mode、结果目录；shell 分别启动 2/3/4 个 Godot 进程，Host 等待全部真人 ready 后按真实 `StartMatch()` 开局。
- 自动玩家每 100ms 检查本地输入阶段：pending 时提交第一个合法目标；否则使用第一张合法道具；否则打第一张合法基础牌。新增 debug API 只能查询合法项并调用现有 input/request handler，禁止直接改牌堆、分数、赢家或当前回合。
- 使用正式最低胜分 50、固定测试 seed、`Engine.TimeScale` 加速；每局墙钟超时 180 秒。AI 不补开局人数。
- 新增 `NetworkBoardViewFingerprint`：按固定字段顺序计算 SHA-256。Host 保存每个 seat 的预期裁剪视图；Client 保存应用后的视图；结果验证器按相同 epoch/snapshot 比较，并断言其他席位仅有数量、没有 card code/item id。
- 新增 `run_lan_full_match_2p_standard_smoke.sh`、`3p_items`、`4p_items` 和聚合 suite；每局验证赢家、分数、桌面、牌堆/手牌/道具手牌数量以及正常胜负流程。
- 提交：`Add full LAN match convergence coverage`。

## Wave 4：将异步权威流程变成可恢复事务

- 新增可序列化 `AuthorityActionState`，字段固定为：action id、client action id、kind、phase、actor seat、turn generation、基础/道具 hand index、item/reward id、target seat/index、reserve replacement、played/captured/completes turn/deck collected、剩余附着奖励队列。
- `AuthorityActionKind` 固定为 `None/BaseCard/ItemCard/QuestionReward/CardTransfer`；phase 固定为 `Applying/WaitingTargetPlayer/WaitingTargetHand/WaitingDiscard/WaitingAttachment/Finalizing`。
- `TaskCompletionSource` 仅作为当前进程 UI 等待器，不再代表权威真相。Target、HandLimitDiscard、Attachment 三类选择先写入 `AuthorityActionState`，再发布 checkpoint；恢复后由 `ResumeAuthorityActionAsync()` 按 phase 重建等待器或继续 finalizing。
- 每个 mutation 完成后才推进 phase；恢复时不得重复已经完成的扣牌、得分、抽牌或奖励。每席位保存 `LastAcceptedClientActionId`，重复或更旧请求只返回快照，不重复执行。
- 稳定提交点固定为：大厅 roster/ready/config 改变；开局动画完成；等待选择建立；选择阶段推进；完整基础牌/道具牌/AI 动作和回合推进完成。普通视觉动画中间状态不是提交点。
- 提交：`Make authority actions resumable`。

## Wave 5：权威 checkpoint 与 epoch 协议

- 协议统一为 `NetworkProtocol.CurrentVersion = 4`；新增 `MigratingHost`、`NetworkAuthorityPhase`、`NetworkMigrationPlan`、`NetworkMigrationRosterEntry`、`NetworkAuthorityCheckpoint`、`NetworkAuthorityCheckpointCodec`。
- Checkpoint schema v1 包含：完整私有 config（含房间密码）、manifest hash、roster/迁移 token、seat control/reconnect claim、last client action ids、全部牌堆/手牌/道具手牌、桌面及附着、分数/赢家/回合/question counter、AuthorityActionState、RNG seed/state、snapshot id、epoch/checkpoint id。
- 不保存 Tween、节点、Texture、Task、视觉队列、预测状态或 `IsHiddenDuringCapture`；卡牌只存稳定 code、附着 ID 和逻辑 flags，恢复时从 catalog 重建运行时资源。
- Codec 使用显式 DTO、`JsonPropertyName/JsonPropertyOrder`、UTF-8、SHA-256 小写 hex；payload 上限 256KB，数组和字符串复用现有网络边界。decode 和语义校验全部通过后才原子替换状态。
- 所有 action、snapshot、visual、reconnect、migration RPC 带 `session_id/match_id/authority_epoch`；高 epoch 先清空视觉/预测并重置 snapshot fence，同 epoch 只接受更大 snapshot，低 epoch 一律丢弃。
- `ExportAuthorityCheckpoint()` 只从稳定提交点调用；`TryRestoreAuthorityCheckpoint()` 恢复 RNG state、事务、snapshot/generation 后硬渲染，并用新 epoch 发布第一份按席位裁剪快照。
- 提交：`Add deterministic authority checkpoints and epochs`。

## Wave 6：LAN 热备与完整房主迁移

- 新增 `IHostMigrationTransport`，只暴露 observed address、start server、connect 和 close；P0 实现 `LanHostMigrationTransport`，Steam 实现明确返回 Unsupported。
- 玩家进入 Lobby 时由 Host 生成私有 session migration token 并单独发送；开始比赛后沿用现有 reconnect token。公开 roster 和迁移计划永远不含 token。
- 热备始终为除 Host 外最低席位在线真人。每个稳定提交点只向热备发送 `PackedByteArray checkpoint`；热备 decode、校验 hash 后 ACK。连续两个 checkpoint 未 ACK 或断线时，Host 才切换下一最低席位。
- ACK 后 Host 向所有人广播不含隐藏信息的 plan：source/target epoch、checkpoint id/hash、候选 stable id/seat、Host 观察到的 LAN 地址和固定端口 37146。客户端只缓存最新合法 plan。
- Host 崩溃时只有 plan 指定候选可以建服：250ms 后尝试绑定，每 250ms 重试至 5 秒；其他客户端 750ms 后每 500ms 连接；总超时 10 秒。不得在失败后自行进行第二轮选主。
- 新 Host 成为 peer id 1；InGame 保留所有原 seat，新 Host 的游戏 seat 不改，原 Host seat 转 `AiProxyReclaimable`。Lobby 则移除原 Host，剩余玩家按旧 seat 排序重编号，新 Host 为 seat 0，保留 ready，配置和密码不变。
- 追随者使用 stable id + session token + reconnect token claim 原 seat；InGame 超时未回来的席位转 AI，之后仍可按现有回合起点规则认领。新 Host 恢复并发布硬快照前，任何牌局输入均拒绝。
- 主动离开复用同一 plan：等待候选已有最新 ACK，广播 begin-migration，最多等待客户端 2 秒确认后关闭旧 peer；没有可用热备时正常离开并让其他客户端安全失败返回菜单。
- 提交：`Implement LAN hot-standby host migration`。

## Wave 7：失败回退、迁移 UI 与封板

- `MigratingHost` 时清除预测和旧视觉队列，锁定全部牌局输入；显示“正在迁移/正在恢复/迁移失败”状态。失败统一关闭 peer、清理 checkpoint/token/plan，返回多人菜单，不继续本地模拟。
- 自动迁移 suite 覆盖：Lobby 主动迁移；2 人普通 Host 强杀；3/4 人道具迁移后打到胜负；Host 当前回合；Target/Discard/Attachment pending；视觉动画中强杀回滚；旧 epoch 注入；原 Host token 认领；候选同时退出；错误 hash；端口和总超时。
- 强杀测试由 shell 在确认 `STANDBY_ACKED` 后 `kill -9` Host，不能调用 Host 的正常离开逻辑；边界 fixture 可构造指定 pending 状态，但“迁移后继续到赢家”的牌局仍必须使用真实请求。
- 最终门禁：build、Rules.Tests、manifest `--check`、现有 reconnect suite、full-match suite、host-migration suite 全绿；两台真实 LAN 设备完成一次 Host 进程强杀并继续到赢家。
- 文档记录协议 v4、事务 phase、checkpoint schema、RPC 时序、超时、失败码、Steam P1 transport 接入点和新增玩法数据字段。
- 提交：`Harden and document the P0 multiplayer baseline`。

## P0 非目标

不实现 Steam Lobby owner/Steam transport 迁移、Steam 双账号测试、Demo、Coming Soon、新冒险关卡、新道具、新强化、皮肤、观战、匹配服务、专用服务器或 AI 补齐开局真人。

---

## 执行回填（2026-07-15）

以下内容记录本计划在当前仓库中的实际落地状态，不改变上面的范围约束。

### 协议与状态契约

- `NetworkProtocol.CurrentVersion = 4`。
- `NetworkProtocol.AuthorityCheckpointSchemaVersion = 1`，checkpoint 最大 payload 为 256 KiB，最大玩家数为 4，LAN 迁移端口为 `37146`。
- `NetworkSessionState` 新增并实际使用 `MigratingHost`；稳定迁移阶段只接受 `Lobby` 和 `InGame` authority phase。
- `NetworkAuthorityPhase` 为 `Lobby/Starting/LoadingScene/InGame/MigratingHost/Failed`。P0 只为稳定 Lobby 和未结束 InGame 生成迁移 plan；Starting/LoadingScene 的 Host 消失直接返回多人菜单。
- `AuthorityActionKind` 固定为 `None/BaseCard/ItemCard/QuestionReward/CardTransfer`。
- `AuthorityActionPhase` 固定为 `Applying/WaitingTargetPlayer/WaitingTargetHand/WaitingDiscard/WaitingAttachment/Finalizing`。`TaskCompletionSource` 只负责当前进程等待器，checkpoint 中的 `AuthorityActionState` 才是恢复依据。
- 每个 seat 保存 `LastAcceptedClientActionId`；重复或更旧的 client action 不会重新执行 mutation。

### AuthorityActionState

`AuthorityActionState` 使用显式 `JsonPropertyName/JsonPropertyOrder` 字段，包含 action/client action id、kind/phase、actor seat、turn generation、基础牌和道具牌 hand index、item/reward id、target seat/index、reserve replacement、played/captured/completes turn/deck collected、剩余附着奖励队列，以及 question reward/card transfer/discard/attachment mutation flags。

恢复相关字段还包括 `ContinuationStep`、`PrimaryMutationApplied`、`TurnAdvanceApplied`、pending reward、嵌套 discard 的 deferred target/phase。每个 mutation 完成后才推进事务状态；恢复时按 phase 重建 target/discard/attachment waiter，或继续 finalizing，避免重复扣牌、得分、抽牌和奖励。

### Checkpoint schema v1

`NetworkAuthorityCheckpoint` 的稳定 JSON 顺序如下：

- metadata：`schema_version`、`protocol_version`、`session_id`、`match_id`、`room_name`、`room_password`、`match_seed`、`player_count`、`item_mode`、`winning_score`、`authority_phase`、`manifest_hash`；
- roster/control：`roster`、`seat_controls`、`last_accepted_client_action_ids`、`session_migration_tokens`、`reconnect_claims`；公开 roster 和 plan 不携带这些私有 token；
- deterministic board：`decks`、`hands`、`item_hands`、`table_cards`（稳定 card code、附着 ID 和逻辑 flags）、`scores`、`winner_player_index`；
- turn/action：`current_turn_player_index`、`turn_sequence_generation`、`completed_turns_since_question_refresh`、`question_refresh_anchor_player_index`、`authority_action`；
- RNG/fence：`rng_seed`、`rng_state`、`snapshot_id`、`authority_epoch`、`checkpoint_id`。

`NetworkAuthorityCheckpointCodec` 先验证 SHA-256 小写 hex 和 256 KiB 上限，再做 protocol/schema、上下文、seat、roster、卡牌集合、token、action、RNG 和 board bounds 的语义校验；全部成功后才交给 `TryRestoreAuthorityCheckpoint()` 原子恢复。checkpoint 不保存 Tween、节点、Texture、Task、视觉队列、预测状态或 `IsHiddenDuringCapture`。

### RPC 时序

1. Host 在稳定提交点导出 checkpoint，发送 `RpcStandbyCheckpoint(session_id, match_id, authority_epoch, payload, hash, checkpoint_id)`；最低席位在线真人 decode 后通过 `RpcStandbyCheckpointAck` 返回 hash 和 observed LAN address。
2. Host 在 ACK 对应最新 checkpoint 后广播 `RpcMigrationPlan`。plan 只含 session/match、source/target epoch、checkpoint id/hash、candidate stable id/seat、source stable id、observed/candidate address、端口和 authority phase。
3. 主动迁移额外发送 `RpcBeginHostMigration`，各客户端通过 `RpcConfirmHostMigration` 回执；确认窗口结束后 source Host 关闭 peer。被动迁移则由 `OnServerDisconnected` 直接进入同一 plan。
4. 候选在 250 ms 后绑定 ENet server，校验 checkpoint/hash/context 后成为 peer 1；InGame 恢复事务、发送第一份硬快照并等待恢复完成。其他客户端在 750 ms 后连接候选，收到 `RpcReconnectResult` 后用 reconnect token claim seat。
5. Lobby 迁移使用单独的 session migration token。跟随者发送 `RpcClaimMigratedLobbySeat`，只有 stable id 和 token 同时有效才接受；新 Host 在 claim 前广播的 roster 不会让跟随者提前退出 `MigratingHost`，claim 成功后的 `RpcSyncLobby` 才切换到 Lobby。旧 Host 或错误 token 通过 `RpcLobbyRejected` 返回 `OldHostTokenRejected`。
6. 新 Host 完成所有必要 readiness 后发送 `RpcMigrationReady`；客户端提升 epoch、清理旧视觉/预测状态、解除输入锁。所有 action、snapshot、visual、reconnect 和 migration context 都带 `session_id/match_id/authority_epoch`；高 epoch 清理旧 fence，低 epoch 丢弃，同 epoch 只接受更大 snapshot。

### 超时与失败码

| 项目 | P0 值 |
| --- | ---: |
| standby ACK 等待 | 1.0 s |
| 连续未 ACK 后切换候选 | 2 个 checkpoint |
| 候选首次绑定 / 重试间隔 / 窗口 | 0.25 s / 0.25 s / 5.0 s |
| 跟随者首次连接 / 单次超时 / 重试间隔 / 总超时 | 0.75 s / 2.0 s / 0.50 s / 10.0 s |
| 主动迁移客户端确认窗口 | 2.0 s |
| LAN peer ENet timeout | factor 32，1000–2500 ms |

统一失败码为 `NoPlan`、`NotCandidate`、`CheckpointMissing`、`CheckpointHashMismatch`、`CheckpointInvalid`、`BindTimeout`、`ConnectTimeout`、`CandidateUnavailable`、`OldHostTokenRejected`、`ConcurrentCandidateExit`。失败路径关闭 peer，清空 checkpoint/token/plan/readiness/prediction 状态，显示失败提示并返回多人菜单；不会继续本地模拟。

### LAN transport 与 Steam P1 接入点

`IHostMigrationTransport` 只暴露 `ObservedAddress`、`StartServer`、`Connect` 和 `Close`。P0 的 `LanHostMigrationTransport` 使用 ENet 和固定迁移端口 `37146`；`SteamHostMigrationTransport` 已作为明确的 `Unsupported` stub 接入，P1 只需在该接口实现 Steam relay/owner handoff，并在 Steam lobby owner 迁移流程接入相同的 checkpoint、epoch、claim 和 readiness fence。P0 不宣称 Steam 双账号迁移已验证。

### Gameplay manifest 字段

Item card 的 Pandora/fallback/handler contract 由 `tools/ItemCardManifestSync.gd` 校验，字段为：`id`、`enabled_in_game`、`playable_from_hand`、`can_be_question_reward`、`weight`、`rarity`、`tags`、`reward_delivery`、`play_presentation`、`trigger`、`effect_type`、`effect_values`、`priority`、`attached_trigger`、`attached_effect_type`、`attached_effect_values`、`attached_priority`、`description_key`。`attach_modifier` 使用 `ImmediateAttachment + PlayableFromHand=false`；普通可领取奖励使用 `Hand + PlayableFromHand=true`；非奖励卡使用 `None`。

Adventure upgrade manifest 额外保存 `effect_values`，由 `AdventureUpgradeRuntimeMetadata` 声明允许 key、默认值、范围和 description template；未知/缺失字段、非法 effect、delivery 或 placeholder 会使 manifest 不可用，不从 `.tres` 静默补玩法字段。当前 `--check` 生成的 gameplay manifest hash 为 `0befd69b9b63863172883dee5e7eb743d3806f541c83f4b4a6ad33fe35792d7d`。

### 实现索引

- Wave 4：`scripts/AuthorityActionState.cs`、`scripts/GameBoardScene.NetworkAuthorityState.cs`、`scripts/GameBoardScene.NetworkAuthority.cs`、`scripts/NetworkAuthorityActionVariantCodec.cs`、`scripts/NetworkManager.AuthorityActions.cs`，以及 `tests/Rules.Tests/Program.NetworkFoundationTests.cs`。
- Wave 5：`scripts/NetworkAuthorityCheckpoint.cs`、`scripts/GameBoardScene.NetworkCheckpoint.cs`、`scripts/NetworkProtocol.cs`、`scripts/NetworkMigrationRules.cs`，并扩展 `NetworkManager`、`NetworkManager.BoardSync`、`NetworkManager.Reconnect`、`GameBoardScene.NetworkSnapshot` 的 context/epoch/checkpoint fence。
- Wave 6：`scripts/HostMigrationTransport.cs`、`scripts/NetworkManager.Migration.cs`、`scripts/NetworkSessionModels.cs`，以及 `tests/LanFullMatchPeer.gd`、`tests/run_lan_host_migration_smoke.sh`。
- Wave 7：`scripts/MultiplayerLobbyScene.cs` 的迁移失败 toast、`NetworkManager.Lobby.cs` 的 lobby claim/epoch 回退、失败时的 pending lobby error handoff，以及旧-token 场景的候选进程清理。
- Wave 0–3 已分别由既有提交覆盖：本地化基线、玩法 manifest、冒险模式/进度和完整 LAN 牌局收敛。

### 当前验收证据

在 Godot `4.6.1-stable.mono`、项目 `Deckland` 环境下：

- `dotnet build pokegame.csproj --no-restore`：通过，0 warning，0 error；
- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`：`314 passed, 0 failed`；
- Godot `ItemCardManifestSync.gd --check`：`ITEM_CARD_MANIFEST_CHECK_OK`；
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh`：`MULTIPLAYER_RECONNECT_SMOKE_SUITE_OK`；
- `bash tests/run_lan_full_match_smoke_suite.sh`：2p standard、3p items、4p items 全部通过，最终 `LAN_FULL_MATCH_SMOKE_SUITE_OK`；
- `bash tests/run_lan_host_migration_smoke.sh`：标准 2p/3p/4p、active lobby、current turn、pending target/discard/attachment、visual kill、stale epoch、old host token、candidate exit、wrong hash、bind timeout 全部通过，最终 `LAN_HOST_MIGRATION_FAILURE_SUITE_OK`；
- Godot MCP/编辑器连接已验证可用：项目可读，版本 `4.6.1-stable`，编辑器错误为 0。

当前自动化均为同一台机器上的多进程 LAN/ENet 验证。按当前执行范围决策，不继续两台真实 LAN 设备之间的 Host `kill -9` 现场验收；该项不影响其余 P0 实现、自动化门禁和文档交付，但不能把本机多进程结果升级为真实设备证据。

`tests/REAL_LAN_HOST_MIGRATION_MANUAL_CHECKS.md` 保留为后续独立现场参考，不作为本轮执行项；该 runbook 只提供可复现的现场步骤，不把同机多进程结果升级为真实设备证据。
