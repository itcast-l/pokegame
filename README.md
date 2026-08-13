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

Active priorities and open follow-up work are tracked in [docs/ROADMAP.md](docs/ROADMAP.md).

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

For the current multiplayer regression baseline, also run:

```sh
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

Steam reconnect validation remains a manual dual-account check. Use:

```sh
bash tests/run_steam_reconnect_manual_host.sh
CODEX_STEAM_LOBBY_ID=<lobbyId> bash tests/run_steam_reconnect_manual_client.sh
```

For local Steam testing, keep `project.godot` `steam/initialization/app_id` on a valid test or shipping app id instead of `0`.

## Project Layout

- `project.godot` - Godot project settings and autoloads.
- `scenes/` - Godot scenes for the menu, game board, cards, avatars, and adventure UI.
- `scripts/` - C# gameplay, UI, animation, localization, and catalog code.
- `art/` - card, character, UI, map, and effect assets.
- `asset/audio/` - audio assets.
- `localization/` - translation CSV and generated translation imports.
- `docs/` - project documentation and retained third-party notes.

## Documentation

- Roadmap: [docs/ROADMAP.md](docs/ROADMAP.md)
- Resource baseline: [docs/RESOURCE_BASELINE.md](docs/RESOURCE_BASELINE.md)
- Steam reconnect manual regression: [docs/STEAM_RECONNECT_REGRESSION.md](docs/STEAM_RECONNECT_REGRESSION.md)
- Multiplayer authority model: [docs/NETWORK_AUTHORITY_MODEL.md](docs/NETWORK_AUTHORITY_MODEL.md)
- Card-effect sync requirements: [docs/CARD_EFFECT_SYNC_REQUIREMENTS.md](docs/CARD_EFFECT_SYNC_REQUIREMENTS.md)
- Card-effect verification gate: [docs/CARD_EFFECT_VERIFICATION_GATE.md](docs/CARD_EFFECT_VERIFICATION_GATE.md)

## Notes

The previous root README, quickstart, changelog, license, and GitHub release workflow described SaveState Lite addon publishing. They have been isolated under `docs/vendor/savestate-lite/` so future project tags do not accidentally publish a SaveState Lite zip.
