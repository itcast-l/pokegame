# Project Overview

`Deckland` is a Godot 4.6 C# card battler prototype. The current playable slice centers on a local card-table match, a prototype multiplayer lobby flow, animated character avatars, item-card effects, and an underwater-themed first region.

## Implemented

- Main menu with single-player setup, adventure entry, multiplayer lobby entry, settings, avatar editing, and exit confirmation.
- Local 2-4 player card-table game.
- Prototype multiplayer lobby flow with LAN host/search, room join, ready/start flow, and Steam lobby hooks.
- LAN turn sync, reconnect, AI takeover, and pending-choice fallback logic with automated smoke coverage.
- Linear adventure enemy ladder: the first enemy is available, victories record progress, and the next enemy becomes available.
- Configurable item-card mode and winning score.
- Score meters, winner banner, pause menu, replay, and return-to-menu flow.
- Character avatar catalog, selected avatar persistence, adventure unlock progress, and SaveState-backed profile slots with legacy settings migration.
- Data-driven item-card definitions through Pandora with fallback C# defaults.
- Special-card resolver architecture documented in `docs/SPECIAL_CARD_SYSTEM.md`.
- Runtime localization for Simplified Chinese, Traditional Chinese, and English.

## Partial

- Adventure mode follows an enemy-selection ladder by design; the full 1-14 progression, persisted restart behavior, and final-stage completion state still need runtime QA.
- Multiplayer is in prototype shape, but Steam runtime/manual QA and lobby focus/controller QA still need coverage.
- Audio/visual coverage is broad, but several adventure ability ids and attack styles still use generic SFX or lack dedicated routing; see `docs/AUDIO_VISUAL_QA.md`.
- Windows export can produce a build, but the preset still exports all resources and needs release packaging cleanup.

## Not Yet Implemented

- Release/export automation for game builds.
- Final game license and distribution packaging decisions.
