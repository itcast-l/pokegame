# Adventure Suit Upgrades Config And Plugin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the new `hearts` adventure suit upgrades as formal Pandora-backed game configuration and provide a dedicated Godot editor plugin to manage them without wiring them into gameplay yet.

**Architecture:** Use the existing Pandora data system as the source of truth by adding a new `adventure_suit_upgrades` category with 14 seeded entities from the workbook. Add a small typed runtime catalog in C# for future consumers, and add a focused editor dock plugin that edits this category through Pandora save APIs instead of inventing a separate config backend.

**Tech Stack:** Godot 4 editor plugins in GDScript, Pandora addon/data model, C# runtime catalog classes, workbook content from `outputs/heart_pool_sheet/adventure_hearts_pool.xlsx`

---

### Task 1: Lock The Seed Data And Plan IDs

**Files:**
- Create: `docs/superpowers/plans/2026-06-28-adventure-suit-upgrades-config-and-plugin.md`
- Read: `outputs/heart_pool_sheet/adventure_hearts_pool.xlsx`

- [ ] **Step 1: Confirm the workbook rows and derived IDs**

Use these 14 entries exactly, in this order:

```text
1. hearts_warm_current_supply
2. hearts_long_capture_recovery
3. hearts_miss_bandage
4. hearts_question_mark_first_aid
5. hearts_shoal_preparation
6. hearts_thick_blood
7. hearts_chain_tide_breath
8. hearts_red_tide_return
9. hearts_supply_cache_return
10. hearts_enchanted_recovery
11. hearts_deep_tide_lung
12. hearts_return_tide_heart
13. hearts_revival_draw
14. hearts_overflow_growth
```

- [ ] **Step 2: Lock the row-to-field mapping**

Map workbook columns like this:

```text
Rarity -> rarity
Name -> Pandora entity name
Trigger -> trigger_text
Effect -> effect_text
Notes -> notes
```

- [ ] **Step 3: Lock default config values**

Use these defaults for all 14 entities:

```text
suit = hearts
enabled = true
sort_order = 10, 20, 30 ... 140
```

- [ ] **Step 4: Preserve non-row workbook metadata as plugin help text only**

Do not create Pandora entities for:

```text
Scope = Adventure mode only
Status = Design draft
Rule Note = Max HP abilities require shared Current HP / Max HP across the run.
Count = 14
```

Instead, show them as read-only help text in the editor dock.

- [ ] **Step 5: Commit after implementation**

```bash
git add docs/superpowers/plans/2026-06-28-adventure-suit-upgrades-config-and-plugin.md
git commit -m "docs: add adventure suit upgrades implementation plan"
```


### Task 2: Add Pandora Category And Seed Data

**Files:**
- Modify: `data.pandora`
- Modify: `pandora/categories.gd`
- Create or modify: `pandora/adventure_suit_upgrade_ids.gd`
- Read: `pandora/item_card_ids.gd`

- [ ] **Step 1: Add the new Pandora category**

Mirror the existing `item_cards` category structure in `data.pandora` and add a root category with:

```json
{
  "_category_id": "",
  "_generate_ids": true,
  "_icon_color": "00fff7ff",
  "_id": "adventure_suit_upgrades",
  "_ids_generation_class": "AdventureSuitUpgradeIds",
  "_name": "Adventure Suit Upgrades"
}
```

- [ ] **Step 2: Add the category property schema**

Create category properties for `adventure_suit_upgrades` with these names and types:

```text
suit -> string
rarity -> string
trigger_text -> string
effect_text -> string
notes -> string
enabled -> bool
sort_order -> int
```

No `id`, `display_name`, `description_key`, or gameplay-effect fields should be added here.

- [ ] **Step 3: Seed the 14 Pandora entities**

For each upgrade, create one entity under `adventure_suit_upgrades` with:

