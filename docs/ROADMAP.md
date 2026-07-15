# Roadmap

Last updated: 2026-07-15

当前项目的优先事项不再靠零散口头同步维护，统一以以下文档为入口。

## Current Execution Plan

- [P0_GAMEPLAY_NETWORK_FOUNDATION_EXECUTION_PLAN.md](./P0_GAMEPLAY_NETWORK_FOUNDATION_EXECUTION_PLAN.md)

## Multiplayer Baseline Docs

- [RESOURCE_BASELINE.md](./RESOURCE_BASELINE.md)
- [STEAM_RECONNECT_REGRESSION.md](./STEAM_RECONNECT_REGRESSION.md)
- [NETWORK_AUTHORITY_MODEL.md](./NETWORK_AUTHORITY_MODEL.md)
- [CARD_EFFECT_SYNC_REQUIREMENTS.md](./CARD_EFFECT_SYNC_REQUIREMENTS.md)
- [CARD_EFFECT_VERIFICATION_GATE.md](./CARD_EFFECT_VERIFICATION_GATE.md)

执行 Steam 相关回归前，先确认 `project.godot` 中的 `steam/initialization/app_id` 不是 `0`。

## Current Non-Goals

以下事项当前仍明确不在这轮高优先级范围内：

- Steam 商店页与发版素材
- 成就、云存档、正式发版 CI
- 新卡牌内容扩展
- 大规模 UI polish
- 非阻塞性美术重做
