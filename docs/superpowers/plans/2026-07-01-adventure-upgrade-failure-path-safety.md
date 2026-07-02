# Adventure Upgrade Failure Path Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make adventure upgrade selection safe under display failure, cancellation, and scene cleanup so rewards are never silently consumed and victory never resolves before the pending upgrade is actually completed.

**Architecture:** Keep the fix local to the current adventure upgrade flow inside `GameBoardScene` rather than broadening the scope into overlay refactors. Model upgrade selection completion explicitly, delay trigger consumption until successful confirmation, and treat external overlay cleanup as a cancellation path so async tasks cannot hang after UI removal.

**Tech Stack:** Godot 4.6 Mono, C#, existing `GameBoardScene` partial-class structure, GDScript smoke tests, existing debug hook pattern in `GameBoardScene.Debug.cs`

---

### Task 1: Make upgrade selection outcomes explicit

**Files:**
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`

- [ ] Add a private selection outcome enum with exactly these values:

```csharp
private enum AdventureUpgradeSelectionOutcome
{
	Confirmed = 0,
	Canceled = 1,
	DisplayFailed = 2,
}
```

- [ ] Add a private readonly result record with this exact shape:

```csharp
private readonly record struct AdventureUpgradeSelectionResult(
	AdventureUpgradeSelectionOutcome Outcome,
	int SelectedIndex);
```

- [ ] Change the completion field type from:

```csharp
private TaskCompletionSource<int>? _adventureUpgradeSelectionCompletionSource;
```

to:

```csharp
private TaskCompletionSource<AdventureUpgradeSelectionResult>? _adventureUpgradeSelectionCompletionSource;
```

- [ ] Keep these invariants everywhere in the file:

```text
Confirmed      => SelectedIndex >= 0
Canceled       => SelectedIndex == -1
DisplayFailed  => SelectedIndex == -1
```

- [ ] Run a compile-oriented check after this refactor step:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: the project still builds and rule tests still pass because this task only changes scene-side types.


### Task 2: Separate “overlay failed to show” from “waiting for choice”

**Files:**
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`

- [ ] Change `ShowAdventureUpgradeSelectionOverlay(...)` from `void` to `bool`.

- [ ] Return `false` on either of these conditions only:

```csharp
PackedScene? overlayScene = GD.Load<PackedScene>(AdventureUpgradeSelectionOverlayScenePath);
if (overlayScene?.Instantiate() is not Control selectionOverlay)
{
	GD.PushError($"Missing adventure upgrade selection overlay scene: {AdventureUpgradeSelectionOverlayScenePath}");
	return false;
}
```

- [ ] Keep the existing success-side behavior unchanged after successful instantiation:

```csharp
AddChild(selectionOverlay);
_adventureUpgradeSelectionOverlay = selectionOverlay;
_selectionOverlay = null;
PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
ConfigureAdventureUpgradeSelectionOverlay(selectionOverlay, title, subtitle, options);
return true;
```

- [ ] Rewrite `ShowAdventureUpgradeSelectionAsync(...)` so it follows this order exactly:

```text
1. Clear the existing overlay
2. Build overlayOptions
3. Build _selectionOverlayActions
4. Create TaskCompletionSource<AdventureUpgradeSelectionResult>
5. Try to show the overlay
6. If display failed: clear actions and return DisplayFailed immediately
7. If display succeeded: assign _adventureUpgradeSelectionCompletionSource and await it
8. After await, clear the field if it still points at the same completion source
```

- [ ] Use this exact failure return inside `ShowAdventureUpgradeSelectionAsync(...)`:

```csharp
return new AdventureUpgradeSelectionResult(
	AdventureUpgradeSelectionOutcome.DisplayFailed,
	-1);
```

- [ ] Preserve `TaskCreationOptions.RunContinuationsAsynchronously` on the completion source.


### Task 3: Centralize upgrade selection completion and cancellation

**Files:**
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`

- [ ] Add a private helper dedicated to completing the active upgrade selection task. Use this exact signature:

```csharp
private void ResolveAdventureUpgradeSelection(
	AdventureUpgradeSelectionResult result,
	bool clearOverlay)
