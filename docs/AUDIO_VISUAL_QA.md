# Audio/Visual QA Coverage

Last audited: 2026-05-31

This is a static QA pass for the card-table audio/visual layer. It checks whether each major gameplay action, item-card effect, and adventure avatar ability has:

- Animation or visible effect.
- SFX hook and available asset.
- Player-facing screen feedback.

Evidence used:

- `scripts/GameAudioManager.cs`
- `scripts/GameBoardScene.Audio.cs`
- `scripts/GameBoardScene.*Animation*.cs`
- `scripts/GameBoardScene.*Fx.cs`
- `resources/item_cards/*.tres`
- `resources/character_avatars/*.tres`
- `asset/audio/sfx/**`

Legend:

| Status | Meaning |
| --- | --- |
| OK | Animation/visual, SFX, and screen feedback are all present. |
| PARTIAL | Coverage exists, but one dimension is generic, missing a dedicated route, or still needs runtime confirmation. |
| GAP | Known missing or contradictory coverage. |
| N/A | Not active gameplay content or no effect is expected by design. |

## Core Actions

| Action | Animation / visual | SFX | Screen feedback | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| Opening deal | Opening deal card flights and intro flow | `opening_intro`, `card_deal` | Cards visibly enter the table/hand layout | OK | Covered by opening deal animation partials. |
| Card hover | Hand card hover state | `card_hover` | Hover response on hand cards | OK | SFX is cooldown-gated to prevent chatter. |
| Card pickup / select | Hand interaction and selection state | `card_pickup`, `base_card_enchant_sparkle_flash_take14`, `ui_invalid` | Selection, target-select, and invalid feedback | OK | Invalid actions have explicit SFX. |
| Card flight / land | Card flight animation to target positions | `card_flight`, `card_land` | Moving card and landing placement | OK | Used by normal play and table movement. |
| Capture start | Capture bite and capture flow animation | `capture_bite` | Capture begins with visible table-card response | OK | Triggered from card play capture path. |
| Capture lift / collect | Table capture lift and captured-cards-to-deck animation | `capture_lift`, `capture_collect` | Captured cards move into deck/score flow | OK | Core collection feedback is wired. |
| Draw card | Draw animation | `card_draw` | New card enters hand | OK | Also used by item draw card effect. |
| Deck shuffle | Deck shuffle frame animation | `deck_shuffle` | Deck shuffle visual | OK | Dedicated frame assets are loaded. |
| Table shuffle | Table-card shuffle animation | `table_shuffle` | Table cards fly/reposition around shuffle center | OK | Also used by Shuffle item card. |
| Score transfer | Score flight and arrival effects | `score_flight`, `score_arrive` | Score particles/labels move to score UI | OK | Core score feedback is present. |
| Pause/options panels | Pause and panel UI transitions | `ui_pause_open`, `ui_pause_close`, `ui_panel_open`, `ui_panel_close` | Panels open/close visibly | OK | UI audio manager auto-binds common controls. |
| Buttons/toggles/sliders | UI control states | `ui_button_click`, `ui_button_hover`, `ui_toggle_option`, `ui_slider_tick`, `ui_cancel`, `ui_target_select` | Press/hover/toggle/slider state | OK | Auto-bound recursively by `GameAudioManager`. |
| Scene transition | Circular transition | `ui_scene_transition` | Screen cover/reveal transition | OK | Triggered from scene transition helper. |
| Adventure basic attack | Avatar action, attack flight, impact pulse, damage effect frames | `avatar_attack`, `avatar_hit`, `adventure_attack_flight`, `adventure_attack_impact` | Attacker/target avatars animate; impact appears on target | OK | Generic attack coverage exists for adventure mode. |
| Adventure style attack | Per-avatar special attack visuals for configured styles | Some styles have dedicated SFX | Per-style attack visuals are visible | PARTIAL | Missing dedicated SFX routing for several configured attack styles; see Open Gaps. |
| Adventure ability trigger | Enemy ability avatar FX overlay and borrowed Boss K avatar FX | Some ability ids have dedicated SFX | Ability overlay plays on triggering enemy | PARTIAL | Missing dedicated SFX routing for several ability ids; see Open Gaps. |

## Item And Special Cards

