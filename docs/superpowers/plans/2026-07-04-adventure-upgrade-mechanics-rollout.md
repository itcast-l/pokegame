# Adventure Upgrade Mechanics Rollout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the adventure upgrade system from "offer and persist chosen IDs" into a fully working 56-upgrade mechanics system with cross-stage run state, question reward logic, capture/damage modifiers, and Godot smoke coverage.

**Architecture:** Keep Pandora as the source of upgrade metadata and display text, but move all gameplay interpretation into explicit C# rule models keyed by upgrade ID. Build a pure `AdventureUpgradeEffectService` plus run-state v2 so most behavior can be covered in `tests/Rules.Tests`, then add thin `GameBoardScene` hooks for draw/heal/damage/reveal/reorder/extra-action effects. Preserve existing normal-mode, tutorial-mode, and non-adventure rules by guarding every effect behind `ShouldUseAdventureUpgradeSystem()`.

**Tech Stack:** Godot 4.6.1, C# partial `GameBoardScene`, Pandora data catalog, custom `Rules.Tests` runner, GDScript smoke scripts

---

### Task 1: Add Run-State V2 And Shared Effect Models

**Files:**
- Create: `scripts/AdventureUpgradeEffectState.cs`
- Create: `scripts/AdventureUpgradeEffectService.cs`
- Modify: `scripts/AdventureUpgradeRunSnapshot.cs`
- Modify: `scripts/AdventureUpgradeRunState.cs`
- Modify: `scripts/AdventureRunSessionStore.cs`
- Modify: `tests/Rules.Tests/Rules.Tests.csproj`
- Test: `tests/Rules.Tests/Program.cs`

- [ ] **Step 1: Write the failing rules tests for snapshot v2 migration and persistence**

```csharp
Run("adventure upgrade run snapshot v2 restores legacy v1 saves", AdventureUpgradeRunSnapshotV2RestoresLegacyV1Saves);
Run("adventure upgrade run state persists effect counters and health", AdventureUpgradeRunStatePersistsEffectCountersAndHealth);
```

```csharp
private static void AdventureUpgradeRunSnapshotV2RestoresLegacyV1Saves()
{
	var legacy = new AdventureUpgradeRunSnapshot(
		1,
		3,
		1,
		true,
		true,
		new List<string> { "hearts_thick_blood" });

	var state = new AdventureUpgradeRunState();
	state.RestoreFromSnapshot(legacy);

	Equal(100, state.CurrentHealth);
	Equal(100, state.MaxHealth);
	True(state.HasAcquiredUpgrade("hearts_thick_blood"));
}

private static void AdventureUpgradeRunStatePersistsEffectCountersAndHealth()
{
	var state = new AdventureUpgradeRunState();
	state.BeginNewRun(3);
	state.SetHealthForTests(92, 108);
	state.SetEffectStateForTests(new AdventureUpgradeEffectState
	{
		PermanentCaptureDamageBonus = 2,
		QuestionRewardCount = 3,
		HealingOverflowRemainder = 1,
		NextCaptureDamageBonus = 4,
	});

	AdventureUpgradeRunSnapshot snapshot = state.ToSnapshot();
	var restored = new AdventureUpgradeRunState();
	restored.RestoreFromSnapshot(snapshot);

	Equal(92, restored.CurrentHealth);
	Equal(108, restored.MaxHealth);
	Equal(2, restored.EffectState.PermanentCaptureDamageBonus);
	Equal(3, restored.EffectState.QuestionRewardCount);
	Equal(1, restored.EffectState.HealingOverflowRemainder);
	Equal(4, restored.EffectState.NextCaptureDamageBonus);
}
```

- [ ] **Step 2: Run the rules tests to verify RED**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: fail because `AdventureUpgradeRunSnapshot` is still schema v1 and `AdventureUpgradeRunState` has no health/effect-state persistence yet.

- [ ] **Step 3: Implement snapshot v2 and effect-state model**

