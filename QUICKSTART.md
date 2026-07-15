# Quick Start

## Open And Run

1. Install Godot 4.6 with .NET support.
2. Open this folder as a Godot project.
3. Run the configured main scene, `res://scenes/main_menu_scene.tscn`.

## Local Build Check

```sh
dotnet build pokegame.csproj
```

## Local Test Checks

```sh
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
```

## Main Flows

- `Solo` opens the player-count dialog and starts a local 2-4 player match.
- `Adventure` opens the enemy/avatar selection screen and starts a 2-player adventure battle.
- `Multi` opens the multiplayer lobby with LAN host/search, room join, ready/start flow, and Steam host/refresh when Steam is initialized.
- `Settings` controls master volume, music volume, mute, window mode, language, ocean motion, and motion scale.

## Useful Files

- Gameplay entry: `scripts/GameBoardScene.cs`
- Main menu entry: `scripts/MainMenuScene.cs`
- Adventure enemy selection: `scripts/AdventureAvatarMenuScene.cs`
- Item-card catalog: `scripts/ItemCardCatalog.cs`
- Card-effect synchronization contract: `docs/CARD_EFFECT_SYNC_REQUIREMENTS.md`
- Roadmap: `docs/ROADMAP.md`
