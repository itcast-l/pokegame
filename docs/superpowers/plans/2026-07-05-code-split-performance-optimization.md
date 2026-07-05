# Large Code Split And Animation-Safe Performance Optimization Plan

## Summary

- Use a conservative one-pass implementation split into small commits by subsystem.
- Keep animation visually equivalent: do not change asset paths, frame counts, animation durations, exported scene parameters, particle counts, bubble counts, or redraw FPS.
- Target no regressions plus lower measured spikes: existing smokes must pass, optional FX warmup peaks should drop, and image-based menu bubbles should avoid Canvas redraw fallback when assets exist.
- Do not add dependencies, rewrite shaders, or rebuild the asset pipeline.

## Key Changes

- Split `scripts/GameBoardScene.AdventureUpgrades.cs`.
  - Move upgrade selection overlay, card reveal/hover motion, and selection session methods into `scripts/GameBoardScene.AdventureUpgradeSelectionOverlay.cs`.
  - Move offer building and grant flow into `scripts/GameBoardScene.AdventureUpgradeOffers.cs`.
  - Keep battle effect application flow in the original file.

- Split `scripts/GameBoardScene.cs`.
  - Move exported properties into `scripts/GameBoardScene.Exports.cs`.
  - Move player view slot and seat mapping helpers into `scripts/GameBoardScene.PlayerViewSlots.cs`.
  - Move network payload reader helpers into `scripts/GameBoardScene.NetworkPayloadReaders.cs`.
  - Keep `_Ready`, `InitializeBoard`, and core field declarations in the original file.

- Split `scripts/UnderwaterAmbientLayer.cs`.
  - Move image-based asset loading, layers, instances, and updates into `scripts/UnderwaterAmbientLayer.Images.cs`.
  - Move procedural fallback drawing into `scripts/UnderwaterAmbientLayer.ProceduralDraw.cs`.
  - Preserve existing exported properties and fallback behavior.

- Split `scripts/MainMenuOceanDynamics.cs`.
  - Move image-based asset loading, layers, instances, and updates into `scripts/MainMenuOceanDynamics.Images.cs`.
  - Move procedural fallback drawing into `scripts/MainMenuOceanDynamics.ProceduralDraw.cs`.
  - Add image-based bubble instances using the existing `BubbleTexturePath`, `bubble_count`, and motion formula, so ImageBased mode uses `TextureRect` updates instead of `_Draw()` when bubble texture exists.
  - Keep procedural bubble drawing only as missing-texture fallback.

- Split `scripts/GameAudioManager.cs`.
  - Move SFX player pool and SFX loading into `scripts/GameAudioManager.Sfx.cs`.
  - Move music, ambience, and tension layer handling into `scripts/GameAudioManager.Music.cs`.
  - Move UI button/range binding into `scripts/GameAudioManager.UiBinding.cs`.
  - Move disposal and teardown helpers into `scripts/GameAudioManager.Disposal.cs`.
  - Preserve all current public methods and signal binding behavior.

## Performance Work

- Update `scripts/GameBoardScene.ResourceWarmup.cs`.
  - Split `WarmupOptionalFx:CoreGameplay`, `WarmupOptionalFx:ItemMode`, and `WarmupOptionalFx:AdventureMode` into one synchronous load step per FX group.
  - Keep using `WaitForWarmupFrameAsync` between groups to reduce one-frame spikes.

- Update `scripts/GameBoardScene.Assets.cs`.
  - Add private per-FX loaded flags for optional FX groups.
  - Keep existing `Ensure*ResourcesLoaded()` methods able to load all needed resources.
  - Use grouped loading only from the warmup path.

- Keep `scripts/FrameAnimationHelpers.cs` cache behavior.
  - Do not add a new async loading framework.
  - Do not change first-frame selection or tween timing.

- Leave hand/table render skip logic and pools alone.
  - `RenderPlayerHands` and `RenderTableCards` already use signatures and pooling.
  - Use smoke tests to guard against regressions instead of refactoring these paths.

## Test Plan

- After each subsystem split, run:

```bash
dotnet build pokegame.csproj --no-restore
```

- Run rules regression:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

- Run board and animation baseline smokes:

```bash
bash tests/run_game_board_idle_performance_trace_smoke.sh
bash tests/run_game_board_backdrop_visibility_smoke.sh
bash tests/run_game_board_mode_regression_smoke.sh
```

- Run adventure upgrade smokes:

```bash
bash tests/run_adventure_upgrade_run_start_smoke.sh
bash tests/run_adventure_upgrade_overlay_display_failure_smoke.sh
bash tests/run_adventure_upgrade_victory_smoke.sh
bash tests/run_adventure_upgrade_big_capture_smoke.sh
```

- Add one dynamic-layer smoke test.
  - Instantiate main menu, multiplayer lobby, game board, and adventure map scenes.
  - Assert ImageBased dynamic layers exist.
  - Assert main menu bubble image nodes exist when bubble texture is available.
  - Advance several frames and assert image node frame or position changes.
  - Assert complete-image asset paths do not require procedural redraw fallback.

## Assumptions

- "Guarantee animation effects" means visual equivalence: no lower redraw FPS, no reduced `bubble_count`, `mote_count`, or `glint_count`, no asset swaps, and no tween duration changes.
- "One pass" means one branch or PR covers all targets, but implementation still uses small subsystem commits with verification after each.
- Performance target is not a fixed millisecond budget. Compare existing idle performance smoke output before and after; optional warmup peaks should drop and idle rendering should not regress.
