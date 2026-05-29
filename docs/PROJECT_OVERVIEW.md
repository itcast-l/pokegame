# Project Overview

`Deckland` is a Godot 4.6 C# card battler prototype. The current playable slice centers on a local card-table match with animated character avatars, item-card effects, and an underwater-themed first region.

## Implemented

- Main menu with single-player setup, adventure entry, settings, avatar editing, and exit confirmation.
- Local 2-4 player card-table game.
- Configurable item-card mode and winning score.
- Score meters, winner banner, pause menu, replay, and return-to-menu flow.
- Character avatar catalog, selected avatar persistence, and adventure unlock progress.
- Data-driven item-card definitions through Pandora with fallback C# defaults.
- Special-card resolver architecture documented in `docs/SPECIAL_CARD_SYSTEM.md`.
- Runtime localization for Simplified Chinese, Traditional Chinese, and English.

## Partial

- Adventure mode currently enters enemy selection and then starts a battle. A separate adventure map scene exists but is not connected to the main flow.
- SaveState Lite is installed as an addon, but current game settings and adventure progress use `ConfigFile` and `user://settings.cfg`.
- Resonate is installed and menu music is wired; gameplay sound effects still need implementation.

## Not Yet Implemented

- Real multiplayer flow behind the main-menu multiplayer button.
- Adventure map data, node selection, route progression, and battle return flow.
- Release/export automation for game builds.
- Final game license and distribution packaging decisions.
