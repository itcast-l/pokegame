# Special Card System

## Architecture

Special cards are data-driven. Pandora owns card data and configuration only:

```text
Pandora item_cards
  -> ItemCardCatalog
  -> CardEventBus
  -> SpecialCardEffectResolver
  -> GameBoardScene runtime adapters
  -> existing rule/state/render methods
```

`GameBoardScene` still owns visual animation and live table/player state. Card data never controls scene nodes directly.

## Pandora Schema

Every `item_cards` entity supports this normalized structure:

| Field | Purpose |
| --- | --- |
| `id` | Stable card id. |
| `name` | Display name/config name. |
| `description` | Human-readable description or localization key. |
| `description_key` | Existing localization key used by UI. |
| `rarity` | Card pool rarity such as `common`, `uncommon`, `rare`. |
| `tags` | Comma-separated tags, for example `fixed,reward,attachment`. |
| `trigger` | Event name, for example `on_play`, `on_collect`, `on_draw`. |
| `effect_type` | Resolver operation, for example `draw_card`, `attach_modifier`. |
| `effect_values` | Parameters as `key=value;key=value`. |
| `priority` | Higher resolves earlier when multiple cards trigger together. |
| `icon` | Optional icon path or key. |
| `weight` | Card-pool weight. |
| `sprite_name`, `art_run`, `frame_count`, `sort_order` | Existing art/loading fields. |
| `enabled_in_game`, `playable_from_hand`, `can_be_question_reward` | Existing availability fields. |

Current effect data:

| Card | trigger | effect_type | effect_values |
| --- | --- | --- | --- |
| Question Mark | `on_collect` | `grant_reward` | empty |
| Bomb | `on_play` | `place_table_pair_destroy` | `pair_tag=bomb` |
| Magnet | `on_play` | `attach_modifier` | `modifier=magnet` |
| Lens | `on_play` | `reveal_cards` | `target=opponent_table_face_down;count=2` |
| Double | `on_play` | `attach_modifier` | `modifier=double` |
| Overtime | `on_play` | `attach_modifier` | `modifier=overtime` |
| Draw Card | `on_play` | `draw_card` | `count=1` |
| Shuffle | `on_play` | `shuffle_table` | `target=table` |

Legacy J/Q/K rules are registered as runtime definitions in `ItemCardCatalog` so their old behavior is preserved without adding them to the special-card art pool.

## Event Flow

1. Gameplay publishes a `CardGameEvent`.
2. `CardEventBus` asks `ICardTriggerProvider` for matching card definitions.
3. Matching `CardTriggerRequest`s are sorted by `priority`, then insertion order.
4. `SpecialCardEffectResolver` resolves `effect_type`.
5. Resolver calls runtime adapters on `GameBoardScene` for state changes and existing animations.
6. Effects can publish follow-up events through the same queue when future combo rules need it.

Supported event names:

`on_play`, `on_match`, `on_collect`, `on_round_start`, `on_round_end`, `on_draw`, `on_shuffle`, `on_discard`, `on_empty_deck`.

## Resolver Contract

Add a new card by adding or reusing an `effect_type`; do not branch on `card.id`.

Implemented resolver types:

`draw_card`, `steal_card`, `steal_deck_cards`, `collect_table`, `place_table_pair_destroy`, `attach_modifier`, `reveal_cards`, `shuffle_table`, `double_collect`, `destroy_cards`, `extra_turn`, `block_match`, `reverse_collect`, `grant_reward`.

If a new card can be represented by an existing `effect_type`, only add a Pandora entity with different `effect_values`, `priority`, `rarity`, `tags`, and `weight`.

## Priority, Chain, and Recursion Rules

`priority` is descending: larger values resolve first. Ties use queue insertion order.

`CardEventBus` limits:

- max queued triggers: 128
- max chain depth: 16
- max same trigger resolutions: 4

`GameBoardScene` also deduplicates the same runtime request inside one event pass by card id, trigger, effect type, source table index, and source hand index.

These guards prevent recursive loops while still allowing multi-card and chained interactions.

## Class Responsibilities

| Class/File | Responsibility |
| --- | --- |
| `ItemCardCatalog` | Reads Pandora data, applies fallback defaults, exposes `ItemCardDefinition`. |
| `SpecialCardSystem.cs` | Event names, effect names, effect values parser, event bus, trigger request models. |
| `GameBoardScene.SpecialCards.cs` | Runtime trigger provider and event publishing bridge. |
| `GameBoardScene.EffectResolver.cs` | Data-driven effect execution entry points. |
| Existing `GameBoardScene.*` partials | Existing animations, table state, player hands/decks, scoring, targeting, AI flow. |

## Adding a Card

1. Add a Pandora `item_cards` entity.
2. Fill the normalized fields.
3. Use an existing `effect_type` if possible.
4. If a new operation is needed, add one resolver branch in `SpecialCardEffectResolver` and a runtime adapter method in `GameBoardScene.EffectResolver.cs`.
5. Do not create a script per card and do not control UI nodes from card data.

## Multiplayer/AI Extension

The runtime event object already carries actor, source hand index, source table index, target player, target hand index, capture start, and origin position. For networking, serialize `CardGameEvent` plus random seeds/selected targets; keep visuals client-side and resolver state changes deterministic.
