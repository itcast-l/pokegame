# Adventure Upgrade Visual Resource Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the adventure-upgrade card art flow to a project-wide external-resource pipeline so editor previews, gameplay overlays, and generated art all use one governed source of truth without ever serializing card pixel data into `.tscn` scenes again.

**Architecture:** Keep processed upgrade card PNGs as Godot-imported external `Texture2D` assets, store suit/rarity/path/region metadata in a dedicated `Resource` catalog under `resources/`, and make both runtime gameplay and editor preview scenes resolve visuals through the same catalog-backed loader. Add repository-level audits so large built-in image resources or oversized scenes fail fast during local verification and CI-style smoke runs.

**Tech Stack:** Godot 4.6 Mono, C#, GDScript, `.tscn` scenes, `.tres` resources, shell-based smoke tests, custom `Rules.Tests` console runner

---

## Read This First

Before executing any task in this plan, read these files in order:

1. [QUICKSTART.md](/Volumes/KIOXIA11/godotproject/pokegame/QUICKSTART.md:1)
2. [docs/RESOURCE_BASELINE.md](/Volumes/KIOXIA11/godotproject/pokegame/docs/RESOURCE_BASELINE.md:1)
3. [docs/superpowers/plans/2026-06-29-upgrade-choice-popup-asset-scene.md](/Volumes/KIOXIA11/godotproject/pokegame/docs/superpowers/plans/2026-06-29-upgrade-choice-popup-asset-scene.md:1)
4. [scripts/GameBoardScene.AdventureUpgrades.cs](/Volumes/KIOXIA11/godotproject/pokegame/scripts/GameBoardScene.AdventureUpgrades.cs:1)
5. [scripts/UpgradeChoicePopupPreview.gd](/Volumes/KIOXIA11/godotproject/pokegame/scripts/UpgradeChoicePopupPreview.gd:1)
6. [scenes/adventure_upgrade_selection_overlay.tscn](/Volumes/KIOXIA11/godotproject/pokegame/scenes/adventure_upgrade_selection_overlay.tscn:1)
7. [scenes/upgrade_choice_popup_asset_scene.tscn](/Volumes/KIOXIA11/godotproject/pokegame/scenes/upgrade_choice_popup_asset_scene.tscn:1)

Why these files matter:

- `QUICKSTART.md` documents the currently accepted build and test entry points.
- `RESOURCE_BASELINE.md` shows the repo's existing style for resource governance and mandatory checks.
- The previous popup asset scene plan explains the original intent of this editor preview scene.
- The adventure-upgrade code and scenes show the current split between runtime overlay rendering and editor-only previewing.

## Confirmed Facts You Must Preserve

These facts were confirmed in the repo before writing this plan and must stay true after implementation:

- `tests/Rules.Tests/Rules.Tests.csproj` is a custom console runner. Use `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj`, not `dotnet test`, as the primary rules verification command.
- `tests/run_multiplayer_reconnect_smoke_suite.sh` is the broadest existing stable headless Godot smoke entry point.
- The runtime overlay scene [adventure_upgrade_selection_overlay.tscn](/Volumes/KIOXIA11/godotproject/pokegame/scenes/adventure_upgrade_selection_overlay.tscn:1) already uses `ext_resource type="Texture2D"` plus `AtlasTexture` for sample card art. Treat this scene as the canonical art-reference pattern.
- The editor preview scene [upgrade_choice_popup_asset_scene.tscn](/Volumes/KIOXIA11/godotproject/pokegame/scenes/upgrade_choice_popup_asset_scene.tscn:1) is currently the only project scene with massive embedded `Image` payloads and is approximately 250 MB.
- `generated-images/upgrade-popup-assets/raw/` is not referenced by runtime code. It is only referenced by `.import` files and `transparent-processing-meta.json`, which means it can be isolated from runtime-facing asset usage without breaking gameplay code.
- The repo already uses `Resource`-backed authoring types under `scripts/` plus `.tres` assets under `resources/` for other asset families, for example [ItemCardDefinitionResource.cs](/Volumes/KIOXIA11/godotproject/pokegame/scripts/ItemCardDefinitionResource.cs:1) and [CharacterAvatarDefinitionResource.cs](/Volumes/KIOXIA11/godotproject/pokegame/scripts/CharacterAvatarDefinitionResource.cs:1).

