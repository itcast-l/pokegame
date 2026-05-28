# Quick Start

## Open And Run

1. Install Godot 4.6 with .NET support.
2. Open this folder as a Godot project.
3. Run the configured main scene, `res://scenes/main_menu_scene.tscn`.

## Local Build Check

```sh
dotnet build pokegame.csproj
```

## Main Flows

- `Solo` opens the player-count dialog and starts a local 2-4 player match.
- `Adventure` opens the enemy/avatar selection screen and starts a 2-player adventure battle.
- `Settings` controls master volume, music volume, mute, window mode, language, ocean motion, and motion scale.

`Multi` is currently a visible placeholder and is not wired to a multiplayer flow yet.

## Useful Files

- Gameplay entry: `scripts/GameBoardScene.cs`
- Main menu entry: `scripts/MainMenuScene.cs`
- Adventure enemy selection: `scripts/AdventureAvatarMenuScene.cs`
- Item-card catalog: `scripts/ItemCardCatalog.cs`
- Special-card architecture: `docs/SPECIAL_CARD_SYSTEM.md`
- Roadmap: `docs/ROADMAP.md`
