# Single Player AI Difficulty Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add single-player AI difficulty selection and make offline solo AI behavior vary across low, medium, and high according to the approved spec.

**Architecture:** Keep the UI and scene wiring small inside the existing main-menu and `GameBoardScene` flow. Extract solo AI heuristics into focused rule helpers so capture choice, target choice, discard choice, and magnet attachment choice can be tested without booting Godot scenes, then reuse those helpers from the live board logic while leaving adventure AI and network AI behavior unchanged.

**Tech Stack:** Godot 4.6.1 Mono, C#, `.tscn` scenes, CSV localization, custom shell-based rule tests, headless Godot smoke audits

---

### Task 1: Add failing rule tests for solo AI difficulty behavior

**Files:**
- Create: `scripts/SoloAiRules.cs`
- Modify: `tests/Rules.Tests/Program.cs`
- Modify: `tests/Rules.Tests/Rules.Tests.csproj`
- Test: `tests/Rules.Tests/Program.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
Run("solo ai difficulty profiles expose expected values", SoloAiDifficultyProfilesExposeExpectedValues);
Run("solo ai medium capture prefers more cards while high prefers points", SoloAiMediumCapturePrefersMoreCardsWhileHighPrefersPoints);
Run("solo ai medium and high skip pair only captures", SoloAiMediumAndHighSkipPairOnlyCaptures);
Run("solo ai target player strategy picks largest relevant opponent", SoloAiTargetPlayerStrategyPicksLargestRelevantOpponent);
Run("solo ai discard and magnet heuristics keep immediate captures when possible", SoloAiDiscardAndMagnetHeuristicsKeepImmediateCapturesWhenPossible);
Run("player count localization difficulty rows exist for every locale", PlayerCountLocalizationDifficultyRowsExistForEveryLocale);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
Expected: FAIL with missing `SoloAiRules` symbols and missing localization rows.

- [ ] **Step 3: Write minimal implementation**

```csharp
internal static class SoloAiRules
{
	internal enum Difficulty
	{
		Low = 0,
		Medium = 1,
		High = 2,
	}

