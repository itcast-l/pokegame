# Deckland

Deckland is a Godot 4.6 C# card battler prototype with local card-table play, item-card effects, character avatars, and an early adventure progression flow.

## Current State

- Main scene: `res://scenes/main_menu_scene.tscn`
- Core gameplay scene: `res://scenes/game_board_scene.tscn`
- Local play supports 2-4 players, optional item mode, and configurable winning score.
- Main-menu multiplayer opens a prototype lobby with LAN host/search, ready/start flow, and Steam lobby hooks when Steam is available.
- LAN turn sync, reconnect, AI takeover, and pending-choice fallback flows have automated smoke coverage.
- Adventure currently uses a linear enemy ladder: select the next available enemy, win a 2-player adventure battle, then unlock the next enemy.
- Character avatar selection and adventure unlock progress are stored in SaveState profile slots, with legacy `user://settings.cfg` migration.
- Item-card definitions are data-driven through Pandora data in `data.pandora`, with fallback definitions in `scripts/ItemCardCatalog.cs`.
- Localization currently has complete strings for `zh_CN`, `zh_TW`, and `en`.

Known unfinished areas are tracked in [docs/ROADMAP.md](docs/ROADMAP.md).

## Requirements

- Godot 4.6 with .NET/C# support
- .NET SDK compatible with the Godot C# toolchain

Enabled addons include Pandora, Phantom Camera, Resonate, SaveState Lite, GodotSteam, and local MCP tooling. SaveState Lite vendor documentation has been moved to `docs/vendor/savestate-lite/`; it is retained as third-party reference material, not as the root project identity.

## Build Check

From the project root:

```sh
dotnet build pokegame.csproj
```

To run the game, open this folder in Godot and run the configured main scene.

## Project Layout

- `project.godot` - Godot project settings and autoloads.
- `scenes/` - Godot scenes for the menu, game board, cards, avatars, and adventure UI.
- `scripts/` - C# gameplay, UI, animation, localization, and catalog code.
- `art/` - card, character, UI, map, and effect assets.
- `asset/audio/` - audio assets.
- `localization/` - translation CSV and generated translation imports.
- `docs/` - project documentation and retained third-party notes.

## Notes

The previous root README, quickstart, changelog, license, and GitHub release workflow described SaveState Lite addon publishing. They have been isolated under `docs/vendor/savestate-lite/` so future project tags do not accidentally publish a SaveState Lite zip.
