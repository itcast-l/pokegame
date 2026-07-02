# Task 6 Run Session Store + Task 7 Smoke Exit Leak Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move adventure-upgrade run state out of static globals into a persistent profile-scoped session store, then remove deterministic exit-time leak warnings from current `GameBoardScene` smoke tests without changing gameplay behavior.

**Architecture:** Task 6 introduces an `AdventureRunSessionStore` autoload that owns the current profile's adventure-upgrade run, persists it through `GameSaveStore`, and exposes explicit continue/restart behavior from the adventure menu. Task 7 adds a reusable verbose leak-assertion harness, hardens `GameAudioManager` immediate disposal, and makes `GameBoardScene` close transient selection UI during `_ExitTree()`.

**Tech Stack:** Godot 4.6.1 Mono, C#, GDScript smoke tests, shell harnesses, existing `GameSaveStore`, `AdventureAvatarMenuScene`, `GameAudioManager`, and `GameBoardScene` partial classes

---

## Task 6: Adventure-upgrade run session persistence

### Task 6.1: Replace static run state with instance state and a snapshot

**Files:**
- Create: `scripts/AdventureUpgradeRunSnapshot.cs`
- Modify: `scripts/AdventureUpgradeRunState.cs`
- Modify: `tests/Rules.Tests/Program.cs`
- Test: `tests/Rules.Tests/Rules.Tests.csproj`

- [ ] **Step 1: Add the snapshot model**

Create `scripts/AdventureUpgradeRunSnapshot.cs`:

```csharp
using System.Collections.Generic;

internal readonly record struct AdventureUpgradeRunSnapshot(
	int SchemaVersion,
	int CurrentAvatarStageNumber,
	int CurrentRunStageIndex,
	bool ShouldGrantRunStartUpgrade,
	bool ShouldGrantVictoryUpgrade,
	IReadOnlyList<string> AcquiredUpgradeIds)
{
	internal const int CurrentSchemaVersion = 1;
}
```

- [ ] **Step 2: Convert `AdventureUpgradeRunState` to an instance class**

Replace `internal static class AdventureUpgradeRunState` with `internal sealed class AdventureUpgradeRunState`. Keep these instance members:

```csharp
internal bool IsActive { get; private set; }
internal int CurrentAvatarStageNumber { get; private set; }
internal int CurrentRunStageIndex { get; private set; }
internal bool ShouldGrantRunStartUpgrade { get; private set; }
internal bool ShouldGrantVictoryUpgrade { get; private set; }
internal void BeginNewRun(int avatarStageNumber)
internal void AdvanceToStage(int avatarStageNumber)
internal void MarkRunStartUpgradeGranted()
internal void MarkVictoryUpgradeGranted()
internal void AcquireUpgrade(string upgradeId)
internal bool HasAcquiredUpgrade(string upgradeId)
internal IReadOnlyCollection<string> GetAcquiredUpgradeIds()
internal void Clear()
```

- [ ] **Step 3: Add snapshot conversion on the state object**

Add these methods to `AdventureUpgradeRunState`:

```csharp
internal AdventureUpgradeRunSnapshot ToSnapshot()
{
	return new AdventureUpgradeRunSnapshot(
		AdventureUpgradeRunSnapshot.CurrentSchemaVersion,
		CurrentAvatarStageNumber,
		CurrentRunStageIndex,
		ShouldGrantRunStartUpgrade,
		ShouldGrantVictoryUpgrade,
		new List<string>(AcquiredUpgradeIds));
}

internal void RestoreFromSnapshot(AdventureUpgradeRunSnapshot snapshot)
{
	Clear();
	if (snapshot.SchemaVersion != AdventureUpgradeRunSnapshot.CurrentSchemaVersion
		|| !AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(snapshot.CurrentAvatarStageNumber))
	{
		return;
	}

	IsActive = true;
	CurrentAvatarStageNumber = snapshot.CurrentAvatarStageNumber;
	CurrentRunStageIndex = AdventureUpgradeChoiceRules.ToRunStageIndex(snapshot.CurrentAvatarStageNumber);
	ShouldGrantRunStartUpgrade = snapshot.ShouldGrantRunStartUpgrade;
	ShouldGrantVictoryUpgrade = snapshot.ShouldGrantVictoryUpgrade;
	foreach (string upgradeId in snapshot.AcquiredUpgradeIds)
	{
		AcquireUpgrade(upgradeId);
	}
}
```