	internal readonly record struct Profile(
		int BestCaptureChancePercent,
		int AnyCaptureChancePercent,
		bool UsePointScoring,
		bool AvoidPairOnlyCaptures);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
Expected: PASS for the new solo AI rule tests.

- [ ] **Step 5: Commit**

```bash
git add tests/Rules.Tests/Program.cs tests/Rules.Tests/Rules.Tests.csproj scripts/SoloAiRules.cs localization/ui.csv
git commit -m "test: cover solo ai difficulty rules"
```

### Task 2: Wire solo AI rules into offline single-player board behavior

**Files:**
- Modify: `scripts/GameBoardScene.cs`
- Modify: `scripts/GameBoardScene.TurnAi.cs`
- Modify: `scripts/GameBoardScene.TargetItemTargeting.cs`
- Modify: `scripts/GameBoardScene.ItemCards.cs`
- Modify: `scripts/GameBoardScene.MagnetAttachments.cs`
- Modify: `scripts/GameBoardScene.EffectResolver.cs`
- Test: `tests/Rules.Tests/Program.cs`

- [ ] **Step 1: Write the failing behavior coverage first**

```csharp
Run("solo ai random capture branch stays legal", SoloAiRandomCaptureBranchStaysLegal);
Run("solo ai hand target index remains random legal choice", SoloAiHandTargetIndexRemainsRandomLegalChoice);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
Expected: FAIL because `GameBoardScene` still uses random discard/magnet logic and best-capture fallback for offline solo turns.

- [ ] **Step 3: Write minimal implementation**

```csharp
[Export(PropertyHint.Enum, "Low,Medium,High")]
public int solo_ai_difficulty
{
	get => _soloAiDifficulty;
	set => _soloAiDifficulty = Mathf.Clamp(value, 0, 2);
}

private bool IsOfflineSoloAiDifficultyActive()
{
	return solo_mode && !adventure_mode && !IsNetworkMultiplayerMode();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
Expected: PASS with offline solo AI rules using the new difficulty helper while adventure and network paths remain unchanged.

- [ ] **Step 5: Commit**

```bash
git add scripts/GameBoardScene.cs scripts/GameBoardScene.TurnAi.cs scripts/GameBoardScene.TargetItemTargeting.cs scripts/GameBoardScene.ItemCards.cs scripts/GameBoardScene.MagnetAttachments.cs scripts/GameBoardScene.EffectResolver.cs scripts/SoloAiRules.cs tests/Rules.Tests/Program.cs
git commit -m "feat: apply solo ai difficulty rules"
```

### Task 3: Add menu difficulty selection, localization, icon, and scene plumbing

**Files:**
- Modify: `scripts/MainMenuPlayerCountDialog.cs`
- Modify: `scripts/MainMenuScene.cs`
- Modify: `scripts/MainMenuScene.PlayerCountDialog.cs`
- Modify: `scripts/MainMenuPlayerCountIconBadge.cs`
- Modify: `scenes/main_menu_player_count_dialog.tscn`
- Modify: `localization/ui.csv`
- Create: `assets/ui/player_count_difficulty_glyph.png`
- Test: `tests/LocalizationLayoutAudit.gd`

- [ ] **Step 1: Write the failing coverage first**

```csharp
Run("player count localization difficulty rows exist for every locale", PlayerCountLocalizationDifficultyRowsExistForEveryLocale);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
Expected: FAIL because `PLAYER_COUNT_DIFFICULTY*` keys are not present yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
[Signal]
public delegate void StartGameRequestedEventHandler(int playerCount, int soloAiDifficulty, bool itemMode, int winningScore);

public OptionButton DifficultyOption { get; private set; } = default!;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`
Expected: PASS for localization coverage and no compile errors from the updated dialog signal.

- [ ] **Step 5: Commit**

```bash
git add scripts/MainMenuPlayerCountDialog.cs scripts/MainMenuScene.cs scripts/MainMenuScene.PlayerCountDialog.cs scripts/MainMenuPlayerCountIconBadge.cs scenes/main_menu_player_count_dialog.tscn localization/ui.csv assets/ui/player_count_difficulty_glyph.png
git commit -m "feat: add solo ai difficulty menu selection"
```

### Task 4: Verify scene smoke and localization layout audits

**Files:**
- Modify: `tests/GameBoardModeRegressionSmoke.gd`
- Modify: `tests/run_game_board_mode_regression_smoke.sh`
- Test: `tests/GameBoardModeRegressionSmoke.gd`
- Test: `tests/LocalizationLayoutAudit.gd`

- [ ] **Step 1: Write the failing smoke updates**

```gdscript
{
	"name": "solo_low",
	"player_count": 2,
	"item_mode": true,
	"winning_score": 100,
	"solo_mode": true,
	"solo_ai_difficulty": 0,
	"adventure_mode": false,
},
{
	"name": "solo_high_four_player",
	"player_count": 4,
	"item_mode": true,
	"winning_score": 100,
	"solo_mode": true,
	"solo_ai_difficulty": 2,
	"adventure_mode": false,
},
```

- [ ] **Step 2: Run test to verify it fails**

Run: `bash tests/run_game_board_mode_regression_smoke.sh`
Expected: FAIL if the new exported difficulty field or new menu wiring causes startup/runtime errors.

- [ ] **Step 3: Write minimal implementation**

```gdscript
grep -Fq "MODE_OK solo_low" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "MODE_OK solo_high" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "MODE_OK solo_high_four_player" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `bash tests/run_game_board_mode_regression_smoke.sh`
Expected: PASS with offline solo low/high variants and four-player solo smoke coverage.

Run: `bash tests/run_localization_layout_audit.sh`
Expected: PASS with no overflow or off-screen issues in the updated player-count dialog.

- [ ] **Step 5: Commit**

```bash
git add tests/GameBoardModeRegressionSmoke.gd tests/run_game_board_mode_regression_smoke.sh
git commit -m "test: expand solo difficulty smoke coverage"
```