| Card / rule | Active data | Animation / visual | SFX | Screen feedback | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Question Mark | Enabled, fixed reward card, `grant_reward` on collect | Reward transform, reveal, and insert animation | `question_reward_transform`, `question_reward_reveal_magic_take1`, `question_reward_insert` | Reward item appears or attachment flow begins | OK | Not playable from hand, as expected. |
| Bomb | Enabled, playable, reward, `place_table_pair_destroy` | Table overlay, pair lock, warning, explosion, card destroy visuals | `item_card_play`, `special_trigger`, `bomb_place`, `bomb_pair_lock`, `bomb_warning`, `bomb_explosion`, `bomb_destroy_card` | Bomb pair and destroyed cards are visible | OK | Strongest complete special-card coverage. |
| Magnet | Enabled, reward, attachment, not playable from hand | Attachment overlay, target selection/countdown, extra-capture reveal | `magnet_attach`, `magnet_extra_capture`, target/invalid UI SFX | Attached modifier and extra captured card are visible | OK | Not playable from hand by data; reward attachment path is covered. |
| Lens | Enabled, playable, reward, `reveal_cards` | Lens split/scan/reveal visuals | `item_card_play`, `special_trigger`, `lens_scan`, `lens_reveal` | Revealed face-down cards become visible | OK | Dedicated split presentation exists. |
| Double | Enabled, playable, reward, attachment, `double_collect` | Attachment overlay and collect feedback | `item_card_play`, `special_trigger`, `double_collect` | Score multiplier is reflected in collection/score flow | OK | Uses attachment overlay plus score feedback. |
| Overtime | Enabled, playable, reward, attachment, `extra_turn` | Attachment overlay and extra-turn trigger | `item_card_play`, `special_trigger`, `extra_turn` | Turn does not complete when triggered | OK | Visual state depends on attached card feedback. |
| Draw Card | Enabled, playable, reward, `draw_card` | Transform-area trigger plus draw animation | `item_card_play`, `special_trigger`, `item_draw_card`, `card_draw` | New card enters hand | OK | Effect resolver and draw animation are both wired. |
| Shuffle | Enabled, playable, reward, `shuffle_table` | Transform-area trigger plus table shuffle animation | `item_card_play`, `special_trigger`, `table_shuffle` | Table order visibly changes | OK | Uses core table shuffle animation. |
| Greed | Disabled, not playable, `EffectType` empty | Item art frames can load, but effect is not implemented | None dedicated | Not expected to appear in active play | PARTIAL | Runtime reward list filters by `EnabledInGame`, so it should not be rewarded. Data still has `CanBeQuestionReward = true`, which is confusing. |
| Runtime Jack | Disabled legacy runtime rule, `collect_table` | Uses existing card/table capture feedback when invoked | Core capture/collect SFX; no dedicated Jack SFX | Table collect behavior is visible when runtime rule is used | PARTIAL | Not in active item-card pool. Add a dedicated presentation only if court-card specials become player-facing item cards. |
| Runtime Queen | Disabled legacy runtime rule, `steal_card` | Targeted trigger/steal feedback | `steal_card` | Stolen card movement is visible | OK | Not in active item-card pool. |
| Runtime King | Disabled legacy runtime rule, `steal_deck_cards` | Targeted trigger/steal feedback | `steal_card` | Stolen deck-card movement is visible | OK | Not in active item-card pool. |

## Adventure Avatars

Generic adventure coverage:

- Avatar action SFX: `avatar_score`, `avatar_attack`, `avatar_hit`, `avatar_victory`, `avatar_defeat`.
- Generic attack SFX: `adventure_attack_flight`, `adventure_attack_impact`.
- Ability visual: `PlayEnemySpecialAbilityAvatarFxForAbilityAsync` plays enemy avatar FX and optional Boss K borrowed-avatar FX.

Dedicated SFX routing currently exists for:

- Attack styles: `fish_beam`, `crab_pincer`, `jellyfish_tentacle`, `manta_ray_cyclone`, `sea_slug_marker`, `garden_eel_burrow`, `leafy_seadragon_slash`, `dragon_prince_hit`, `dragon_queen_jaw`, `boss_k_royal_seizure`.
- Ability ids: `tide_guard`, `bubble_cover`, `sand_ambush`, `color_current`, `spiral_shell`, `tusk_break`, `tide_shift`, `leafy_mirage`, `dragon_prince`, `dragon_queen`, `dragon_king`.

