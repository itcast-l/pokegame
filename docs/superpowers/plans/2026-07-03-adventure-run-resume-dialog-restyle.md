# Adventure Run Resume Dialog Restyle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the unfinished-upgrade-run resume dialog to match the approved underwater modal direction while keeping the dialog readable across every shipped locale.

**Architecture:** Keep the existing runtime entry points in `AdventureAvatarMenuScene.RunResumeDialog.cs`, but replace the ad-hoc panel construction with the same panel/button art vocabulary already used by the main menu dialogs. Add a focused layout smoke test that opens the runtime-built dialog for every locale and fails on text overflow or off-screen controls, then use that test to drive the layout changes.

**Tech Stack:** Godot 4 C#, GDScript headless smoke tests, runtime CSV-backed localization.

---

### Task 1: Add a locale layout regression for the resume dialog

**Files:**
- Create: `tests/AdventureUpgradeRunResumeLocalizationLayoutSmoke.gd`
- Create: `tests/run_adventure_upgrade_run_resume_localization_layout_smoke.sh`

- [ ] **Step 1: Add a failing dialog-specific localization smoke**

Create a headless GDScript smoke that:
- loads translations from `res://localization/ui.csv`
- seeds an unfinished run by reusing the existing menu/board flow from `tests/AdventureUpgradeRunResumeSmoke.gd`
- opens the resume dialog for each locale
- audits only the dialog subtree for text overflow and controls outside the viewport

- [ ] **Step 2: Run the new smoke and confirm it fails on the current dialog**

Run:

```bash
bash tests/run_adventure_upgrade_run_resume_localization_layout_smoke.sh
```

Expected: non-zero exit because the current runtime-built dialog uses fixed offsets and fixed-width buttons that do not safely fit every locale.

### Task 2: Restyle the runtime-built dialog and make it locale-safe

**Files:**
- Modify: `scripts/AdventureAvatarMenuScene.RunResumeDialog.cs`

- [ ] **Step 1: Replace the ad-hoc panel with project-native dialog art**

Build the dialog with:
- a full-screen scrim
- a centered motion root
- `OceanDialogPanelArt` as the frame surface
- `PlayerCountDialogFrameArt` wrappers for the action buttons

Keep the existing continue / restart / back handlers and focus behavior unchanged.

- [ ] **Step 2: Make title, body, and buttons adapt to locale length**

Use container-driven layout instead of fixed offsets, then add minimal fitting logic so:
- the title can wrap cleanly
- the body keeps readable line length
- the button row can switch from one row to stacked rows when text gets too wide
- button labels never rely on hardcoded English-width assumptions

- [ ] **Step 3: Run the new localization smoke until it passes**

Run:

```bash
bash tests/run_adventure_upgrade_run_resume_localization_layout_smoke.sh
```

Expected: exit 0 with an `_OK` marker from the new smoke.

- [ ] **Step 4: Re-run the existing resume behavior smoke**

Run:

```bash
bash tests/run_adventure_upgrade_run_resume_smoke.sh
```

Expected: exit 0 with `ADVENTURE_UPGRADE_RUN_RESUME_SMOKE_OK`, proving the restyle did not break continue/restart behavior.