- [ ] **Step 4: Update rules tests to instantiate state**

Update `AdventureUpgradeRunStateTracksStageProgressionAndAcquiredIds()` so it starts with:

```csharp
var runState = new AdventureUpgradeRunState();
runState.BeginNewRun(avatarStageNumber: 3);
```

Then replace every `AdventureUpgradeRunState.` call in that test with `runState.`.

- [ ] **Step 5: Verify the instance conversion**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: all rules tests pass.

- [ ] **Step 6: Commit the run-state instance conversion**

```bash
git add scripts/AdventureUpgradeRunSnapshot.cs scripts/AdventureUpgradeRunState.cs tests/Rules.Tests/Program.cs
git commit -m "refactor: make adventure upgrade run state instance scoped"
```

### Task 6.2: Add profile-scoped autoload session store and persistence

**Files:**
- Create: `scripts/AdventureRunSessionStore.cs`
- Modify: `scripts/GameSaveStore.cs`
- Modify: `project.godot`
- Modify: `tests/Rules.Tests/Program.cs`

- [ ] **Step 1: Extend profile data with pending run JSON**

Add one field to `GameSaveStore.GameProgressData`:

```csharp
string PendingAdventureRunJson
```

Use this dictionary key:

```csharp
private const string ProgressPendingAdventureRunJsonKey = "pending_adventure_run_json";
```

Update `CreateDefault()`, `FromDictionary(...)`, `ToDictionary()`, `Normalize()`, and `ToMetadata(...)` call sites so the new field defaults to `string.Empty` and round-trips through profile saves.

- [ ] **Step 2: Create the session store autoload**

Create `scripts/AdventureRunSessionStore.cs`:

```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed partial class AdventureRunSessionStore : Node
{
	private readonly AdventureUpgradeRunState _runState = new();

	public bool HasActiveRun => _runState.IsActive;

	public override void _Ready()
	{
		ReloadFromActiveProfile();
	}

	internal AdventureUpgradeRunState GetRunState()
	{
		return _runState;
	}

	internal bool TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot)
	{
		if (!_runState.IsActive)
		{
			snapshot = default;
			return false;
		}

		snapshot = _runState.ToSnapshot();
		return true;
	}

	internal void BeginNewRun(int avatarStageNumber)
	{
		_runState.BeginNewRun(avatarStageNumber);
		Persist();
	}

	internal void AdvanceToStage(int avatarStageNumber)
	{
		_runState.AdvanceToStage(avatarStageNumber);
		Persist();
	}

	internal void MarkRunStartUpgradeGranted()
	{
		_runState.MarkRunStartUpgradeGranted();
		Persist();
	}

	internal void MarkVictoryUpgradeGranted()
	{
		_runState.MarkVictoryUpgradeGranted();
		Persist();
	}

	internal void AcquireUpgrade(string upgradeId)
	{
		_runState.AcquireUpgrade(upgradeId);
		Persist();
	}

	internal void ClearRun()
	{
		_runState.Clear();
		Persist();
	}

	internal void ReloadFromActiveProfile()
	{
		_runState.Clear();
		string json = GameSaveStore.LoadActiveProfile().PendingAdventureRunJson;
		if (string.IsNullOrWhiteSpace(json))
		{
			return;
		}

		try
		{
			AdventureUpgradeRunSnapshot snapshot = JsonSerializer.Deserialize<AdventureUpgradeRunSnapshot>(json);
			_runState.RestoreFromSnapshot(snapshot);
		}
		catch (Exception error)
		{
			GD.PushWarning($"Failed to restore adventure upgrade run session: {error.Message}");
			_runState.Clear();
		}
	}

	private void Persist()
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string json = _runState.IsActive
			? JsonSerializer.Serialize(_runState.ToSnapshot())
			: string.Empty;
		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAdventureRunJson = json,
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist adventure upgrade run session: {saveResult}");
		}
	}
}
```

