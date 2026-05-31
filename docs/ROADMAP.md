# Roadmap

## Short Term

- Disable or implement the main-menu multiplayer button so it no longer behaves like a dead entry.
- Verify the linear adventure ladder in Godot: first enemy available, victory unlocks the next enemy, next-stage/replay/return buttons, persisted progress after restart, and final-stage completion behavior.
- Run keyboard/controller focus QA for the main menu, adventure enemy list, settings rows, dialog controls, pause menu, and winner banner.
- Clean the Windows export preset so release builds exclude editor tooling, MCP/server files, vendor docs, sample scenes, and unused development assets.
- Run the main menu, local match, adventure enemy selection, pause menu, winner banner, and adventure stages 1-14 in Godot and capture runtime issues.

## Medium Term

- Convert repeated card render rebuilds to pooled or diffed UI nodes.
- Limit visible language choices to languages with real translations or clearly document fallback behavior.
- Add release/export workflow for the game, separate from any vendor addon workflows.
- Add automated smoke checks for catalog loading, localization loading, SaveState profile migration, adventure progression, SFX routing coverage, and core gameplay state transitions.

## Longer Term

- Decide whether to keep `scenes/adventure_map_scene.tscn` as a future alternate mode or remove it from active project scope.
- Decide the multiplayer direction: local shared-device only, Steam multiplayer, or no multiplayer for the first public build.