```csharp
internal readonly record struct AdventureUpgradeRunSnapshot(
	int SchemaVersion,
	int CurrentAvatarStageNumber,
	int CurrentRunStageIndex,
	bool ShouldGrantRunStartUpgrade,
	bool ShouldGrantVictoryUpgrade,
	IReadOnlyList<string> AcquiredUpgradeIds,
	int CurrentHealth = 100,
	int MaxHealth = 100,
	int PermanentCaptureDamageBonus = 0,
	int QuestionRewardCount = 0,
	bool QuestionRewardDamageDoubles = false,
	int HealingOverflowRemainder = 0,
	int MissStreakBonus = 0,
	int NextCaptureDamageBonus = 0,
	int NextTurnHealAmount = 0,
	int NextTurnDrawCount = 0)
{
	internal const int CurrentSchemaVersion = 2;
}
```

```csharp
internal sealed class AdventureUpgradeEffectState
{
	internal int PermanentCaptureDamageBonus { get; set; }
	internal int QuestionRewardCount { get; set; }
	internal bool QuestionRewardDamageDoubles { get; set; }
	internal int HealingOverflowRemainder { get; set; }
	internal int MissStreakBonus { get; set; }
	internal int NextCaptureDamageBonus { get; set; }
	internal int NextTurnHealAmount { get; set; }
	internal int NextTurnDrawCount { get; set; }

	internal AdventureUpgradeEffectState Clone()
	{
		return (AdventureUpgradeEffectState)MemberwiseClone();
	}
}
```

In `scripts/AdventureUpgradeRunState.cs`:

- add `CurrentHealth`, `MaxHealth`, and `EffectState`
- initialize main-player adventure health to `100/100`
- on v1 restore, preserve acquired IDs and trigger flags while defaulting v2 fields
- keep old save compatibility instead of hard-rejecting `SchemaVersion == 1`

- [ ] **Step 4: Run the rules tests to verify GREEN**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: the two new run-state tests pass.

- [ ] **Step 5: Commit the foundation state changes**

```bash
git add scripts/AdventureUpgradeEffectState.cs scripts/AdventureUpgradeEffectService.cs scripts/AdventureUpgradeRunSnapshot.cs scripts/AdventureUpgradeRunState.cs scripts/AdventureRunSessionStore.cs tests/Rules.Tests/Program.cs tests/Rules.Tests/Rules.Tests.csproj
git commit -m "feat: add adventure upgrade run state v2 foundation"
```

### Task 2: Replace Question Reward Stub With Rule-Driven Adventure Reward Flow

**Files:**
- Modify: `scripts/RewardResolver.cs`
- Modify: `scripts/GameBoardScene.QuestionCards.cs`
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`
- Modify: `scripts/AdventureUpgradeEffectService.cs`
- Modify: `tests/Rules.Tests/Program.cs`
- Test: `tests/AdventureUpgradeRunStartSmoke.gd`

- [ ] **Step 1: Write failing rules tests for weighted question rewards and reroll/damage side effects**

```csharp
Run("adventure question reward picks from item reward catalog", AdventureQuestionRewardPicksFromItemRewardCatalog);
Run("adventure question reward side effects track count permanent bonus and final double", AdventureQuestionRewardSideEffectsTrackCountPermanentBonusAndFinalDouble);
```

```csharp
private static void AdventureQuestionRewardSideEffectsTrackCountPermanentBonusAndFinalDouble()
{
	var state = new AdventureUpgradeEffectState();
	var upgrades = new[]
	{
		"diamonds_question_mark_windfall",
		"diamonds_question_mark_interest",
		"diamonds_final_double",
	};

	AdventureQuestionRewardResult first = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);
	AdventureQuestionRewardResult second = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);
	AdventureQuestionRewardResult third = AdventureUpgradeEffectService.OnQuestionRewardGranted(state, upgrades);

	Equal(1, first.BonusDirectDamage);
	Equal(2, second.BonusDirectDamage);
	Equal(3, third.BonusDirectDamage);
	Equal(3, state.QuestionRewardCount);
	Equal(3, state.PermanentCaptureDamageBonus);
	True(state.QuestionRewardDamageDoubles);
}
```

- [ ] **Step 2: Run the targeted rules tests to verify RED**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: fail because `PickQuestionRewardItemCardId()` is still hardcoded to `Suit Collect` and no question-reward upgrade side effects exist.

- [ ] **Step 3: Implement adventure reward resolution and effect hooks**

```csharp
internal readonly record struct AdventureQuestionRewardResult(
	int BonusDirectDamage,
	int HealAmount,
	int DrawCount,
	bool AllowsSingleReroll);