```text
_id = the locked seed ID
_name = workbook Name
property overrides:
  suit = hearts
  rarity = common|rare|legendary
  trigger_text = workbook Trigger
  effect_text = workbook Effect
  notes = workbook Notes
  enabled = true
  sort_order = 10..140
```

- [ ] **Step 4: Regenerate Pandora ID helper files**

After the category and entities exist, regenerate the Pandora output helpers so the repo contains the new category constant and entity ID class.

Expected additions:

```gdscript
# pandora/categories.gd
const ADVENTURE_SUIT_UPGRADES = "adventure_suit_upgrades"
```

```gdscript
# pandora/adventure_suit_upgrade_ids.gd
class_name AdventureSuitUpgradeIds
const HEARTS_WARM_CURRENT_SUPPLY = "hearts_warm_current_supply"
...
const HEARTS_OVERFLOW_GROWTH = "hearts_overflow_growth"
```

- [ ] **Step 5: Verify the seeded data is structurally correct**

Check that:

```text
- 14 entities exist
- all have suit=hearts
- rarity counts are 6 common, 5 rare, 3 legendary
- sort_order is strictly ascending
```

- [ ] **Step 6: Commit after implementation**

```bash
git add data.pandora pandora/categories.gd pandora/adventure_suit_upgrade_ids.gd
git commit -m "feat: add adventure suit upgrades pandora config"
```


### Task 3: Add The Runtime Catalog

**Files:**
- Create: `scripts/AdventureSuitUpgradeDefinition.cs`
- Create: `scripts/AdventureSuitUpgradeCatalog.cs`
- Test: `tests/AdventureSuitUpgradeCatalogSmoke.gd`

- [ ] **Step 1: Write the failing runtime smoke test**

Create a Godot smoke test script that expects a runtime catalog with this shape:

```gdscript
extends SceneTree

func _init():
	var host := Node.new()
	root.add_child(host)
	var catalog = AdventureSuitUpgradeCatalog.Load(host)
	assert(catalog.All.Count == 14)
	var hearts = catalog.GetDefinitionsForSuit("hearts")
	assert(hearts.Count == 14)
	var first_ok = false
	var last_ok = false
	var common_count := 0
	var rare_count := 0
	var legendary_count := 0
	for definition in catalog.All:
		assert(definition.Suit == "hearts")
		assert(definition.Enabled)
		match definition.Rarity:
			"common":
				common_count += 1
			"rare":
				rare_count += 1
			"legendary":
				legendary_count += 1
		if definition.Id == "hearts_warm_current_supply":
			first_ok = definition.DisplayName == "温流补给"
		if definition.Id == "hearts_overflow_growth":
			last_ok = definition.DisplayName == "过量生长"
	assert(common_count == 6)
	assert(rare_count == 5)
	assert(legendary_count == 3)
	assert(first_ok)
	assert(last_ok)
	quit()
```

- [ ] **Step 2: Run the smoke test to confirm it fails**

Run a Godot headless command that targets the test script.

Expected: failure because `AdventureSuitUpgradeCatalog` does not exist yet.

- [ ] **Step 3: Add the definition type**

Create `scripts/AdventureSuitUpgradeDefinition.cs`:

```csharp
public readonly struct AdventureSuitUpgradeDefinition
{
	public AdventureSuitUpgradeDefinition(
		string id,
		string displayName,
		string suit,
		string rarity,
		string triggerText,
		string effectText,
		string notes,
		bool enabled,
		int sortOrder)
	{
		Id = id;
		DisplayName = displayName;
		Suit = suit;
		Rarity = rarity;
		TriggerText = triggerText;
		EffectText = effectText;
		Notes = notes;
		Enabled = enabled;
		SortOrder = sortOrder;
	}

	public string Id { get; }
	public string DisplayName { get; }
	public string Suit { get; }
	public string Rarity { get; }
	public string TriggerText { get; }
	public string EffectText { get; }
	public string Notes { get; }
	public bool Enabled { get; }
	public int SortOrder { get; }
}
```