- [ ] **Step 3: Register the autoload**

Add this line under `[autoload]` in `project.godot`:

```ini
AdventureRunSessionStore="*res://scripts/AdventureRunSessionStore.cs"
```

- [ ] **Step 4: Add persistence roundtrip coverage**

Add a rules test that creates a `GameProgressData`, stores a non-empty `PendingAdventureRunJson`, converts to dictionary, restores with `FromDictionary`, and asserts the JSON string is preserved.

- [ ] **Step 5: Verify profile persistence changes**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: all rules tests pass, including the new pending-run JSON roundtrip.

- [ ] **Step 6: Commit the session store and profile persistence**

```bash
git add scripts/AdventureRunSessionStore.cs scripts/GameSaveStore.cs project.godot tests/Rules.Tests/Program.cs
git commit -m "feat: persist adventure upgrade run sessions"
```

### Task 6.3: Rewire board runtime to the session store

**Files:**
- Modify: `scripts/GameBoardScene.cs`
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`
- Modify: `scripts/GameBoardScene.Layout.cs`
- Modify: `scripts/GameBoardScene.PauseMenu.cs`
- Modify: `scripts/GameBoardScene.Debug.cs`
- Modify: `scripts/AdventureAvatarMenuScene.Navigation.cs`

- [ ] **Step 1: Add a cached store accessor to `GameBoardScene`**

Add a nullable field:

```csharp
private AdventureRunSessionStore? _adventureRunSessionStore;
```

Add this helper near other autoload accessors:

```csharp
private AdventureRunSessionStore? GetAdventureRunSessionStore()
{
	return _adventureRunSessionStore ??= GetNodeOrNull<AdventureRunSessionStore>("/root/AdventureRunSessionStore");
}
```

- [ ] **Step 2: Replace board-side static run-state reads**

In `GameBoardScene.AdventureUpgrades.cs`, replace static state reads with:

```csharp
AdventureUpgradeRunState? runState = GetAdventureRunSessionStore()?.GetRunState();
```

If the store is missing, the upgrade system must not throw; it should behave as inactive and print a warning only at state-changing boundaries.

- [ ] **Step 3: Replace board-side static run-state writes**

Use only these store methods for mutation:

```csharp
GetAdventureRunSessionStore()?.BeginNewRun(avatarStageNumber);
GetAdventureRunSessionStore()?.AdvanceToStage(avatarStageNumber);
GetAdventureRunSessionStore()?.MarkRunStartUpgradeGranted();
GetAdventureRunSessionStore()?.MarkVictoryUpgradeGranted();
GetAdventureRunSessionStore()?.AcquireUpgrade(selectedDefinition.Id);
GetAdventureRunSessionStore()?.ClearRun();
```

- [ ] **Step 4: Preserve current initialization semantics**

`InitializeAdventureUpgradeRunForCurrentBoard()` must follow this exact behavior:

```text
- Non-main adventure stage: no run state changes.
- Store has no active run: begin a new run at current stage.
- Store has active run and current stage matches: keep it.
- Store has active run and current stage differs: advance to current stage.
```

- [ ] **Step 5: Preserve confirmed pause restart semantics**

In pause-menu restart, replace the current static `BeginNewRun(...)` call with:

```text
- Clear the current run.
- Begin a new run from the current stage.
- Deal a new match.
```

This intentionally does not return to stage 1 and does not preserve acquired upgrades.

- [ ] **Step 6: Clear saved run after final main adventure completion**

When the player completes the last main adventure stage and no next stage exists, clear the store before showing or after resolving the final result flow so returning to the adventure menu does not offer continue.

- [ ] **Step 7: Verify board runtime rewiring**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_adventure_upgrade_run_start_smoke.sh
bash tests/run_adventure_upgrade_big_capture_smoke.sh
bash tests/run_adventure_upgrade_victory_smoke.sh
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
```