## Global Rules For Every Round

- Do not change gameplay rules, offer selection rules, rarity weights, localization strings, or card descriptions unless a task explicitly says to.
- Do not rename processed PNG files in this plan unless a task explicitly introduces a migration step and updates every consumer in the same round.
- Do not move third-party addon files.
- Do not mix runtime visual-governance work with unrelated UI restyling.
- Do not edit `project.godot` directly unless a task explicitly requires a setting and no editor-based alternative exists.
- If a task changes scene serialization behavior, that task must end with a scene re-save verification step and a file-size verification step.

## Baseline Commands Before Round 1

Run these commands and record their output in the working notes before touching files:

```bash
dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_multiplayer_reconnect_smoke_suite.sh
wc -c scenes/upgrade_choice_popup_asset_scene.tscn
python3 - <<'PY'
from pathlib import Path
root = Path('scenes/upgrade_choice_popup_asset_scene.tscn')
text = root.read_text(errors='ignore')
print('embedded_image_subresources', text.count('[sub_resource type="Image"'))
print('embedded_image_textures', text.count('[sub_resource type="ImageTexture"'))
print('packed_byte_arrays', text.count('PackedByteArray('))
PY
```

Expected baseline characteristics before implementation:

- `dotnet build pokegame.csproj` passes.
- `dotnet run --project tests/Rules.Tests/Rules.Tests.csproj` passes.
- `bash tests/run_multiplayer_reconnect_smoke_suite.sh` passes.
- `wc -c` reports that `scenes/upgrade_choice_popup_asset_scene.tscn` is far above normal scene size.
- The Python snippet reports 12 embedded `Image` subresources and 12 embedded `ImageTexture` subresources in the popup asset scene.

---

### Task 1: Round 0 Audit And Guardrail Baseline

**Files:**
- Create: `tools/audit_adventure_upgrade_visual_resources.py`
- Create: `docs/RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md`
- Modify: `tests/Rules.Tests/Program.cs`
- Test: `tools/audit_adventure_upgrade_visual_resources.py`
- Test: `tests/Rules.Tests/Program.cs`

- [ ] **Step 1: Create a failing audit script skeleton for the current popup scene problem**

Create `tools/audit_adventure_upgrade_visual_resources.py` with this initial behavior:

```python
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
POPUP_SCENE = ROOT / "scenes" / "upgrade_choice_popup_asset_scene.tscn"


def main() -> int:
    text = POPUP_SCENE.read_text(errors="ignore")
    embedded_images = text.count('[sub_resource type="Image"')
    embedded_image_textures = text.count('[sub_resource type="ImageTexture"')
    packed_byte_arrays = text.count('PackedByteArray(')

    if embedded_images == 0 and embedded_image_textures == 0 and packed_byte_arrays == 0:
        print("ADVENTURE_UPGRADE_VISUAL_AUDIT_OK popup scene has no embedded image payloads")
        return 0

    print(
        "ADVENTURE_UPGRADE_VISUAL_AUDIT_FAIL popup scene contains embedded image payloads: "
        f"images={embedded_images} image_textures={embedded_image_textures} packed_arrays={packed_byte_arrays}"
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Run the new audit and confirm it fails against the current scene**

Run:

```bash
python3 tools/audit_adventure_upgrade_visual_resources.py
```

Expected: FAIL output that reports embedded image payloads in `scenes/upgrade_choice_popup_asset_scene.tscn`.

- [ ] **Step 3: Add a failing rules-runner smoke check that calls the audit**

Append a new rules test near the existing resource checks in `tests/Rules.Tests/Program.cs`:

```csharp
Run("adventure upgrade popup scene has no embedded image payloads", AdventureUpgradePopupSceneHasNoEmbeddedImagePayloads);
```

Add the helper method:

```csharp
private static void AdventureUpgradePopupSceneHasNoEmbeddedImagePayloads()
{
	string scenePath = Path.Combine(ProjectRoot(), "scenes", "upgrade_choice_popup_asset_scene.tscn");
	string text = File.ReadAllText(scenePath);

	False(text.Contains("[sub_resource type=\"Image\""), "Popup scene should not embed Image subresources");
	False(text.Contains("[sub_resource type=\"ImageTexture\""), "Popup scene should not embed ImageTexture subresources");
	False(text.Contains("PackedByteArray("), "Popup scene should not serialize packed image bytes");
}
```

If `ProjectRoot()` is not yet available in the file, add a helper consistent with the rest of `Program.cs` path-based resource checks instead of inventing a new test utility layer.

- [ ] **Step 4: Run the rules runner and confirm the new resource-governance test fails**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: FAIL on `adventure upgrade popup scene has no embedded image payloads`.

- [ ] **Step 5: Write the round-0 baseline doc before implementation**

Create `docs/RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md` with these sections:

- Scope
- Current failure signature
- Current affected files
- Mandatory commands
- Regression rules
- Success thresholds after migration

Populate it with repo-specific facts, including:

- the popup scene is currently abnormally large
- runtime overlay uses external textures already
- raw art is not runtime-addressed
- future scenes must not contain built-in card image payloads

- [ ] **Step 6: Commit the audit baseline work**

Run:

```bash
git add tools/audit_adventure_upgrade_visual_resources.py docs/RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md tests/Rules.Tests/Program.cs
git commit -m "test: add adventure upgrade visual resource audit baseline"
```

---

### Task 2: Round 1 Create The Shared Visual Catalog Resource Model

**Files:**
- Create: `scripts/AdventureUpgradeCardVisualEntryResource.cs`
- Create: `scripts/AdventureUpgradeCardVisualCatalogResource.cs`
- Create: `resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres`
- Modify: `pokegame.csproj` if new C# files need explicit inclusion consistency checks
- Test: `tests/Rules.Tests/Program.cs`
- Test: new optional headless smoke in `tests/AdventureUpgradeCardVisualCatalogSmoke.gd`

- [ ] **Step 1: Write a failing rules test for the new visual catalog contract**

Add a failing `Program.cs` entry:

```csharp
Run("adventure upgrade visual catalog exposes 12 processed card visuals", AdventureUpgradeVisualCatalogExposesTwelveProcessedCards);
```

Add a helper method that expects:

- exactly 12 visual entries
- suits `hearts`, `spades`, `diamonds`, `clubs`
- tiers `common`, `uncommon`, `rare`
- every `TexturePath` starts with `res://generated-images/upgrade-popup-assets/`
- every `TexturePath` contains `/processed/`
- every region has positive width and height

Make the test load a `.tres` resource path that does not exist yet so the test fails first.

- [ ] **Step 2: Run the rules runner and confirm the visual catalog test fails**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: FAIL because the new `.tres` and/or resource classes do not exist yet.

- [ ] **Step 3: Create the entry resource type using existing repo patterns**

Create `scripts/AdventureUpgradeCardVisualEntryResource.cs` modeled after the repo's other `[GlobalClass] ... : Resource` files:

```csharp
using Godot;

[GlobalClass]
public partial class AdventureUpgradeCardVisualEntryResource : Resource
{
	[Export] public string Suit { get; set; } = string.Empty;
	[Export] public string Tier { get; set; } = string.Empty;
	[Export] public string TexturePath { get; set; } = string.Empty;
	[Export] public Rect2 Region { get; set; } = Rect2.Zero;
	[Export] public int SortOrder { get; set; }
	[Export] public string PreviewLabel { get; set; } = string.Empty;

	public string Key => $"{Suit.Trim().ToLowerInvariant()}::{Tier.Trim().ToLowerInvariant()}";
}
```

Do not add speculative fields such as animation, tint palettes, variant tags, or localization keys unless a later task actually uses them.

- [ ] **Step 4: Create the catalog resource type with deterministic lookup helpers**

Create `scripts/AdventureUpgradeCardVisualCatalogResource.cs`:

```csharp
using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class AdventureUpgradeCardVisualCatalogResource : Resource
{
	[Export] public Godot.Collections.Array<AdventureUpgradeCardVisualEntryResource> Entries { get; set; } = new();

	public IReadOnlyList<AdventureUpgradeCardVisualEntryResource> GetSortedEntries()
	{
		var results = new List<AdventureUpgradeCardVisualEntryResource>();
		foreach (AdventureUpgradeCardVisualEntryResource entry in Entries)
		{
			if (entry != null)
			{
				results.Add(entry);
			}
		}

		results.Sort((a, b) =>
		{
			int sortComparison = a.SortOrder.CompareTo(b.SortOrder);
			return sortComparison != 0 ? sortComparison : string.CompareOrdinal(a.Key, b.Key);
		});
		return results;
	}

	public AdventureUpgradeCardVisualEntryResource? Find(string suit, string tier)
	{
		string key = $"{Normalize(suit)}::{Normalize(tier)}";
		foreach (AdventureUpgradeCardVisualEntryResource entry in Entries)
		{
			if (entry != null && entry.Key == key)
			{
				return entry;
			}
		}
		return null;
	}

	private static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
	}
	}
```

Keep the type focused on data and lookups. Do not make it load textures itself.

- [ ] **Step 5: Author the catalog `.tres` with the exact 12 processed assets and current crop rects**

Create `resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres` and populate these 12 entries exactly once each:

- `hearts/common` -> `res://generated-images/upgrade-popup-assets/hearts/processed/hearts-common.png` region `(291, 58, 671, 1097)`
- `hearts/uncommon` -> `res://generated-images/upgrade-popup-assets/hearts/processed/hearts-uncommon.png` region `(291, 58, 671, 1097)`
- `hearts/rare` -> `res://generated-images/upgrade-popup-assets/hearts/processed/hearts-rare.png` region `(291, 58, 671, 1097)`
- `spades/common` -> `res://generated-images/upgrade-popup-assets/spades/processed/spades-common.png` region `(290, 32, 673, 1167)`
- `spades/uncommon` -> `res://generated-images/upgrade-popup-assets/spades/processed/spades-uncommon.png` region `(290, 32, 673, 1167)`
- `spades/rare` -> `res://generated-images/upgrade-popup-assets/spades/processed/spades-rare.png` region `(290, 32, 673, 1167)`
- `diamonds/common` -> `res://generated-images/upgrade-popup-assets/diamonds/processed/diamonds-common.png` region `(296, 42, 659, 1156)`
- `diamonds/uncommon` -> `res://generated-images/upgrade-popup-assets/diamonds/processed/diamonds-uncommon.png` region `(296, 42, 659, 1156)`
- `diamonds/rare` -> `res://generated-images/upgrade-popup-assets/diamonds/processed/diamonds-rare.png` region `(296, 42, 659, 1156)`
- `clubs/common` -> `res://generated-images/upgrade-popup-assets/clubs/processed/clubs-common.png` region `(291, 53, 670, 1106)`
- `clubs/uncommon` -> `res://generated-images/upgrade-popup-assets/clubs/processed/clubs-uncommon.png` region `(291, 53, 670, 1106)`
- `clubs/rare` -> `res://generated-images/upgrade-popup-assets/clubs/processed/clubs-rare.png` region `(291, 53, 670, 1106)`

Do not invent new crop rectangles in this round. Preserve the currently shipped runtime values.

- [ ] **Step 6: Re-run the rules runner and make the new visual catalog test pass**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: PASS for the new visual catalog test, while the popup embedded-image test still fails until later rounds.

- [ ] **Step 7: Commit the shared resource model**

Run:

```bash
git add scripts/AdventureUpgradeCardVisualEntryResource.cs scripts/AdventureUpgradeCardVisualCatalogResource.cs resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres tests/Rules.Tests/Program.cs
git commit -m "feat: add adventure upgrade visual catalog resource"
```

---

### Task 3: Round 2 Create A Shared Runtime Visual Resolver And Migrate Gameplay Usage

**Files:**
- Create: `scripts/AdventureUpgradeCardVisualResolver.cs`
- Modify: `scripts/GameBoardScene.AdventureUpgrades.cs`
- Modify: `scripts/GameBoardScene.cs` if startup loading location needs a small hook
- Test: `tests/Rules.Tests/Program.cs`
- Test: `tests/AdventureUpgradeBigCaptureVictoryOrderSmoke.gd`

- [ ] **Step 1: Add failing rules tests that demand runtime use the shared catalog**

Add two failing tests to `Program.cs`:

```csharp
Run("adventure upgrade visual resolver returns atlas texture from catalog", AdventureUpgradeVisualResolverReturnsAtlasTextureFromCatalog);
Run("adventure upgrade runtime no longer hardcodes popup art path switch tables", AdventureUpgradeRuntimeNoLongerHardcodesPopupArtSwitchTables);
```

The second test should read the text of `scripts/GameBoardScene.AdventureUpgrades.cs` and fail if it still contains the hardcoded processed PNG path switch tables after migration.

- [ ] **Step 2: Run the rules runner and confirm both new tests fail**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: FAIL because the resolver class does not exist and the runtime file still contains hardcoded path/region switches.

- [ ] **Step 3: Implement a resolver that loads the catalog once and returns atlas textures**

Create `scripts/AdventureUpgradeCardVisualResolver.cs` with a focused responsibility:

- lazy-load `res://resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres`
- expose `TryGetEntry(suit, tier, out AdventureUpgradeCardVisualEntryResource entry)`
- expose `Texture2D? LoadAtlasTexture(string suit, string tier)`
- internally load `Texture2D` via `GD.Load<Texture2D>(entry.TexturePath)`
- wrap that texture in `AtlasTexture` using `entry.Region`

Cache the loaded catalog and per-key atlas textures so runtime selection overlay setup does not rebuild the same atlas on every focus refresh.

- [ ] **Step 4: Migrate gameplay overlay code to use the resolver and delete hardcoded switches**

In `scripts/GameBoardScene.AdventureUpgrades.cs`:

- replace `LoadAdventureUpgradeCardTexture`, `GetAdventureUpgradeCardTextureRegion`, and `GetAdventureUpgradeCardTexturePath` hardcoded switch usage with resolver calls
- keep `MapAdventureUpgradeRarityToTextureTier` and `NormalizeAdventureUpgradeTextureKey` only if still needed to normalize gameplay rarity to the visual catalog tier names
- do not change overlay button composition, palette selection, or trigger ordering in this round

When finished, the file should no longer contain literal path tables for the 12 processed PNGs.

- [ ] **Step 5: Re-run rules and adventure-upgrade smoke coverage**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
```

Expected:

- rules runner passes the new resolver tests
- big-capture/victory-order smoke still passes

- [ ] **Step 6: Commit the runtime migration**

Run:

```bash
git add scripts/AdventureUpgradeCardVisualResolver.cs scripts/GameBoardScene.AdventureUpgrades.cs tests/Rules.Tests/Program.cs
git commit -m "refactor: route adventure upgrade overlay art through visual catalog"
```

---

### Task 4: Round 3 Rebuild The Editor Preview Scene On External Resources Only

**Files:**
- Modify: `scripts/UpgradeChoicePopupPreview.gd`
- Modify: `scenes/upgrade_choice_popup_asset_scene.tscn`
- Create: `tests/UpgradeChoicePopupAssetSceneVisualAudit.gd`
- Modify: `tests/Rules.Tests/Program.cs`
- Test: `tests/UpgradeChoicePopupAssetSceneVisualAudit.gd`
- Test: `tools/audit_adventure_upgrade_visual_resources.py`

- [ ] **Step 1: Add a failing editor-scene visual audit smoke test**

Create `tests/UpgradeChoicePopupAssetSceneVisualAudit.gd` that:

- loads `res://scenes/upgrade_choice_popup_asset_scene.tscn`
- instantiates it
- verifies there are 12 card preview `TextureRect` nodes
- verifies every preview node resolves to a `Texture2D` or `AtlasTexture`
- verifies there are no null textures after one frame

The test should intentionally fail until the scene is rebuilt.

- [ ] **Step 2: Run the new headless scene audit and confirm it fails before migration**

Use the same Godot executable discovery pattern as existing `tests/run_*.sh` scripts. Run the script directly with headless Godot:

```bash
"${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}" --headless --path . --script res://tests/UpgradeChoicePopupAssetSceneVisualAudit.gd
```

Expected: FAIL or null-texture problems before the migration is complete.

- [ ] **Step 3: Rewrite the preview script so it only binds imported textures or atlas textures**

In `scripts/UpgradeChoicePopupPreview.gd`:

- keep the scene-binding responsibilities small
- stop using `Image.new()`, `image.load(...)`, and `ImageTexture.create_from_image(...)`
- preload or load the shared visual catalog resource
- bind preview nodes by suit/tier through the shared resolver contract or a small GDScript helper that mirrors its data source

If GDScript cannot directly instantiate the C# resolver cleanly in-tool, read the `.tres` catalog directly from GDScript instead of duplicating path tables in the scene script.

- [ ] **Step 4: Rebuild and re-save the popup scene so it contains no embedded image payloads**

Open and re-save `scenes/upgrade_choice_popup_asset_scene.tscn` after updating bindings so the built-in `Image` and `ImageTexture` subresources disappear.

The saved scene should only contain layout/theme data plus external resource references needed for previewing.

- [ ] **Step 5: Run the audit script, file-size check, and visual scene smoke after the scene rewrite**

Run:

```bash
python3 tools/audit_adventure_upgrade_visual_resources.py
wc -c scenes/upgrade_choice_popup_asset_scene.tscn
"${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}" --headless --path . --script res://tests/UpgradeChoicePopupAssetSceneVisualAudit.gd
```

Expected:

- audit script passes
- popup scene file size drops to a normal editor-scene range rather than hundreds of MB
- scene audit passes with all 12 preview nodes textured

- [ ] **Step 6: Re-run the rules runner so the embedded-image governance test passes**

Run:

```bash
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Expected: PASS for `adventure upgrade popup scene has no embedded image payloads`.

- [ ] **Step 7: Commit the editor-scene migration**

Run:

```bash
git add scripts/UpgradeChoicePopupPreview.gd scenes/upgrade_choice_popup_asset_scene.tscn tests/UpgradeChoicePopupAssetSceneVisualAudit.gd tools/audit_adventure_upgrade_visual_resources.py tests/Rules.Tests/Program.cs
git commit -m "fix: remove embedded popup card textures from editor scene"
```

---

### Task 5: Round 4 Align Editor Preview And Runtime On One Data Flow

**Files:**
- Modify: `scripts/AdventureUpgradeChoiceArt.cs`
- Modify: `scripts/UpgradeChoicePopupPreview.gd`
- Modify: `scenes/adventure_upgrade_selection_overlay.tscn`
- Modify: `scenes/upgrade_choice_popup_asset_scene.tscn`
- Test: `tests/GodotSceneSmoke.gd`
- Test: `tests/UpgradeChoicePopupAssetSceneVisualAudit.gd`

- [ ] **Step 1: Add a failing rules or smoke assertion that preview and runtime use the same crop metadata source**

Choose one of these low-risk assertions:

- a `Program.cs` text check that the runtime file no longer owns crop rect literals and the preview script no longer owns duplicated path maps
- or a GDScript smoke that loads the catalog and confirms one sample runtime card crop and one sample preview card crop match the same region

Make the chosen assertion fail before the duplication is removed.

- [ ] **Step 2: Remove duplicate art metadata from preview-side script code**

Refactor `scripts/UpgradeChoicePopupPreview.gd` so it does not carry its own 12-entry path map. It must consume either:

- the shared `.tres` catalog directly, or
- a thin exported scene-level mapping derived from that catalog

Do not keep two independent path tables after this round.

- [ ] **Step 3: Make runtime sample art in the overlay scene point back to the same catalog definitions where practical**

In `scenes/adventure_upgrade_selection_overlay.tscn`, preserve the current visual behavior but reduce duplication where low risk. Acceptable approaches:

- keep sample ext_resources if they are only decorative and clearly comment their relationship in the plan notes
- or replace the sample ext_resources with catalog-driven setup in code if that can be done without destabilizing the overlay scene

This round is about metadata alignment, not about redesigning the overlay scene.

- [ ] **Step 4: Run broad scene smoke and targeted preview smoke**

Run:

```bash
"${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}" --headless --path . --script res://tests/GodotSceneSmoke.gd
"${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}" --headless --path . --script res://tests/UpgradeChoicePopupAssetSceneVisualAudit.gd
```

Expected: both pass.

- [ ] **Step 5: Commit the metadata-alignment work**

Run:

```bash
git add scripts/AdventureUpgradeChoiceArt.cs scripts/UpgradeChoicePopupPreview.gd scenes/adventure_upgrade_selection_overlay.tscn scenes/upgrade_choice_popup_asset_scene.tscn tests/GodotSceneSmoke.gd tests/UpgradeChoicePopupAssetSceneVisualAudit.gd tests/Rules.Tests/Program.cs
git commit -m "refactor: align upgrade preview and runtime art metadata"
```

---

### Task 6: Round 5 Isolate Source Art From Runtime-Facing Assets And Document The Pipeline

**Files:**
- Create: `docs/ADVENTURE_UPGRADE_ART_PIPELINE.md`
- Modify: `generated-images/upgrade-popup-assets/transparent-processing-meta.json` only if source paths need normalization
- Create or modify: `tools/README`-style note or a small shell helper under `tools/`
- Move or document: `generated-images/upgrade-popup-assets/raw/*` and concept art handling
- Test: repository grep checks

- [ ] **Step 1: Decide and document one of two supported raw-art isolation strategies**

Use exactly one strategy and document it clearly:

Strategy A:
- keep `raw/` inside the repo but outside gameplay lookup code
- enforce that runtime/resource catalogs only point to `/processed/`

Strategy B:
- move `raw/` and concept art under a non-runtime source-art directory such as `art_source/` or `docs/art_source/`
- preserve `processed/` assets under `res://generated-images/.../processed/`

Given the repo currently stores `.import` files for raw assets, Strategy A is the lower-risk migration and should be the default unless the user explicitly wants source-art relocation now.

- [ ] **Step 2: Write the art pipeline doc with explicit contracts**

Create `docs/ADVENTURE_UPGRADE_ART_PIPELINE.md` with these sections:

- Purpose
- Directory layout
- Runtime-allowed assets
- Source-only assets
- How crop regions are governed
- How to add a new suit/tier visual
- What must be updated if a processed PNG is replaced
- What must never be serialized into scenes

This doc must explicitly state:

- runtime code only references `processed/`
- raw concept art is not to be referenced by gameplay scenes or gameplay code
- preview scenes must bind imported textures, not generated image bytes

- [ ] **Step 3: Add a repo-grep validation command for runtime/raw separation**

Document and run this check:

```bash
rg -n "generated-images/upgrade-popup-assets/.*/raw/" scripts scenes resources tests -g '!**/.godot/**'
```

Expected after cleanup: no runtime code, gameplay scenes, or resource catalogs reference `/raw/`.

- [ ] **Step 4: Commit the pipeline documentation work**

Run:

```bash
git add docs/ADVENTURE_UPGRADE_ART_PIPELINE.md generated-images/upgrade-popup-assets/transparent-processing-meta.json tools
git commit -m "docs: define adventure upgrade art pipeline"
```

If `tools/` was not changed, omit it from `git add` and commit only the files actually touched.

---

### Task 7: Round 6 Add Permanent Repository Guardrails And Final Verification

**Files:**
- Modify: `tools/export_preflight.sh`
- Create: `tests/run_adventure_upgrade_visual_resource_audit.sh`
- Modify: `tests/run_multiplayer_reconnect_smoke_suite.sh` only if adding the new audit is low risk and fast
- Modify: `docs/RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md`
- Test: all listed verification commands

- [ ] **Step 1: Add a shell wrapper for the visual-resource audit**

Create `tests/run_adventure_upgrade_visual_resource_audit.sh` using the repo's shell-script style:

```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