- [ ] **Step 4: Add the catalog loader**

Create `scripts/AdventureSuitUpgradeCatalog.cs` with a Pandora-backed loader that:

```csharp
using Godot;
using System;
using System.Collections.Generic;

public sealed class AdventureSuitUpgradeCatalog
{
	private const string CategoryId = "adventure_suit_upgrades";

	private readonly List<AdventureSuitUpgradeDefinition> _all = new();
	private readonly Dictionary<string, AdventureSuitUpgradeDefinition> _byId = new(StringComparer.Ordinal);

	public IReadOnlyList<AdventureSuitUpgradeDefinition> All => _all;

	public static AdventureSuitUpgradeCatalog Load(Node sceneNode)
	{
		var catalog = new AdventureSuitUpgradeCatalog();
		Node? pandora = sceneNode.GetNodeOrNull<Node>("/root/Pandora");
		if (pandora == null || !(bool)pandora.Call("is_loaded"))
		{
			GD.PushWarning("Adventure suit upgrades catalog unavailable because Pandora is not loaded.");
			return catalog;
		}

		GodotObject? category = pandora.Call("get_category", CategoryId).AsGodotObject();
		if (category == null)
		{
			GD.PushWarning($"Missing Pandora category: {CategoryId}");
			return catalog;
		}

		Godot.Collections.Array entities = pandora.Call("get_all_entities", category).AsGodotArray();
		var definitions = new List<AdventureSuitUpgradeDefinition>();
		foreach (Variant entityVariant in entities)
		{
			GodotObject? entity = entityVariant.AsGodotObject();
			if (entity == null)
			{
				continue;
			}

			string id = entity.Call("get_entity_id").AsString();
			string name = entity.Call("get_entity_name").AsString();
			string suit = entity.Call("get_string", "suit").AsString();
			string rarity = entity.Call("get_string", "rarity").AsString();
			string triggerText = entity.Call("get_string", "trigger_text").AsString();
			string effectText = entity.Call("get_string", "effect_text").AsString();
			string notes = entity.Call("get_string", "notes").AsString();
			bool enabled = entity.Call("get_bool", "enabled").AsBool();
			int sortOrder = entity.Call("get_integer", "sort_order").AsInt32();

			definitions.Add(new AdventureSuitUpgradeDefinition(
				id,
				name,
				suit,
				rarity,
				triggerText,
				effectText,
				notes,
				enabled,
				sortOrder));
		}

		definitions.Sort((a, b) =>
		{
			int orderComparison = a.SortOrder.CompareTo(b.SortOrder);
			return orderComparison != 0 ? orderComparison : string.CompareOrdinal(a.Id, b.Id);
		});

		foreach (AdventureSuitUpgradeDefinition definition in definitions)
		{
			catalog._all.Add(definition);
			catalog._byId[definition.Id] = definition;
		}

		return catalog;
	}

	public bool TryGetDefinition(string id, out AdventureSuitUpgradeDefinition definition)
	{
		return _byId.TryGetValue(id, out definition);
	}

	public IReadOnlyList<AdventureSuitUpgradeDefinition> GetDefinitionsForSuit(string suit)
	{
		var results = new List<AdventureSuitUpgradeDefinition>();
		foreach (AdventureSuitUpgradeDefinition definition in _all)
		{
			if (string.Equals(definition.Suit, suit, StringComparison.Ordinal))
			{
				results.Add(definition);
			}
		}

		return results;
	}
}
```

- [ ] **Step 5: Run the smoke test to confirm it passes**

Run the same Godot headless command again.

Expected: PASS with 14 loaded entries and correct rarity counts.

- [ ] **Step 6: Commit after implementation**

```bash
git add scripts/AdventureSuitUpgradeDefinition.cs scripts/AdventureSuitUpgradeCatalog.cs tests/AdventureSuitUpgradeCatalogSmoke.gd
git commit -m "feat: add adventure suit upgrades runtime catalog"
```