Expected: all commands pass.

- [ ] **Step 8: Commit the board-store rewiring**

```bash
git add scripts/GameBoardScene.cs scripts/GameBoardScene.AdventureUpgrades.cs scripts/GameBoardScene.Layout.cs scripts/GameBoardScene.PauseMenu.cs scripts/GameBoardScene.Debug.cs scripts/AdventureAvatarMenuScene.Navigation.cs
git commit -m "refactor: route adventure upgrade runs through session store"
```

### Task 6.4: Add continue/restart choice to the adventure menu

**Files:**
- Create: `scripts/AdventureAvatarMenuScene.RunResumeDialog.cs`
- Modify: `scripts/AdventureAvatarMenuScene.cs`
- Modify: `scripts/AdventureAvatarMenuScene.Navigation.cs`
- Modify: `scripts/AdventureAvatarMenuScene.Ui.cs`
- Modify: `scripts/AdventureAvatarMenuScene.Localization.cs`
- Modify: `localization/ui.csv`

- [ ] **Step 1: Add menu state fields**

Add fields to `AdventureAvatarMenuScene`:

```csharp
private Control? _runResumeDialogRoot;
private Label? _runResumeTitleLabel;
private Label? _runResumeBodyLabel;
private Button? _runResumeContinueButton;
private Button? _runResumeRestartButton;
private Button? _runResumeCancelButton;
private bool _isShowingRunResumeDialog;
```

- [ ] **Step 2: Build a dedicated adventure-menu resume dialog**

Create `AdventureAvatarMenuScene.RunResumeDialog.cs` with helpers:

```csharp
private void EnsureRunResumeDialog()
private void ShowRunResumeDialog()
private void HideRunResumeDialog()
private void ContinueSavedAdventureRun()
private void RestartAdventureFromCurrentSelection()
private AdventureRunSessionStore? GetAdventureRunSessionStore()
```

The dialog is created in code as a child of `AdventureAvatarMenuScene`; do not reuse the main menu global dialog classes.

- [ ] **Step 3: Gate `StartAdventure()` through the dialog**

At the start of `StartAdventure()`:

```text
- If already starting, returning, or showing the dialog: return.
- If the store has an active run: show the continue/restart dialog and return.
- Otherwise keep the current start flow.
```

- [ ] **Step 4: Implement continue behavior**

`ContinueSavedAdventureRun()` must:

```text
- Read the saved snapshot from `AdventureRunSessionStore`.
- Resolve the avatar id from `snapshot.CurrentAvatarStageNumber`.
- Start the board scene for that avatar.
- Ignore the currently highlighted avatar.
- Leave saved acquired upgrades and grant flags intact.
```

- [ ] **Step 5: Implement restart behavior**

`RestartAdventureFromCurrentSelection()` must:

```text
- Call `ClearRun()`.
- Start the currently highlighted avatar through the normal new-run path.
- This works even if the highlighted item is tutorial or another non-main stage.
```

- [ ] **Step 6: Add localization keys**

Add these keys to `localization/ui.csv`:

```csv
ADVENTURE_RESUME_RUN_TITLE
ADVENTURE_RESUME_RUN_BODY
ADVENTURE_CONTINUE_RUN
ADVENTURE_RESTART_RUN
```

Use Simplified Chinese source copy:

```text
继续未完成的强化旅程？
检测到当前存档中有一条未完成的强化旅程。你可以继续它，或放弃它并从当前选择重新开始。
继续
重开
```

English fallback:

```text
Continue unfinished upgrade run?
This profile has an unfinished upgrade run. Continue it, or abandon it and restart from the current selection.
Continue
Restart
```

- [ ] **Step 7: Verify no pending-run regression**

Run:

```bash
bash tests/run_adventure_upgrade_run_start_smoke.sh
```

Expected: PASS when no pending run snapshot exists.

- [ ] **Step 8: Commit adventure-menu resume UI**

