# Roadmap

## Short Term

- Disable or implement the main-menu multiplayer button so it no longer behaves like a dead entry.
- Connect adventure progression to `scenes/adventure_map_scene.tscn` or remove the unused map scene until the flow is ready.
- Add keyboard/controller focus to the main menu, adventure enemy list, settings rows, and dialog controls.
- Add menu and gameplay sound effects through Resonate.
- Run the main menu, local match, adventure enemy selection, pause menu, and winner banner in Godot and capture runtime issues.

## Medium Term

- Convert repeated card render rebuilds to pooled or diffed UI nodes.
- Add an explicit game-save strategy: keep settings in `ConfigFile`, but move progression/save slots to SaveState or document why not.
- Limit visible language choices to languages with real translations or clearly document fallback behavior.
- Add release/export workflow for the game, separate from any vendor addon workflows.

## Longer Term

- Finish adventure map data, route UI, node rewards, unlock animations, and return-to-map flow.
- Decide the multiplayer direction: local shared-device only, Steam multiplayer, or no multiplayer for the first public build.
- Add automated smoke checks for catalog loading, localization loading, and core gameplay state transitions.