```

- [ ] Implement the helper with these semantics:

```text
- Read the current _adventureUpgradeSelectionCompletionSource into a local variable
- Clear the field before resolving the task
- TrySetResult(result) on the local variable if it exists
- If clearOverlay is true, remove the overlay UI after resolving the task
```

- [ ] Update the upgrade button action closures inside `ShowAdventureUpgradeSelectionAsync(...)` so they no longer call `completionSource.TrySetResult(...)` directly. They must instead call:

```csharp
ResolveAdventureUpgradeSelection(
	new AdventureUpgradeSelectionResult(
		AdventureUpgradeSelectionOutcome.Confirmed,
		capturedIndex),
	clearOverlay: true);
```

- [ ] Rewrite `CancelAdventureUpgradeSelection()` so it does only one thing:

```csharp
ResolveAdventureUpgradeSelection(
	new AdventureUpgradeSelectionResult(
		AdventureUpgradeSelectionOutcome.Canceled,
		-1),
	clearOverlay: false);
```

- [ ] Do not leave any remaining direct `TrySetResult(...)` calls for the adventure upgrade completion source outside this helper.


### Task 4: Make external overlay cleanup cancel an active upgrade selection

**Files:**
- Modify: `scripts/GameBoardScene.TargetItemSelection.cs`

- [ ] Change the selection cleanup signature from:

```csharp
private void ClearSelectionOverlay()
```

to:

```csharp
private void ClearSelectionOverlay(bool cancelAdventureUpgradeSelection = true)
```

- [ ] At the top of `ClearSelectionOverlay(...)`, add this behavior:

```text
If cancelAdventureUpgradeSelection is true and an adventure upgrade selection completion source is active,
complete it as Canceled before removing the upgrade overlay node.
```

- [ ] Keep the normal node removal behavior for `_adventureUpgradeSelectionOverlay` and `_selectionOverlay` unchanged after that cancellation step.

- [ ] Update the one successful upgrade-confirm path in `scripts/GameBoardScene.AdventureUpgrades.cs` so that overlay removal uses:

```csharp
ClearSelectionOverlay(cancelAdventureUpgradeSelection: false);
```

- [ ] Leave all other existing callers of `ClearSelectionOverlay()` unchanged so they inherit the safe default behavior.

- [ ] Verify by inspection that these external cleanup paths now cancel instead of hanging:

```text
Deck reset
Item-card pending selection cleanup
Network predicted selection rollback
Adventure upgrade board reset
```


### Task 5: Delay trigger consumption until reward selection succeeds

**Files:**
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`

- [ ] Rewrite the main loop in `ResolvePendingAdventureUpgradeTriggersInternalAsync(...)` so it uses `Peek()` instead of `Dequeue()` at the start of each iteration.

- [ ] Replace the current sequence:

```text
Dequeue trigger
Mark trigger granted
Build offer
Show selection
Acquire upgrade
```

with this exact control flow:

```text
1. Peek current trigger
2. Build offer
3. If offer.Count == 0:
   - Dequeue trigger
   - Do not mark it granted
   - Continue
4. Show selection and await result
5. If outcome is Confirmed and index is valid:
   - Fetch selected definition
   - Mark trigger granted
   - Acquire upgrade
   - Print acquired log
   - Dequeue trigger
6. If outcome is Canceled or DisplayFailed:
   - Do not dequeue trigger
   - Do not mark it granted
   - Return from the resolver
```

- [ ] Move the only call to `MarkAdventureUpgradeTriggerAsGranted(...)` into the confirmed-selection branch.

- [ ] Preserve the current `offer.Count == 0` behavior as “skip this trigger because nothing is available,” but do not treat that as a granted reward.

- [ ] Preserve the existing guard checks around:

```text
turnSequenceGeneration match
_isLeavingGame
IsGameOver()
```

so the resolver remains compatible with the rest of the board state machine.


### Task 6: Add a one-shot debug hook to force upgrade overlay display failure