```

In `scripts/GameBoardScene.QuestionCards.cs`:

- replace the hardcoded `Suit Collect` return with `ItemCardCatalog.RewardIds` + `RewardResolver.PickWeightedReward`
- when adventure upgrades are active, call `AdventureUpgradeEffectService.OnQuestionRewardGranted(...)`
- apply direct-damage/heal/draw follow-up after the reward is inserted
- keep non-adventure question rewards unchanged

In `scripts/GameBoardScene.AdventureUpgrades.cs`:

- after selecting an upgrade, immediately apply any on-acquire effects returned by the service
- add thin helpers that persist effect-state updates back into `AdventureRunSessionStore`

- [ ] **Step 4: Run rules and the run-start smoke to verify GREEN**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_adventure_upgrade_run_start_smoke.sh
```

Expected: rules stay green and the run-start flow still reaches the selection overlay without reward-resolution regressions.

- [ ] **Step 5: Commit the question reward changes**

```bash
git add scripts/RewardResolver.cs scripts/GameBoardScene.QuestionCards.cs scripts/GameBoardScene.AdventureUpgrades.cs scripts/AdventureUpgradeEffectService.cs tests/Rules.Tests/Program.cs
git commit -m "feat: resolve adventure question rewards from rules"
```

### Task 3: Route Adventure Health, Healing, Capture Damage, And Deferred Effects Through The Service

**Files:**
- Modify: `scripts/GameBoardScene.Score.cs`
- Modify: `scripts/GameBoardScene.TableCaptureFlow.cs`
- Modify: `scripts/GameBoardScene.Turns.cs`
- Modify: `scripts/GameBoardScene.TurnAdvancement.cs`
- Modify: `scripts/GameBoardScene.TurnEndState.cs`
- Modify: `scripts/GameBoardScene.HandInteractions.cs`
- Modify: `scripts/GameBoardScene.TurnAi.cs`
- Modify: `scripts/AdventureUpgradeEffectService.cs`
- Modify: `tests/Rules.Tests/Program.cs`
- Test: `tests/AdventureUpgradeVictorySmoke.gd`
- Test: `tests/AdventureUpgradeBigCaptureSmoke.gd`

- [ ] **Step 1: Write failing rules tests for hearts and diamonds capture/heal logic**

```csharp
Run("adventure capture rules apply min damage bonus chaining and final double in order", AdventureCaptureRulesApplyDamagePipelineInOrder);
Run("adventure healing overflow increases max health every four overflow", AdventureHealingOverflowIncreasesMaxHealthEveryFourOverflow);
Run("adventure low-health rescue can heal before defeat resolves", AdventureLowHealthRescueCanPreventDefeat);
```

- [ ] **Step 2: Run the rules tests to verify RED**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: fail because capture damage still maps directly from `CaptureFlowPlanner` points to `AddPlayerScore()` with no adventure upgrade pipeline.

- [ ] **Step 3: Implement the shared damage/heal pipeline and board integration**

In `scripts/AdventureUpgradeEffectService.cs`, add pure entry points for:

- `OnUpgradeAcquired(...)`
- `OnBattleStarted(...)`
- `OnTurnStarted(...)`
- `OnSuccessfulCapture(...)`
- `OnCaptureResolved(...)`
- `OnMissResolved(...)`
- `ApplyHealing(...)`
- `ApplyIncomingAdventureDamage(...)`

Use the spec's locked order for capture damage:

```csharp
perCardAdjustments
-> copyHighestCard
-> addHalfOriginalTotal
-> flatBonusesAndMinimums
-> finalDoubleAfterThirdQuestion
-> actualDamage
```

