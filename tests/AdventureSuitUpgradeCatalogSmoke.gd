extends SceneTree

const CATALOG_SCRIPT := preload("res://scripts/AdventureSuitUpgradeCatalog.cs")
const EXPECTED_TOTAL := 57
const EXPECTED_SUIT_COUNTS := {
	"hearts": 14,
	"diamonds": 14,
	"spades": 15,
	"clubs": 14,
}

const KEY_EXPECTED_NAMES := {
	"hearts_warm_current_supply": "温流补给",
	"diamonds_small_insurance": "小额保价",
	"spades_missed_beat_calamity": "失拍成灾",
	"clubs_crown_canopy": "冠棚",
}

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var host := Node.new()
	root.add_child(host)
	await process_frame

	var catalog = CATALOG_SCRIPT.new()
	if catalog == null:
		_fail("Failed to instantiate AdventureSuitUpgradeCatalog")
		return

	catalog.LoadFromNode(host)
	var all_definitions: Array = catalog.GetAllDefinitions()
	_assert(all_definitions.size() == EXPECTED_TOTAL, "Expected %d total definitions, got %d" % [EXPECTED_TOTAL, all_definitions.size()])
	if _failed:
		return

	for suit_name in EXPECTED_SUIT_COUNTS:
		var suit_definitions: Array = catalog.GetDefinitionsForSuitData(suit_name)
		var expected_count: int = EXPECTED_SUIT_COUNTS[suit_name]
		_assert(suit_definitions.size() == expected_count, "Expected %d %s definitions, got %d" % [expected_count, suit_name, suit_definitions.size()])
		if _failed:
			return

	var expected_name_hits := {}
	var rarity_counts := {
		"common": 0,
		"rare": 0,
		"legendary": 0,
	}

	for definition in all_definitions:
		var definition_id := String(definition.get("id", ""))
		var display_name := String(definition.get("display_name", ""))
		var suit := String(definition.get("suit", ""))
		var rarity := String(definition.get("rarity", ""))
		var enabled := bool(definition.get("enabled", false))

		_assert(EXPECTED_SUIT_COUNTS.has(suit), "Definition %s has unexpected suit=%s" % [definition_id, suit])
		if _failed:
			return

		_assert(enabled, "Definition %s should be enabled" % definition_id)
		if _failed:
			return

		if not rarity_counts.has(rarity):
			_fail("Unexpected rarity %s on %s" % [rarity, definition_id])
			return
		rarity_counts[rarity] += 1

		if KEY_EXPECTED_NAMES.has(definition_id):
			expected_name_hits[definition_id] = display_name == KEY_EXPECTED_NAMES[definition_id]

	_assert(rarity_counts["common"] == 25, "Expected 25 common definitions, got %d" % rarity_counts["common"])
	_assert(rarity_counts["rare"] == 21, "Expected 21 rare definitions, got %d" % rarity_counts["rare"])
	_assert(rarity_counts["legendary"] == 11, "Expected 11 legendary definitions, got %d" % rarity_counts["legendary"])
	for definition_id in KEY_EXPECTED_NAMES:
		_assert(
			bool(expected_name_hits.get(definition_id, false)),
			"Missing expected display name for %s" % definition_id
		)
	if _failed:
		return

	print("ADVENTURE_SUIT_UPGRADE_CATALOG_SMOKE_OK")
	quit(0)


func _assert(condition: bool, message: String) -> void:
	if condition or _failed:
		return

	_fail(message)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_SUIT_UPGRADE_CATALOG_SMOKE_FAIL %s" % message)
	quit(1)