**Files:**
- Modify: `scripts/GameBoardScene.Debug.cs`
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`

- [ ] Add a new private debug field in `GameBoardScene.Debug.cs`:

```csharp
private bool _debugForceNextAdventureUpgradeOverlayDisplayFailure;
```

- [ ] Add this public method in `GameBoardScene.Debug.cs`:

```csharp
public void DebugForceNextAdventureUpgradeOverlayDisplayFailure()
{
	_debugForceNextAdventureUpgradeOverlayDisplayFailure = true;
}
```

- [ ] Add this private consume helper beside the existing `ConsumeDebugForce...` helpers:

```csharp
private bool ConsumeDebugForceNextAdventureUpgradeOverlayDisplayFailure()
{
	if (!_debugForceNextAdventureUpgradeOverlayDisplayFailure)
	{
		return false;
	}

	_debugForceNextAdventureUpgradeOverlayDisplayFailure = false;
	return true;
}
```

- [ ] In `ShowAdventureUpgradeSelectionOverlay(...)`, add this as the first guard:

```csharp
if (ConsumeDebugForceNextAdventureUpgradeOverlayDisplayFailure())
{
	return false;
}
```

- [ ] Do not add any production-only branching beyond this one-shot test hook.


### Task 7: Add smoke coverage for display-failure safety

**Files:**
- Create: `tests/AdventureUpgradeOverlayDisplayFailureSmoke.gd`
- Create: `tests/AdventureUpgradeVictoryDisplayFailureSmoke.gd`
- Create: `tests/run_adventure_upgrade_overlay_display_failure_smoke.sh`
- Create: `tests/run_adventure_upgrade_victory_display_failure_smoke.sh`

- [ ] Create `tests/run_adventure_upgrade_overlay_display_failure_smoke.sh` by mirroring the structure of:

```text
tests/run_adventure_upgrade_run_start_smoke.sh
```

and point it at:

```text
res://tests/AdventureUpgradeOverlayDisplayFailureSmoke.gd
```

with success token:

```text
ADVENTURE_UPGRADE_OVERLAY_DISPLAY_FAILURE_SMOKE_OK
```

- [ ] Create `tests/run_adventure_upgrade_victory_display_failure_smoke.sh` by mirroring:

```text
tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
```

and point it at:

```text
res://tests/AdventureUpgradeVictoryDisplayFailureSmoke.gd
```

with success token:

```text
ADVENTURE_UPGRADE_VICTORY_DISPLAY_FAILURE_SMOKE_OK
```

- [ ] In `tests/AdventureUpgradeOverlayDisplayFailureSmoke.gd`, set up a normal adventure run-start board, then call:

```gdscript
board.call("DebugForceNextAdventureUpgradeOverlayDisplayFailure")
```

before the upgrade flow resolves.

- [ ] Assert these exact safety conditions in that smoke:

```text
DebugGetAdventureAcquiredUpgradeCount() == 0
DebugGetPendingAdventureUpgradeTriggerCount() > 0
DebugIsAdventureUpgradeResolverBusy() eventually becomes false
DebugIsAdventureUpgradeSelectionVisible() does not remain stuck true
```

- [ ] In `tests/AdventureUpgradeVictoryDisplayFailureSmoke.gd`, build a near-victory adventure setup similar to the existing victory-order smoke, then force the next overlay display failure before the victory reward resolves.

- [ ] Assert these exact conditions in the victory-failure smoke:

```text
DebugGetAdventureAcquiredUpgradeCount() does not increase
DebugGetPendingAdventureWinnerPlayerIndex() remains >= 0
DebugGetPendingAdventureUpgradeTriggerCount() > 0
DebugGetGameLoopPhaseName() != "GameOver"
```

- [ ] Run the full verification set:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_adventure_upgrade_run_start_smoke.sh
bash tests/run_adventure_upgrade_big_capture_smoke.sh
bash tests/run_adventure_upgrade_victory_smoke.sh
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
bash tests/run_adventure_upgrade_overlay_display_failure_smoke.sh
bash tests/run_adventure_upgrade_victory_display_failure_smoke.sh
```

Expected: all commands pass.


## Assumptions

- `offer.Count == 0` means “nothing available to grant right now,” so that trigger may be dropped without being marked granted.
- `Canceled` and `DisplayFailed` both mean “reward not completed,” so the current trigger must remain queued.
- This issue belongs in scene-level smoke coverage, not `Rules.Tests`, because the root cause is async UI control flow in `GameBoardScene`.
- The default `ClearSelectionOverlay(cancelAdventureUpgradeSelection: true)` behavior is the minimum safe way to cover all existing external cleanup callers without first refactoring overlay ownership.
