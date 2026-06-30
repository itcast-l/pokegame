# Upgrade Choice Popup Asset Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Godot editor scene that previews and manages a 3-choice upgrade popup style, including 12 upgrade card variants across 4 suits and 3 rarities, without wiring gameplay.

**Architecture:** Define upgrade card visuals as editor-friendly `Resource` data, render them through a reusable `@tool` C# control, and assemble them in a dedicated popup preview scene. Keep the scene fully local to editor/content workflows so it can be opened and managed without touching gameplay code paths.

**Tech Stack:** Godot 4.6 Mono, C#, `.tres` resources, `.tscn` scenes, GDScript smoke test

---

### Task 1: Add editor smoke coverage for the popup asset scene

**Files:**
- Create: `tests/UpgradeChoicePopupAssetSceneSmoke.gd`

- [ ] Add a headless smoke test that loads the new popup asset scene and asserts it exposes 12 card definitions plus a 3-card preview row.
- [ ] Run the new smoke test first and confirm it fails before implementation exists.

### Task 2: Add upgrade popup resource types and reusable card renderer

**Files:**
- Create: `scripts/UpgradePopupCardDefinitionResource.cs`
- Create: `scripts/UpgradePopupStyleLibraryResource.cs`
- Create: `scripts/UpgradePopupCardView.cs`

- [ ] Define a card definition resource with exported fields for suit, rarity, title, description, colors, textures, and sort order.
- [ ] Define a style library resource that stores the 12 card definitions and helper accessors.
- [ ] Implement a `@tool` card view control that redraws automatically in the editor from a bound definition resource.

### Task 3: Create the style library asset and popup management scene

**Files:**
- Create: `resources/ui/upgrade_popup/upgrade_popup_style_library.tres`
- Create: `scripts/UpgradeChoicePopupAssetScene.cs`
- Create: `scenes/upgrade_choice_popup_asset_scene.tscn`

- [ ] Author 12 definitions covering clubs/hearts/spades/diamonds across common/uncommon/rare.
- [ ] Build a management scene with summary text, a 12-card grid, and a 3-choice preview row.
- [ ] Bind the scene script so editor exports can swap the library and adjust which 3 cards appear in the preview.

### Task 4: Verify the scene loads cleanly

**Files:**
- Modify: `tests/GodotSceneSmoke.gd` (only if adding this scene to the broad smoke suite is low risk)

- [ ] Run the targeted popup smoke test and inspect the output.
- [ ] If safe, add the new scene to the broader scene smoke coverage and re-run that verification too.
