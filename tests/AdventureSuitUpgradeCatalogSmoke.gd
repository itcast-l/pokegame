extends SceneTree

const CATEGORY_ID := "adventure_suit_upgrades"
const VISUAL_CATALOG := preload("res://resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres")
const EXPECTED_TOTAL := 54
const EXPECTED_SUIT_COUNTS := {
	"hearts": 14,
	"diamonds": 14,
	"spades": 12,
	"clubs": 14,
}
const RETIRED_IDS := {
	"hearts_blood_tide_resonance": true,
	"diamonds_all_gains_converge": true,
	"spades_black_tide_dominion": true,
	"clubs_lush_growth": true,
}

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var pandora = root.get_node_or_null("Pandora")
	var deadline := Time.get_ticks_msec() + 5000
	while (pandora == null or not pandora.is_loaded()) and Time.get_ticks_msec() < deadline:
		await process_frame
		pandora = root.get_node_or_null("Pandora")
	if pandora == null or not pandora.is_loaded():
		_fail("Pandora was not loaded")
		return

	var category = pandora.get_category(CATEGORY_ID)
	if category == null:
		_fail("Missing Pandora category: %s" % CATEGORY_ID)
		return

	var all_definitions: Array = []
	for entity in pandora.get_all_entities(category):
		if entity == null:
			continue
		var definition_id := String(entity.get_entity_id())
		all_definitions.append({
			"id": definition_id,
			"suit": _get_entity_string(entity, "suit"),
			"rarity": _get_entity_string(entity, "rarity"),
			"description_key": _get_description_key(entity, definition_id),
			"effect_text": _get_entity_string(entity, "effect_text"),
			"enabled": entity.get_bool("enabled"),
		})
	_assert(all_definitions.size() == EXPECTED_TOTAL, "Expected %d total definitions, got %d" % [EXPECTED_TOTAL, all_definitions.size()])
	if _failed:
		return

	for suit_name in EXPECTED_SUIT_COUNTS:
		var suit_definition_count := 0
		for definition in all_definitions:
			if String(definition.get("suit", "")) == suit_name:
				suit_definition_count += 1
		var expected_count: int = EXPECTED_SUIT_COUNTS[suit_name]
		_assert(suit_definition_count == expected_count, "Expected %d %s definitions, got %d" % [expected_count, suit_name, suit_definition_count])
		if _failed:
			return

	var rarity_counts := {
		"common": 0,
		"rare": 0,
		"legendary": 0,
	}
	var enabled_ids := {}

	for definition in all_definitions:
		var definition_id := String(definition.get("id", ""))
		var suit := String(definition.get("suit", ""))
		var rarity := String(definition.get("rarity", ""))
		var description_key := String(definition.get("description_key", ""))
		var effect_text := String(definition.get("effect_text", ""))
		var enabled := bool(definition.get("enabled", false))
		enabled_ids[definition_id] = true

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
		_assert(
			not description_key.is_empty() or not effect_text.is_empty(),
			"Definition %s should expose either description_key or effect_text fallback" % definition_id
		)
		if _failed:
			return

		var texture = _resolve_visual_texture(suit, rarity)
		_assert(texture != null, "Definition %s should resolve a card texture" % definition_id)
		if _failed:
			return

	_assert(rarity_counts["common"] == 24, "Expected 24 common definitions, got %d" % rarity_counts["common"])
	_assert(rarity_counts["rare"] == 20, "Expected 20 rare definitions, got %d" % rarity_counts["rare"])
	_assert(rarity_counts["legendary"] == 10, "Expected 10 legendary definitions, got %d" % rarity_counts["legendary"])
	for retired_id in RETIRED_IDS:
		_assert(not enabled_ids.has(retired_id), "Retired definition %s must not be enabled" % retired_id)
	if _failed:
		return

	print("ADVENTURE_SUIT_UPGRADE_CATALOG_SMOKE_OK")
	quit(0)


func _resolve_visual_texture(suit: String, rarity: String):
	var tier := rarity
	for mapping in VISUAL_CATALOG.get("RarityTierMappings"):
		if String(mapping.get("Rarity")) == rarity:
			tier = String(mapping.get("Tier"))
			break

	for entry in VISUAL_CATALOG.get("Entries"):
		if String(entry.get("Suit")) != suit or String(entry.get("Tier")) != tier:
			continue
		var texture_path := String(entry.get("TexturePath"))
		return load(texture_path) as Texture2D
	return null


func _get_entity_string(entity, property_name: String) -> String:
	return String(entity.get_string(property_name)) if entity.has_entity_property(property_name) else ""


func _get_description_key(entity, definition_id: String) -> String:
	var description_key := _get_entity_string(entity, "description_key")
	return description_key if not description_key.is_empty() else "UPGRADE_DESC_%s" % definition_id.to_upper()


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