### Task 4: Add The Editor Plugin Shell

**Files:**
- Create: `addons/adventure_suit_upgrades/plugin.cfg`
- Create: `addons/adventure_suit_upgrades/plugin.gd`
- Create: `addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.gd`
- Create: `addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.tscn`

- [ ] **Step 1: Write the failing editor smoke check**

Add a manual smoke checklist first:

```text
1. Enable the plugin in Project Settings > Plugins.
2. Confirm a left dock named "Adventure Suit Upgrades" appears.
3. Confirm no save errors occur on editor load.
```

- [ ] **Step 2: Add the plugin manifest**

Create `addons/adventure_suit_upgrades/plugin.cfg`:

```ini
[plugin]
name="Adventure Suit Upgrades"
description="Manage Pandora-backed adventure suit upgrade config."
author="OpenAI Codex"
version="1.0"
script="res://addons/adventure_suit_upgrades/plugin.gd"
```

- [ ] **Step 3: Add the editor plugin**

Create `addons/adventure_suit_upgrades/plugin.gd`:

```gdscript
@tool
extends EditorPlugin

const DOCK_SCENE := preload("res://addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.tscn")

var _dock: Control

func _enter_tree() -> void:
	_dock = DOCK_SCENE.instantiate()
	_dock.name = "Adventure Suit Upgrades"
	add_control_to_dock(DOCK_SLOT_LEFT_UL, _dock)

func _exit_tree() -> void:
	if _dock != null:
		remove_control_from_docks(_dock)
		_dock.queue_free()
		_dock = null
```

- [ ] **Step 4: Add a minimal dock scene and script**

Create a basic dock scene with:

```text
VBoxContainer
- Label (title)
- RichTextLabel (help text)
- HBoxContainer (Save / Reload / Open in Pandora / Repair Schema)
- ItemList or VBoxContainer placeholder for rows
```

Create the companion script with empty handlers and a `refresh()` call in `_ready()`.

- [ ] **Step 5: Enable the plugin and verify the dock appears**

Expected:

```text
- plugin loads without parse errors
- dock appears in the left dock area
- title/help text is visible
```

- [ ] **Step 6: Commit after implementation**

```bash
git add addons/adventure_suit_upgrades/plugin.cfg addons/adventure_suit_upgrades/plugin.gd addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.gd addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.tscn
git commit -m "feat: scaffold adventure suit upgrades editor plugin"
```


### Task 5: Wire The Dock To Pandora Data

**Files:**
- Modify: `addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.gd`
- Modify: `addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.tscn`

- [ ] **Step 1: Build read-only loading first**

Load Pandora and gather the category/entity list in `sort_order` order. If Pandora is unavailable or the category is missing, show a blocking warning message instead of row editors.

Core loading logic:

```gdscript
func _get_pandora() -> Node:
	return get_node_or_null("/root/Pandora")

func _get_category():
	var pandora = _get_pandora()
	if pandora == null or not pandora.is_loaded():
		return null
	return pandora.get_category("adventure_suit_upgrades")
```

- [ ] **Step 2: Render the 14 rows**

For each entity, show:

```text
read-only: id, suit
editable: name, rarity, trigger_text, effect_text, notes, enabled, sort_order
```

Use one row container per entity and preserve workbook order via `sort_order`.

- [ ] **Step 3: Implement Save**

On save, write field values back to Pandora entity properties using:

```gdscript
entity.set_entity_name(name_edit.text)
entity.get_entity_property("rarity").set_default_value(rarity_edit.text)
entity.get_entity_property("trigger_text").set_default_value(trigger_edit.text)
entity.get_entity_property("effect_text").set_default_value(effect_edit.text)
entity.get_entity_property("notes").set_default_value(notes_edit.text)
entity.get_entity_property("enabled").set_default_value(enabled_check.button_pressed)
entity.get_entity_property("sort_order").set_default_value(int(sort_order_spin.value))
Pandora.save_data()
```