In `scripts/GameBoardScene.Score.cs`:

- replace fixed main-player adventure max-health assumptions with run-state driven health for the main player
- add helpers to heal the player, raise max HP, and persist updated run-state values
- allow low-health rescue upgrades to resolve before `DeclareWinner`

In `scripts/GameBoardScene.TableCaptureFlow.cs`:

- compute an adventure capture context before `AddPlayerScore`
- apply question-mark bounty bonus, multi-card bonuses, minimum-damage clamps, next-capture carryovers, and final double
- trigger post-capture draw/heal rewards after damage application

- [ ] **Step 4: Run rules, build, and focused smokes to verify GREEN**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
dotnet build pokegame.csproj
bash tests/run_adventure_upgrade_big_capture_smoke.sh
bash tests/run_adventure_upgrade_victory_smoke.sh
```

Expected: rules pass, project builds, and existing big-capture/victory flows still complete while main-player health remains persistent across stages.

- [ ] **Step 5: Commit the health and damage pipeline changes**

```bash
git add scripts/GameBoardScene.Score.cs scripts/GameBoardScene.TableCaptureFlow.cs scripts/GameBoardScene.Turns.cs scripts/GameBoardScene.TurnAdvancement.cs scripts/GameBoardScene.TurnEndState.cs scripts/GameBoardScene.HandInteractions.cs scripts/GameBoardScene.TurnAi.cs scripts/AdventureUpgradeEffectService.cs tests/Rules.Tests/Program.cs
git commit -m "feat: route adventure damage and healing through upgrade rules"
```

### Task 4: Add Spades And Clubs Table-State Mechanics, Pending Enemy Penalties, And Extra Action Windows

**Files:**
- Modify: `scripts/GameBoardScene.CardPlay.cs`
- Modify: `scripts/GameBoardScene.TableCapture.cs`
- Modify: `scripts/GameBoardScene.TableCaptureFlow.cs`
- Modify: `scripts/GameBoardScene.QuestionCards.cs`
- Modify: `scripts/GameBoardScene.HandInteractions.cs`
- Modify: `scripts/GameBoardScene.TurnAi.cs`
- Modify: `scripts/GameBoardScene.State.cs`
- Modify: `scripts/AdventureUpgradeEffectState.cs`
- Modify: `scripts/AdventureUpgradeEffectService.cs`
- Modify: `tests/Rules.Tests/Program.cs`
- Create: `tests/AdventureUpgradeMechanicsSpadesClubsSmoke.gd`

- [ ] **Step 1: Write failing rules tests for old-table capture logic and pending miss penalties**

```csharp
Run("adventure spades old table card rules distinguish preplay cards from newly played card", AdventureSpadesOldTableRulesDistinguishPreplayCards);
Run("adventure enemy miss penalties queue reveal position and damage suppression", AdventureEnemyMissPenaltiesQueueRevealPositionAndDamageSuppression);
Run("adventure clubs neighbor rank matching applies after miss climb bonus", AdventureClubsNeighborRankMatchingAppliesAfterMissClimbBonus);
```

- [ ] **Step 2: Run the rules tests to verify RED**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: fail because the board currently has no pre-play table snapshot, no pending enemy next-card modifiers, and no extra-action-only-basic-card state.

- [ ] **Step 3: Implement board snapshot, pending state, and extra-action hooks**

In `scripts/AdventureUpgradeEffectState.cs` add pending fields for:

- enemy next-card face-up reveal
- enemy next-card left insertion mode
- enemy next successful capture flat penalty
- tracked old-table marked card instance
- pending one-shot extra basic-card action
- next numeric-card suit-match allowance
- canopy reveal anchor

In `scripts/GameBoardScene.State.cs` add any transient board fields that must survive scene-level action flow but should not serialize into save data.

In `scripts/GameBoardScene.CardPlay.cs` and `scripts/GameBoardScene.HandInteractions.cs`:

- capture a pre-play table snapshot before mutating `_tableCards`
- restrict extra action windows to base cards only
- let AI auto-play the first legal base card during the granted extra action

In `scripts/GameBoardScene.TableCaptureFlow.cs` and `scripts/GameBoardScene.TableCapture.cs`:

- adjust scoring-card membership for black-ledger and crown-canopy style effects
- apply short-branch return, primary-root return, climb-canopy return, and gap-rooting capture expansion
- preserve tail card on enemy capture for `spades_tail_pressure`

- [ ] **Step 4: Run rules, build, and the new spades/clubs smoke to verify GREEN**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
dotnet build pokegame.csproj
"${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}" --headless --path . --script res://tests/AdventureUpgradeMechanicsSpadesClubsSmoke.gd
```