```bash
git add scripts/AdventureAvatarMenuScene.cs scripts/AdventureAvatarMenuScene.RunResumeDialog.cs scripts/AdventureAvatarMenuScene.Navigation.cs scripts/AdventureAvatarMenuScene.Ui.cs scripts/AdventureAvatarMenuScene.Localization.cs localization/ui.csv
git commit -m "feat: choose continue or restart for unfinished upgrade runs"
```

### Task 6.5: Add resume/restart smoke coverage

**Files:**
- Create: `tests/AdventureUpgradeRunResumeSmoke.gd`
- Create: `tests/run_adventure_upgrade_run_resume_smoke.sh`
- Modify: `tests/Rules.Tests/Program.cs`

- [ ] **Step 1: Add debug hooks if needed**

Expose only test-safe methods from `AdventureAvatarMenuScene` or `GameBoardScene`:

```csharp
public bool DebugIsAdventureRunResumeDialogVisible()
public bool DebugSelectAdventureRunResumeContinue()
public bool DebugSelectAdventureRunResumeRestart()
```

These methods must return `false` when the dialog is absent and must not be used by production code.

- [ ] **Step 2: Add the resume smoke script**

Create `tests/AdventureUpgradeRunResumeSmoke.gd` to cover:

```text
- Start a main adventure run.
- Select one upgrade.
- Return to menu-equivalent state while preserving the saved run.
- Enter adventure menu again.
- Assert the continue/restart dialog appears.
- Choose Continue.
- Assert acquired upgrade count is preserved after the fresh stage-level match starts.
```

- [ ] **Step 3: Add the restart path**

In the same smoke or a second path in the same script:

```text
- Seed a saved pending run.
- Open adventure menu.
- Choose Restart.
- Assert old acquired upgrades are cleared.
- Assert a fresh run starts from the currently highlighted stage.
```

- [ ] **Step 4: Add the shell wrapper**

Create `tests/run_adventure_upgrade_run_resume_smoke.sh` following the existing smoke wrapper pattern and require marker:

```text
ADVENTURE_UPGRADE_RUN_RESUME_SMOKE_OK
```

- [ ] **Step 5: Verify full Task 6 behavior**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_adventure_upgrade_run_resume_smoke.sh
bash tests/run_adventure_upgrade_run_start_smoke.sh
bash tests/run_adventure_upgrade_big_capture_smoke.sh
bash tests/run_adventure_upgrade_victory_smoke.sh
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
```

Expected: all commands pass.

- [ ] **Step 6: Commit resume/restart coverage**

```bash
git add tests/AdventureUpgradeRunResumeSmoke.gd tests/run_adventure_upgrade_run_resume_smoke.sh tests/Rules.Tests/Program.cs
git commit -m "test: cover unfinished upgrade run resume choices"
```

## Task 7: Smoke exit leak governance

### Task 7.1: Add a reusable verbose leak regression harness

**Files:**
- Create: `tests/run_verbose_smoke_with_leak_assertions.sh`
- Create: `tests/run_game_board_verbose_leak_suite.sh`
- Read: `tests/godot_test_env.sh`
- Read: `tests/run_adventure_upgrade_run_start_smoke.sh`
- Read: `tests/run_adventure_upgrade_overlay_display_failure_smoke.sh`
- Read: `tests/run_adventure_upgrade_victory_display_failure_smoke.sh`
- Read: `tests/run_selection_overlay_controller_isolation_smoke.sh`

- [ ] **Step 1: Create the generic verbose smoke runner**

```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"