Do not edit `id` or `suit` in the dock.

- [ ] **Step 4: Implement Reload**

Clear the row container and rebuild from Pandora state after save or manual reload.

- [ ] **Step 5: Implement Open In Pandora**

Use the existing Pandora main screen if possible. Minimum acceptable behavior: focus the Pandora editor or show a message telling the user to open the Pandora tab to edit raw entities.

- [ ] **Step 6: Verify editor persistence**

Manual verification:

```text
1. Change one entry's notes text.
2. Save.
3. Reload the dock.
4. Restart the editor.
5. Confirm the changed notes text persists in the dock and in data.pandora.
```

- [ ] **Step 7: Commit after implementation**

```bash
git add addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.gd addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.tscn data.pandora
git commit -m "feat: wire adventure suit upgrades dock to pandora data"
```


### Task 6: Add Schema Repair And Validation Guards

**Files:**
- Modify: `addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.gd`

- [ ] **Step 1: Detect missing schema properties**

Before rendering rows, verify the category contains:

```text
suit
rarity
trigger_text
effect_text
notes
enabled
sort_order
```

If any are missing, block normal editing and show the missing list.

- [ ] **Step 2: Implement Repair Schema**

Create only the missing properties via Pandora:

```gdscript
Pandora.create_property(category, "suit", "string")
Pandora.create_property(category, "rarity", "string")
Pandora.create_property(category, "trigger_text", "string")
Pandora.create_property(category, "effect_text", "string")
Pandora.create_property(category, "notes", "string")
Pandora.create_property(category, "enabled", "bool")
Pandora.create_property(category, "sort_order", "int")
Pandora.save_data()
```

Guard each creation so existing properties are not duplicated.

- [ ] **Step 3: Validate row completeness before save**

Block save if:

```text
- name is blank
- rarity is blank
- suit is blank
- sort_order is not an integer
```

Allow blank `notes`.

- [ ] **Step 4: Verify repair behavior**

Manual verification:

```text
1. Temporarily remove one schema property in a test branch.
2. Open the dock.
3. Confirm the blocking warning appears.
4. Click Repair Schema.
5. Confirm the property is restored and the editor becomes usable again.
```

- [ ] **Step 5: Commit after implementation**

```bash
git add addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.gd data.pandora
git commit -m "feat: add adventure suit upgrades schema repair"
```


### Task 7: Run Verification

**Files:**
- Read: `tests/AdventureSuitUpgradeCatalogSmoke.gd`
- Read: `data.pandora`
- Read: `addons/adventure_suit_upgrades/*`

- [ ] **Step 1: Run the runtime smoke test**

Run the headless Godot test command for `AdventureSuitUpgradeCatalogSmoke.gd`.

Expected: PASS

- [ ] **Step 2: Run a C# project build check**

Run:

```bash
dotnet build /Volumes/KIOXIA11/godotproject/pokegame/pokegame.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Run the editor plugin smoke pass**

Confirm:

```text
- plugin enables successfully
- dock appears
- 14 rows appear
- workbook order is preserved
- one edit survives save + reload + editor restart
```

- [ ] **Step 4: Inspect the final Pandora data**

Check that:

```text
- category adventure_suit_upgrades exists
- generated class AdventureSuitUpgradeIds exists
- all 14 IDs are present
- no accidental item_cards mutations happened outside intended additions
```

- [ ] **Step 5: Commit final polish if needed**

```bash
git add data.pandora pandora/categories.gd pandora/adventure_suit_upgrade_ids.gd scripts/AdventureSuitUpgradeDefinition.cs scripts/AdventureSuitUpgradeCatalog.cs addons/adventure_suit_upgrades tests/AdventureSuitUpgradeCatalogSmoke.gd
git commit -m "feat: add adventure suit upgrades config and editor management"
```