Expected: rules pass, build succeeds, and the smoke validates pending enemy miss effects, extra action windows, and old-table scoring rules.

- [ ] **Step 5: Commit the spades/clubs mechanics**

```bash
git add scripts/GameBoardScene.CardPlay.cs scripts/GameBoardScene.TableCapture.cs scripts/GameBoardScene.TableCaptureFlow.cs scripts/GameBoardScene.QuestionCards.cs scripts/GameBoardScene.HandInteractions.cs scripts/GameBoardScene.TurnAi.cs scripts/GameBoardScene.State.cs scripts/AdventureUpgradeEffectState.cs scripts/AdventureUpgradeEffectService.cs tests/Rules.Tests/Program.cs tests/AdventureUpgradeMechanicsSpadesClubsSmoke.gd
git commit -m "feat: add adventure spades and clubs mechanics"
```

### Task 5: Finish Coverage, Smoke Suites, And Full Regression Verification

**Files:**
- Modify: `tests/Rules.Tests/Program.cs`
- Create: `tests/AdventureUpgradeMechanicsHeartsSmoke.gd`
- Create: `tests/AdventureUpgradeMechanicsDiamondsSmoke.gd`
- Modify: `tests/run_adventure_upgrade_run_start_smoke.sh`
- Modify: `tests/run_adventure_upgrade_victory_smoke.sh`
- Modify: `tests/run_adventure_upgrade_big_capture_smoke.sh`
- Modify: `tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh`

- [ ] **Step 1: Add final rules tests for pool counts, disabled upgrade, and suit-by-suit invariants**

```csharp
Run("adventure upgrade catalog exposes 56 enabled upgrades with one disabled spade", AdventureUpgradeCatalogExposesExpectedEnabledCounts);
Run("adventure upgrade rules keep four suits at fourteen active entries each", AdventureUpgradeRulesKeepFourSuitCountsBalanced);
```

- [ ] **Step 2: Run the full rules suite to verify RED or coverage gaps**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: if any remaining mechanics are still missing, the newly added assertions fail here before final smoke work starts.

- [ ] **Step 3: Add or extend suit-focused Godot smokes and shell wrappers**

Ensure smoke coverage for:

- hearts cross-stage health/max-HP persistence and rescue triggers
- diamonds third-question final double and question direct-damage chain
- spades enemy miss pending state and black-ledger scoring
- clubs extra actions, return-to-hand, and neighbor-rank matching

- [ ] **Step 4: Run the final verification bundle**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
dotnet build pokegame.csproj
bash tests/run_adventure_upgrade_run_start_smoke.sh
bash tests/run_adventure_upgrade_victory_smoke.sh
bash tests/run_adventure_upgrade_big_capture_smoke.sh
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
```

Expected: all commands succeed with no adventure-upgrade regressions.

- [ ] **Step 5: Commit the final coverage pass**

```bash
git add tests/Rules.Tests/Program.cs tests/AdventureUpgradeMechanicsHeartsSmoke.gd tests/AdventureUpgradeMechanicsDiamondsSmoke.gd tests/run_adventure_upgrade_run_start_smoke.sh tests/run_adventure_upgrade_victory_smoke.sh tests/run_adventure_upgrade_big_capture_smoke.sh tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
git commit -m "test: cover adventure upgrade mechanics rollout"
```