if [[ $# -ne 2 ]]; then
	echo "Usage: $0 <godot-script-path> <success-marker>" >&2
	exit 1
fi

SCRIPT_PATH="$1"
SUCCESS_MARKER="$2"

if [[ -n "${GODOT_BIN:-}" ]]; then
	GODOT_CMD="$GODOT_BIN"
elif command -v Godot >/dev/null 2>&1; then
	GODOT_CMD="$(command -v Godot)"
elif [[ -x "$DEFAULT_GODOT_BIN" ]]; then
	GODOT_CMD="$DEFAULT_GODOT_BIN"
else
	echo "Missing Godot binary. Set GODOT_BIN." >&2
	exit 1
fi

TMP_DIR="$(mktemp -d)"
codex_setup_godot_test_home "$TMP_DIR"
trap 'rm -rf "$TMP_DIR"' EXIT

LOG_FILE="$TMP_DIR/verbose-smoke.log"
"$GODOT_CMD" --headless --verbose --path "$ROOT_DIR" --script "$SCRIPT_PATH" >"$LOG_FILE" 2>&1

grep -Fq "$SUCCESS_MARKER" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }

if grep -Eq \
	"ObjectDB instances leaked|Leaked instance:|resources still in use|Resource still in use:" \
	"$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
```

- [ ] **Step 2: Add the Task 7 verbose leak suite wrapper**

```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNNER="$ROOT_DIR/tests/run_verbose_smoke_with_leak_assertions.sh"

bash "$RUNNER" res://tests/AdventureUpgradeRunStartSmoke.gd ADVENTURE_UPGRADE_RUN_START_SMOKE_OK
bash "$RUNNER" res://tests/AdventureUpgradeOverlayDisplayFailureSmoke.gd ADVENTURE_UPGRADE_OVERLAY_DISPLAY_FAILURE_SMOKE_OK
bash "$RUNNER" res://tests/AdventureUpgradeVictoryDisplayFailureSmoke.gd ADVENTURE_UPGRADE_VICTORY_DISPLAY_FAILURE_SMOKE_OK
bash "$RUNNER" res://tests/SelectionOverlayControllerIsolationSmoke.gd SELECTION_OVERLAY_CONTROLLER_ISOLATION_SMOKE_OK

echo "GAME_BOARD_VERBOSE_LEAK_SUITE_OK"
```

- [ ] **Step 3: Run the new suite before runtime cleanup changes**

Run: `bash tests/run_game_board_verbose_leak_suite.sh`

Expected: FAIL while printing leak diagnostics that include at least one of these lines:

```text
ObjectDB instances leaked at exit
Leaked instance: AudioStreamMP3
Resource still in use: res://asset/audio/bgm/gameplay_calm_loop.mp3
```

- [ ] **Step 4: Commit the failing regression harness**

```bash
git add tests/run_verbose_smoke_with_leak_assertions.sh tests/run_game_board_verbose_leak_suite.sh
git commit -m "test: add verbose leak regression harness"
```

### Task 7.2: Make GameAudioManager immediate disposal actually release live streams

**Files:**
- Modify: `scripts/GameAudioManager.cs`
- Test: `tests/run_game_board_verbose_leak_suite.sh`

- [ ] **Step 1: Add owned-audio-tween tracking**

Add this field near the existing audio state fields:

```csharp
private readonly List<Tween> _ownedAudioTweens = new();
```

Add these helpers below `GetAudioPlayerById(...)` and above `DisposeAllAudioPlayers()`:

```csharp
private Tween CreateOwnedAudioTween()
{
	Tween tween = CreateTween();
	_ownedAudioTweens.Add(tween);
	ulong tweenId = tween.GetInstanceId();
	tween.Finished += () => ForgetOwnedAudioTween(tweenId);
	return tween;
}

private void ForgetOwnedAudioTween(ulong tweenId)
{
	Tween? tween = GodotObject.InstanceFromId(tweenId) as Tween;
	if (tween != null)
	{
		_ownedAudioTweens.Remove(tween);
	}
}

private void StopOwnedAudioTweens()
{
	foreach (Tween tween in new List<Tween>(_ownedAudioTweens))
	{
		if (GodotObject.IsInstanceValid(tween))
		{
			tween.Kill();
		}
	}

	_ownedAudioTweens.Clear();
}
```

- [ ] **Step 2: Route all audio fade tweens through the owned-tween helper**

Change both tween creation sites:

```csharp
Tween tween = CreateOwnedAudioTween();
```

Apply this in:

```csharp
private void FadePlayerTo(AudioStreamPlayer? player, float volumeDb, float fadeSeconds)
private void FadeOutAndFree(AudioStreamPlayer? player, float fadeSeconds)
```

- [ ] **Step 3: Strengthen immediate disposal ordering**

Rewrite `DisposeAllAudioImmediately()` so it follows this exact order:

```csharp
public void DisposeAllAudioImmediately()
{
	_gameplayTensionEnabled = false;
	StopOwnedAudioTweens();
	DisposeAllAudioPlayers();
	_musicPlayer = null;
	_ambiencePlayer = null;
	_tensionPlayer = null;
	_sfxPlayerPool.Clear();
	_streamCache.Clear();
	_musicStreamCache.Clear();
	_lastPlayedAt.Clear();
	_missingWarningIds.Clear();
	_missingMusicWarningIds.Clear();
	_currentMusicId = string.Empty;
	_currentMusicSpec = null;
}
```

- [ ] **Step 4: Make per-player disposal clear tracked references before free**

Update `DisposeAudioPlayer(...)` so it nulls tracked layer references before removing the player:

```csharp
if (ReferenceEquals(livePlayer, _musicPlayer))
{
	_musicPlayer = null;
}

if (ReferenceEquals(livePlayer, _ambiencePlayer))
{
	_ambiencePlayer = null;
}

if (ReferenceEquals(livePlayer, _tensionPlayer))
{
	_tensionPlayer = null;
}

livePlayer.Stop();
livePlayer.Stream = null;
livePlayer.GetParent()?.RemoveChild(livePlayer);
livePlayer.Free();
```

- [ ] **Step 5: Run the verbose suite after the audio cleanup change**

Run: `bash tests/run_game_board_verbose_leak_suite.sh`

Expected: FAIL only if a non-audio orphan still remains. The failure output should no longer contain any `Resource still in use:` lines for:

```text
res://asset/audio/bgm/gameplay_calm_loop.mp3
res://asset/audio/bgm/underwater_ambience_loop.mp3
res://asset/audio/sfx/core/card_deal.mp3
res://asset/audio/sfx/ui/ui_panel_open.wav
```

- [ ] **Step 6: Commit the audio cleanup fix**

```bash
git add scripts/GameAudioManager.cs
git commit -m "fix: dispose audio streams cleanly in smoke exits"
```

### Task 7.3: Close board-side transient UI during scene exit and lock the clean exit

**Files:**
- Modify: `scripts/GameBoardScene.cs`
- Modify: `scripts/GameBoardScene.SelectionOverlayController.cs`
- Test: `tests/run_game_board_verbose_leak_suite.sh`
- Test: `tests/run_adventure_upgrade_run_start_smoke.sh`
- Test: `tests/run_adventure_upgrade_overlay_display_failure_smoke.sh`
- Test: `tests/run_adventure_upgrade_victory_display_failure_smoke.sh`
- Test: `tests/run_selection_overlay_controller_isolation_smoke.sh`

- [ ] **Step 1: Make board exit dismiss any active selection overlay session**

Update `GameBoardScene._ExitTree()` to dispose transient UI before pool cleanup:

```csharp
public override void _ExitTree()
{
	DisconnectNetworkManagerSignalsForBoard();
	DisposeActiveSelectionOverlay();
	ClearDetachedVisualPools();
	_animationPresenter?.ClearPools();
}
```

- [ ] **Step 2: Keep overlay controller disposal idempotent**

Preserve this invariant inside `DismissActiveSelectionOverlay(...)`:

```text
- Safe to call when there is no active session.
- Safe to call after a session already disposed itself through selection completion.
- Never invoke cancel twice for the same session.
- Always clear `_activeSelectionOverlaySession` before disposing the root.
```

The method should still keep this sequence:

```csharp
SelectionOverlaySession? session = _activeSelectionOverlaySession;
if (session == null)
{
	return;
}

if (invokeCancel)
{
	session.Cancel();
}

if (ReferenceEquals(_activeSelectionOverlaySession, session))
{
	_activeSelectionOverlaySession = null;
	session.Dispose();
}
```

- [ ] **Step 3: Re-run the functional smoke coverage**

Run:

```bash
bash tests/run_adventure_upgrade_run_start_smoke.sh
bash tests/run_adventure_upgrade_overlay_display_failure_smoke.sh
bash tests/run_adventure_upgrade_victory_display_failure_smoke.sh
bash tests/run_selection_overlay_controller_isolation_smoke.sh
```

Expected: PASS for all four scripts with their existing success markers unchanged.

- [ ] **Step 4: Re-run the verbose leak suite and require a clean exit**

Run: `bash tests/run_game_board_verbose_leak_suite.sh`

Expected:

```text
GAME_BOARD_VERBOSE_LEAK_SUITE_OK
```

and no output containing:

```text
ObjectDB instances leaked
Leaked instance:
resources still in use
Resource still in use:
```

- [ ] **Step 5: Commit the board-exit cleanup**

```bash
git add scripts/GameBoardScene.cs scripts/GameBoardScene.SelectionOverlayController.cs
git commit -m "fix: clean board transient nodes on smoke exit"
```

### Task 8: Final verification and execution handoff

**Files:**
- Modify: `docs/superpowers/plans/2026-07-01-task-7-smoke-exit-leak-governance.md`

- [ ] **Step 1: Run the full verification set**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_adventure_upgrade_run_resume_smoke.sh
bash tests/run_adventure_upgrade_run_start_smoke.sh
bash tests/run_adventure_upgrade_big_capture_smoke.sh
bash tests/run_adventure_upgrade_victory_smoke.sh
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
bash tests/run_adventure_upgrade_overlay_display_failure_smoke.sh
bash tests/run_adventure_upgrade_victory_display_failure_smoke.sh
bash tests/run_selection_overlay_controller_isolation_smoke.sh
bash tests/run_game_board_verbose_leak_suite.sh
```

Expected:

```text
- Rules tests pass.
- All Task 6 adventure-upgrade session/resume smokes pass.
- All Task 7 overlay and leak smokes pass.
- The verbose leak suite prints GAME_BOARD_VERBOSE_LEAK_SUITE_OK.
```

- [ ] **Step 2: Record the acceptance criteria in the plan footer before handoff**

Append this exact checklist under a `## Acceptance Criteria` heading:

```markdown
- `AdventureUpgradeRunState` no longer contains static mutable run state.
- `AdventureRunSessionStore` persists exactly one unfinished upgrade run per active profile.
- Continue/restart dialog appears only when the active profile has an unfinished run.
- Continue restores stage-level run state and starts a fresh match for the saved stage.
- Restart clears the saved run and starts from the currently highlighted adventure selection.
- Final main adventure completion clears the pending run.
- Pause-menu restart resets the run and starts a new run from the current stage.
- No `ObjectDB instances leaked` warnings in the Task 7 verbose leak suite.
- No `Resource still in use:` lines for active board music or SFX streams.
- Standard adventure-upgrade and selection-overlay smoke behavior remains unchanged.
- Task 7 stays scoped to shared `GameBoardScene` cleanup and does not expand into menu or multiplayer scene leak work.
```

- [ ] **Step 3: Commit the finalized combined Task 6 and Task 7 plan if the file changed during execution notes**

```bash
git add docs/superpowers/plans/2026-07-01-task-7-smoke-exit-leak-governance.md
git commit -m "docs: finalize upgrade run session and leak governance plan"
```

## Acceptance Criteria

- `AdventureUpgradeRunState` no longer contains static mutable run state.
- `AdventureRunSessionStore` persists exactly one unfinished upgrade run per active profile.
- Continue/restart dialog appears only when the active profile has an unfinished run.
- Continue restores stage-level run state and starts a fresh match for the saved stage.
- Restart clears the saved run and starts from the currently highlighted adventure selection.
- Final main adventure completion clears the pending run.
- Pause-menu restart resets the run and starts a new run from the current stage.
- No `ObjectDB instances leaked` warnings in the Task 7 verbose leak suite.
- No `Resource still in use:` lines for active board music or SFX streams.
- Standard adventure-upgrade and selection-overlay smoke behavior remains unchanged.
- Task 7 stays scoped to shared `GameBoardScene` cleanup and does not expand into menu or multiplayer scene leak work.