python3 tools/audit_adventure_upgrade_visual_resources.py
wc -c scenes/upgrade_choice_popup_asset_scene.tscn
```

If you want the script to enforce a size ceiling, add a Python or shell assertion after `wc -c` so the command exits non-zero when the popup scene crosses the chosen threshold.

- [ ] **Step 2: Extend the audit script into a repo-wide scanner with explicit thresholds**

Expand `tools/audit_adventure_upgrade_visual_resources.py` so it scans all project-owned `.tscn` and `.tres` files under these roots:

- `scenes/`
- `resources/`

Exclude third-party addon directories.

Enforce these rules:

- fail if project-owned scenes/resources contain `[sub_resource type="Image"]` unless explicitly whitelisted
- fail if project-owned scenes/resources contain suspiciously large `PackedByteArray(` payloads
- fail if any project-owned scene exceeds a documented byte threshold such as `1_000_000` bytes unless whitelisted with a reason

Keep the popup preview scene as the primary check, but make the script future-proof for other scenes too.

- [ ] **Step 3: Add the new audit to an existing preflight path**

Preferred order:

1. add it to `tools/export_preflight.sh`
2. optionally add it to `tests/run_multiplayer_reconnect_smoke_suite.sh` only if runtime remains acceptable

Use this order inside `tools/export_preflight.sh`:

```bash
python3 tools/audit_adventure_upgrade_visual_resources.py
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
```

Do not place the new audit behind slower multiplayer smoke if the goal is fast failure.

- [ ] **Step 4: Run the final verification matrix**

Run:

```bash
python3 tools/audit_adventure_upgrade_visual_resources.py
bash tests/run_adventure_upgrade_visual_resource_audit.sh
dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
bash tests/run_adventure_upgrade_big_capture_victory_order_smoke.sh
bash tests/run_multiplayer_reconnect_smoke_suite.sh
bash tools/export_preflight.sh
```

Expected final state:

- all commands pass
- popup preview scene is no longer abnormally large
- no project-owned scene or resource embeds card image bytes
- runtime overlay still behaves correctly under adventure-upgrade smoke
- preflight blocks regression if someone reintroduces built-in card images later

- [ ] **Step 5: Update the resource baseline doc with final measured outcomes**

In `docs/RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md`, replace the initial failure-state notes with final measured outcomes, including:

- final popup scene byte size
- zero embedded `Image` and `ImageTexture` counts
- commands that now pass
- explicit regression rules for future popup/editor scenes

- [ ] **Step 6: Commit the final governance layer**

Run:

```bash
git add tools/audit_adventure_upgrade_visual_resources.py tests/run_adventure_upgrade_visual_resource_audit.sh tests/run_multiplayer_reconnect_smoke_suite.sh tools/export_preflight.sh docs/RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md
git commit -m "test: enforce adventure upgrade visual resource guardrails"
```

---

## File Map For The Whole Plan

### New Files Expected

- `tools/audit_adventure_upgrade_visual_resources.py`
- `docs/RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md`
- `scripts/AdventureUpgradeCardVisualEntryResource.cs`
- `scripts/AdventureUpgradeCardVisualCatalogResource.cs`
- `resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres`
- `scripts/AdventureUpgradeCardVisualResolver.cs`
- `tests/UpgradeChoicePopupAssetSceneVisualAudit.gd`
- `docs/ADVENTURE_UPGRADE_ART_PIPELINE.md`
- `tests/run_adventure_upgrade_visual_resource_audit.sh`

### Existing Files Expected To Change

- `tests/Rules.Tests/Program.cs`
- `scripts/GameBoardScene.AdventureUpgrades.cs`
- `scripts/UpgradeChoicePopupPreview.gd`
- `scenes/upgrade_choice_popup_asset_scene.tscn`
- `scripts/AdventureUpgradeChoiceArt.cs`
- `scenes/adventure_upgrade_selection_overlay.tscn`
- `tools/export_preflight.sh`
- `tests/run_multiplayer_reconnect_smoke_suite.sh` only if the extra audit cost is acceptable
- `generated-images/upgrade-popup-assets/transparent-processing-meta.json` only if source-art documentation paths need normalization

## Non-Goals To Enforce During Execution

- Do not redesign the look of the popup scene.
- Do not change upgrade rarity weighting or offer composition logic.
- Do not move multiplayer tests or network logic.
- Do not migrate every generated image family in the repo to the same system during this plan. This plan covers the adventure-upgrade popup art pipeline only.
- Do not convert this into a generalized art-management framework for all future assets.

## Completion Checklist

The plan is complete only when all of these are true:

- `scenes/upgrade_choice_popup_asset_scene.tscn` is back to a normal scene size and no longer serializes image bytes.
- The runtime overlay and the editor preview both consume the same visual metadata source.
- The processed adventure-upgrade PNGs remain the only runtime-facing card-art assets in this feature area.
- A repo-level audit fails if someone reintroduces built-in image payloads or oversized project scenes.
- Build, rules tests, targeted adventure-upgrade smoke, and broad multiplayer smoke all pass.