| Avatar | Ability id(s) | Attack visual | Ability visual | Dedicated SFX coverage | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Cat / stage 0 | None | Generic cat damage effect | N/A | Generic avatar/attack SFX | N/A | Player/default avatar, not an adventure enemy stage. |
| Fish / stage 1 | None | `fish_beam` style and fish damage frames | N/A | `fish_beam` plus generic attack SFX | OK | No ability expected. |
| Lanternfish / stage 2 | None | Lanternfish damage frames, generic attack flight | N/A | Generic attack SFX only | OK | No ability expected; unique attack SFX is optional. |
| Shrimp / stage 3 | None | Shrimp damage frames, generic attack flight | N/A | Generic attack SFX only | OK | No ability expected; unique attack SFX is optional. |
| Crab / stage 4 | `tide_guard` | `crab_pincer` style | Enemy ability overlay | `tide_guard`, `crab_pincer` | OK | Dedicated generated SFX are routed for both ability and attack style. |
| Jellyfish / stage 5 | `bubble_cover` | `jellyfish_tentacle` style | Enemy ability overlay | `bubble_cover`, `jellyfish_tentacle` | OK | Dedicated generated SFX are routed for the attack style. |
| Manta Ray / stage 6 | `tide_shift` | `manta_ray_cyclone` style | Enemy ability overlay | `manta_ray_tide_shift` covers ability/style | OK | Dedicated audio path exists. |
| Garden Eel / stage 7 | `sand_ambush` | `garden_eel_burrow` style | Enemy ability overlay | `garden_eel_ambush` covers ability/style | OK | Dedicated audio path exists. |
| Sea Slug / stage 8 | `color_current` | `sea_slug_marker` style | Enemy ability overlay | `color_current`, `sea_slug_marker` | OK | Dedicated generated SFX are routed for both ability and attack style. |
| Nautilus / stage 9 | `spiral_shell` | Nautilus damage frames, generic attack flight | Enemy ability overlay | `nautilus_spiral_shell` ability SFX; generic attack SFX | OK | No explicit attack style is configured. |
| Narwhal / stage 10 | `tusk_break` | Narwhal attack FX root and damage frames | Enemy ability overlay | `narwhal_tusk_break` ability SFX; generic attack SFX | OK | No explicit attack style is configured. |
| Leafy Seadragon / stage 11 | `leafy_mirage` | `leafy_seadragon_slash` style | Enemy ability overlay | `leafy_seadragon_blade` covers ability/style | OK | Dedicated audio path exists. |
| Dragon Prince J / stage 12 | `dragon_prince` | `dragon_prince_hit` style | Enemy ability overlay and special Jack-related flow | `dragon_prince`, `dragon_prince_hit` | OK | Dedicated generated SFX are routed for both ability and attack style. |
| Dragon Queen Q / stage 13 | `dragon_queen` | `dragon_queen_jaw` style | Enemy ability overlay and steal flow | `dragon_queen`, `dragon_queen_jaw`; `steal_card` remains for card movement | OK | Dedicated generated SFX cover the ability identity; steal-card SFX still supports the card movement. |
| Boss K / stage 14 | `tide_guard`, `bubble_cover`, `tide_shift`, `sand_ambush`, `spiral_shell`, `tusk_break`, `leafy_mirage`, `dragon_prince`, `dragon_queen`, `dragon_king` | `boss_k_royal_seizure` style | Boss ability overlay and borrowed-avatar FX where eligible | All configured ability ids plus `boss_k_royal_seizure` | OK | Static routing is complete; runtime mix QA is still required because Boss K can stack strong ability layers. |

## Open Gaps

| Priority | Gap | Concrete next step |
| --- | --- | --- |
| P1 | Newly generated adventure SFX have static routes, but their loudness, timing, and tonal fit have not been runtime-auditioned across stages 4-14. | Run the adventure stages with headphones/speakers, adjust `volumeDb`, pitch ranges, cooldowns, or regenerate any weak sounds. |
| P2 | `greed.tres` is disabled and filtered out of rewards, but still has `CanBeQuestionReward = true` and an empty `EffectType`. | Either set `CanBeQuestionReward = false` for clarity, or implement the Greed effect before enabling it. |
| P2 | Static coverage does not prove mix, timing, or runtime layering. | Run a Godot QA pass for main menu, local match, each item card, and adventure stages 4-14 while checking logs for missing stream warnings. |
| P2 | Automated coverage only checks generated SFX files exist; it does not yet compare all configured ability/style ids against routing. | Add a lightweight catalog smoke check that reports configured ability/style ids without dedicated SFX mapping. |

## Runtime QA Checklist

Use this checklist after any SFX routing or asset changes:

- Start main menu, open/close settings, pause, player count dialog, and avatar unlock prompt; confirm UI SFX are not duplicated.
- Play a local match with item cards enabled; trigger Question Mark, Bomb, Magnet, Lens, Double, Overtime, Draw Card, and Shuffle.
- In adventure mode, test stages 4-14 because these are the ability-bearing enemies.
- Confirm generic attack flight/impact still plays when a stage has no dedicated attack style.
- Confirm Boss K borrowed-avatar FX and ability SFX do not stack into harsh volume spikes.
- Watch Godot output for missing SFX/music resource warnings from `GameAudioManager`.
