extends SceneTree

const ITEM_CATEGORY := "item_cards"
const ADVENTURE_CATEGORY := "adventure_suit_upgrades"
const ITEM_RESOURCE_DIR := "res://resources/item_cards"
const PANDORA_PATH := "res://data.pandora"
const REGISTRY_PATH := "res://scripts/ItemCardEffectContractRegistry.cs"
const SPECIAL_CARD_SYSTEM_PATH := "res://scripts/SpecialCardSystem.cs"
const RESOLVER_PATH := "res://scripts/GameBoardScene.SpecialCards.cs"
const PRESENTATION_PATH := "res://scripts/GameBoardScene.ItemCardPlays.cs"
const LOCALIZATION_PATH := "res://localization/ui.csv"

const GAMEPLAY_FIELDS := [
	{"pandora": "id", "resource": "Id", "kind": "string"},
	{"pandora": "enabled_in_game", "resource": "EnabledInGame", "kind": "bool"},
	{"pandora": "playable_from_hand", "resource": "PlayableFromHand", "kind": "bool"},
	{"pandora": "can_be_question_reward", "resource": "CanBeQuestionReward", "kind": "bool"},
	{"pandora": "weight", "resource": "Weight", "kind": "int"},
	{"pandora": "rarity", "resource": "Rarity", "kind": "string"},
	{"pandora": "tags", "resource": "TagsCsv", "kind": "string"},
	{"pandora": "reward_delivery", "resource": "RewardDelivery", "kind": "string"},
	{"pandora": "play_presentation", "resource": "PlayPresentation", "kind": "string"},
	{"pandora": "trigger", "resource": "Trigger", "kind": "string"},
	{"pandora": "effect_type", "resource": "EffectType", "kind": "string"},
	{"pandora": "effect_values", "resource": "EffectValuesRaw", "kind": "string"},
	{"pandora": "priority", "resource": "Priority", "kind": "int"},
	{"pandora": "attached_trigger", "resource": "AttachedTrigger", "kind": "string"},
	{"pandora": "attached_effect_type", "resource": "AttachedEffectType", "kind": "string"},
	{"pandora": "attached_effect_values", "resource": "AttachedEffectValuesRaw", "kind": "string"},
	{"pandora": "attached_priority", "resource": "AttachedPriority", "kind": "int"},
	{"pandora": "description_key", "resource": "DescriptionKey", "kind": "string"},
]

const VISUAL_FIELDS := [
	"Id",
	"SpriteName",
	"ArtRunName",
	"FramePathPattern",
	"FrameCount",
	"SortOrder",
	"AttachmentOverlayTexturePath",
	"AttachmentOverlayPriority",
	"Icon",
]

const EXTRA_FORBIDDEN_VISUAL_FIELDS := ["DisplayName", "Description"]

var _failed := false


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		args = OS.get_cmdline_args()
	if not args.has("--check"):
		_fail("Usage: godot --headless --path . --script res://tools/ItemCardManifestSync.gd --check")
		_quit_failure()
		return
	call_deferred("_run")


func _run() -> void:
	var pandora_root := _load_json(PANDORA_PATH)
	if pandora_root.is_empty():
		_quit_failure()
		return

	var pandora_entities := _get_category_entities(pandora_root, ITEM_CATEGORY)
	if pandora_entities.is_empty():
		_fail("Pandora item card category is empty")
		_quit_failure()
		return

	var defaults := _get_category_defaults(pandora_root, ITEM_CATEGORY)
	var pandora_by_id := {}
	for entity in pandora_entities:
		var id := String(entity.get("_id", "")).strip_edges()
		if id.is_empty():
			_fail("Pandora contains an item entity without an ID")
			continue
		if pandora_by_id.has(id):
			_fail("Pandora contains duplicate item ID %s" % id)
			continue
		var values := {}
		for field in GAMEPLAY_FIELDS:
			var field_name: String = field.pandora
			var found := _get_pandora_property(entity, field_name, defaults, field.kind)
			if not found.found:
				_fail("Pandora item %s is missing gameplay field %s" % [id, field_name])
			else:
				values[field_name] = found.value
		pandora_by_id[id] = values

	var visuals_by_id := _read_visuals()
	if visuals_by_id.is_empty():
		_fail("No item card visual resources were found")
	else:
		_compare_visual_ids(pandora_by_id, visuals_by_id)

	_check_contract_sources(pandora_by_id)
	_check_manifest_value_shapes(pandora_by_id)
	_check_item_localization(pandora_by_id)
	_check_localization_placeholders(pandora_root)

	var hash_lines: Array[String] = []
	for id in pandora_by_id.keys():
		var values: Dictionary = pandora_by_id[id]
		var parts: Array[String] = [String(id)]
		for field in GAMEPLAY_FIELDS:
			parts.append("%s=%s" % [field.pandora, _canonical_value(values.get(field.pandora, ""))])
		hash_lines.append("\t".join(parts))
	hash_lines.sort()
	var gameplay_hash := _sha256("\n".join(hash_lines) + "\n")
	print("GAMEPLAY_MANIFEST_HASH %s" % gameplay_hash)
	if _failed:
		_quit_failure()
	else:
		print("ITEM_CARD_MANIFEST_CHECK_OK")
		quit(0)


func _load_json(path: String) -> Dictionary:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_fail("Unable to read %s" % path)
		return {}
	var parsed = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		_fail("Invalid JSON in %s" % path)
		return {}
	return parsed


func _get_category_entities(root: Dictionary, category_id: String) -> Array:
	var result: Array = []
	var all_entities: Array = root.get("_entity_data", {}).get("_entities", [])
	for entity in all_entities:
		if entity is Dictionary and String(entity.get("_category_id", "")) == category_id:
			result.append(entity)
	return result


func _get_category_defaults(root: Dictionary, category_id: String) -> Dictionary:
	var defaults := {}
	var properties: Array = root.get("_entity_data", {}).get("_properties", [])
	for property in properties:
		if property is Dictionary and String(property.get("_category_id", "")) == category_id:
			defaults[String(property.get("_name", ""))] = property.get("_default_value", null)
	return defaults


func _get_pandora_property(entity: Dictionary, property_name: String, defaults: Dictionary, kind: String) -> Dictionary:
	var overrides: Dictionary = entity.get("_property_overrides", {})
	if overrides.has(property_name):
		var override_value: Dictionary = overrides[property_name]
		return {"found": true, "value": _normalize_value(override_value.get("value"), kind)}
	if defaults.has(property_name):
		return {"found": true, "value": _normalize_value(defaults[property_name], kind)}
	return {"found": false, "value": null}


func _normalize_value(value, kind: String):
	match kind:
		"bool":
			return bool(value)
		"int":
			return int(value)
		_:
			return String(value if value != null else "").strip_edges()


func _read_visuals() -> Dictionary:
	var result := {}
	var directory := DirAccess.open(ITEM_RESOURCE_DIR)
	if directory == null:
		_fail("Missing visual resource directory %s" % ITEM_RESOURCE_DIR)
		return result
	directory.list_dir_begin()
	while true:
		var file_name := directory.get_next()
		if file_name.is_empty():
			break
		if directory.current_is_dir() or not file_name.ends_with(".tres"):
			continue
		var path := "%s/%s" % [ITEM_RESOURCE_DIR, file_name]
		var parsed := _parse_tres(path)
		if parsed.is_empty():
			continue
		_check_visual_fields(path, parsed)
		var id := String(parsed.get("Id", "")).strip_edges()
		if id.is_empty():
			_fail("Visual resource %s is missing Id" % path)
			continue
		if result.has(id):
			_fail("Visual resources contain duplicate item ID %s" % id)
			continue
		parsed["__path"] = path
		result[id] = parsed
	directory.list_dir_end()
	return result


func _parse_tres(path: String) -> Dictionary:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_fail("Unable to read visual resource %s" % path)
		return {}
	var result := {}
	for line in file.get_as_text().split("\n"):
		var match := RegEx.create_from_string("^([A-Za-z0-9_]+)\\s*=\\s*(.*)$").search(line.strip_edges())
		if match == null:
			continue
		var key := match.get_string(1)
		var raw := match.get_string(2).strip_edges()
		result[key] = _parse_tres_value(raw)
	return result


func _parse_tres_value(raw: String):
	if raw == "true":
		return true
	if raw == "false":
		return false
	if raw.is_valid_int():
		return raw.to_int()
	if raw.begins_with("\"") and raw.ends_with("\""):
		var parsed = JSON.parse_string(raw)
		return String(parsed) if parsed != null else raw.trim_prefix("\"").trim_suffix("\"")
	return raw


func _check_visual_fields(path: String, visual: Dictionary) -> void:
	var forbidden_fields: Array[String] = []
	for field in GAMEPLAY_FIELDS:
		var resource_field: String = field.resource
		if resource_field != "Id":
			forbidden_fields.append(resource_field)
	for field in EXTRA_FORBIDDEN_VISUAL_FIELDS:
		forbidden_fields.append(field)
	for field in forbidden_fields:
		if visual.has(field):
			_fail("Visual resource %s contains forbidden gameplay field %s" % [path, field])
	for field in visual.keys():
		if String(field).begins_with("__") or field == "script":
			continue
		if not VISUAL_FIELDS.has(field):
			_fail("Visual resource %s contains unsupported field %s" % [path, field])


func _compare_visual_ids(pandora_by_id: Dictionary, visuals_by_id: Dictionary) -> void:
	for id in pandora_by_id.keys():
		if not visuals_by_id.has(id):
			_fail("Visual resources are missing Pandora item %s" % id)
	for id in visuals_by_id.keys():
		if not pandora_by_id.has(id):
			_fail("Visual resources contain item not present in Pandora: %s" % id)


func _check_contract_sources(pandora_by_id: Dictionary) -> void:
	var constants := _read_string_constants(SPECIAL_CARD_SYSTEM_PATH, "CardEffectTypes")
	var presentation_constants := _read_string_constants(SPECIAL_CARD_SYSTEM_PATH, "CardPlayPresentationTypes")
	var registry_text := _read_text(REGISTRY_PATH)
	var resolver_text := _read_text(RESOLVER_PATH)
	var presentation_text := _read_text(PRESENTATION_PATH)
	var registry_effects := {}
	var required_resolver_effects := {}
	for match in _all_matches(registry_text, "Add\\(contracts,\\s*CardEffectTypes\\.([A-Za-z0-9_]+)[^\\n]*"):
		var name: String = match.get_string(1)
		if constants.has(name):
			registry_effects[constants[name]] = true
			if match.get_string(0).contains("requiresResolver: true"):
				required_resolver_effects[constants[name]] = true
	var actual_resolver_effects := {}
	for match in _all_matches(resolver_text, "\\[CardEffectTypes\\.([A-Za-z0-9_]+)\\]\\s*="):
		var name: String = match.get_string(1)
		if constants.has(name):
			actual_resolver_effects[constants[name]] = true
	_compare_subset("effect registry", _effects_in_manifest(pandora_by_id), registry_effects)
	_compare_set("effect resolver", required_resolver_effects, actual_resolver_effects)

	var known_presentations := {}
	for match in _all_matches(registry_text, "CardPlayPresentationTypes\\.([A-Za-z0-9_]+)"):
		var name: String = match.get_string(1)
		if presentation_constants.has(name):
			known_presentations[presentation_constants[name]] = true
	var actual_presentations := {}
	for match in _all_matches(presentation_text, "CardPlayPresentationTypes\\.([A-Za-z0-9_]+)\\]"):
		var name: String = match.get_string(1)
		if presentation_constants.has(name):
			actual_presentations[presentation_constants[name]] = true
	actual_presentations["direct_trigger"] = true
	_compare_set("presentation handler", known_presentations, _presentations_in_manifest(pandora_by_id))
	_compare_set("presentation implementation", known_presentations, actual_presentations)


func _check_manifest_value_shapes(pandora_by_id: Dictionary) -> void:
	for id in pandora_by_id.keys():
		var values: Dictionary = pandora_by_id[id]
		var reward_delivery := String(values.get("reward_delivery", ""))
		if reward_delivery not in ["none", "hand", "immediate_attachment"]:
			_fail("Item %s has invalid reward_delivery %s" % [id, reward_delivery])
		if int(values.get("weight", -1)) < 0:
			_fail("Item %s has negative weight" % id)
		if String(values.get("trigger", "")).is_empty():
			_fail("Item %s has an empty trigger" % id)
		if String(values.get("effect_type", "")).is_empty():
			_fail("Item %s has an empty effect_type" % id)
		if not _has_valid_effect_values(String(values.get("effect_values", ""))):
			_fail("Item %s has malformed effect_values" % id)
		if not _has_valid_effect_values(String(values.get("attached_effect_values", ""))):
			_fail("Item %s has malformed attached_effect_values" % id)
		var attached_trigger := String(values.get("attached_trigger", ""))
		var attached_effect := String(values.get("attached_effect_type", ""))
		if attached_trigger.is_empty() != attached_effect.is_empty():
			_fail("Item %s has an incomplete attached effect contract" % id)
		if String(values.get("description_key", "")).is_empty():
			_fail("Item %s has an empty description_key" % id)


func _has_valid_effect_values(raw: String) -> bool:
	if raw.is_empty():
		return true
	var seen := {}
	for segment in raw.split(";"):
		var separator := segment.find("=")
		if separator <= 0 or separator == segment.length() - 1 or segment.find("=", separator + 1) >= 0:
			return false
		var key := segment.substr(0, separator).strip_edges()
		var value := segment.substr(separator + 1).strip_edges()
		if key.is_empty() or value.is_empty() or seen.has(key):
			return false
		seen[key] = true
	return true


func _check_item_localization(pandora_by_id: Dictionary) -> void:
	var file := FileAccess.open(LOCALIZATION_PATH, FileAccess.READ)
	if file == null:
		_fail("Unable to read localization source %s" % LOCALIZATION_PATH)
		return
	var expected := {}
	for values in pandora_by_id.values():
		expected[String(values.get("description_key", ""))] = true
	var seen := {}
	for line in file.get_as_text().split("\n"):
		var columns := _parse_csv_line(line)
		if columns.is_empty() or not expected.has(columns[0]):
			continue
		seen[columns[0]] = true
		for column_index in range(1, columns.size()):
			if columns[column_index].strip_edges().is_empty():
				_fail("Localization %s has an empty column %d" % [columns[0], column_index])
	for key in expected.keys():
		if not seen.has(key):
			_fail("Localization is missing item description %s" % key)


func _effects_in_manifest(pandora_by_id: Dictionary) -> Dictionary:
	var result := {}
	for values in pandora_by_id.values():
		var effect_type := String(values.get("effect_type", ""))
		if not effect_type.is_empty():
			result[effect_type] = true
		var attached := String(values.get("attached_effect_type", ""))
		if not attached.is_empty():
			result[attached] = true
	return result


func _presentations_in_manifest(pandora_by_id: Dictionary) -> Dictionary:
	var result := {}
	for values in pandora_by_id.values():
		result[String(values.get("play_presentation", ""))] = true
	return result


func _compare_set(label: String, expected: Dictionary, actual: Dictionary) -> void:
	for value in expected.keys():
		if not actual.has(value):
			_fail("%s is missing %s" % [label, value])
	for value in actual.keys():
		if not expected.has(value):
			_fail("%s has unexpected %s" % [label, value])


func _compare_subset(label: String, actual: Dictionary, allowed: Dictionary) -> void:
	for value in actual.keys():
		if not allowed.has(value):
			_fail("%s does not register %s" % [label, value])


func _check_localization_placeholders(pandora_root: Dictionary) -> void:
	var file := FileAccess.open(LOCALIZATION_PATH, FileAccess.READ)
	if file == null:
		_fail("Unable to read localization source %s" % LOCALIZATION_PATH)
		return
	var expected_by_key := {}
	var defaults := _get_category_defaults(pandora_root, ADVENTURE_CATEGORY)
	for entity in _get_category_entities(pandora_root, ADVENTURE_CATEGORY):
		var id := String(entity.get("_id", "")).strip_edges()
		var description_key_result := _get_pandora_property(
			entity,
			"description_key",
			defaults,
			"string")
		var description_key := String(description_key_result.value if description_key_result.found else "").strip_edges()
		if description_key.is_empty():
			description_key = "UPGRADE_DESC_%s" % id.to_upper()
		var value_keys_result := _get_pandora_property(
			entity,
			"description_value_keys",
			defaults,
			"string")
		expected_by_key[description_key] = _parse_value_key_list(
			String(value_keys_result.value if value_keys_result.found else ""))
	var seen := {}
	for line in file.get_as_text().split("\n"):
		var columns := _parse_csv_line(line)
		if columns.is_empty():
			continue
		var key := columns[0]
		if not expected_by_key.has(key):
			continue
		seen[key] = true
		var baseline_tokens := _placeholder_tokens(columns[1])
		_validate_expected_placeholder_tokens(key, baseline_tokens, expected_by_key[key])
		for index in range(2, columns.size()):
			var tokens := _placeholder_tokens(columns[index])
			if not _placeholder_sets_equal(tokens, baseline_tokens):
				_fail("Localization placeholder mismatch for %s column %d" % [key, index])
	for key in expected_by_key.keys():
		if not seen.has(key):
			_fail("Localization is missing %s" % key)


func _parse_value_key_list(raw_value: String) -> Array[String]:
	var result: Array[String] = []
	for raw_key in raw_value.split(","):
		var key := String(raw_key).strip_edges()
		if not key.is_empty():
			result.append(key)
	return result


func _validate_expected_placeholder_tokens(
	localization_key: String,
	tokens: Array[String],
	expected_keys: Array[String]) -> void:
	var expected_set := {}
	for expected_key in expected_keys:
		expected_set[expected_key] = true
	for token in tokens:
		if not expected_set.has(token):
			_fail("Localization has unexpected placeholder {%s} for %s" % [token, localization_key])
	var seen_keys := {}
	for token in tokens:
		seen_keys[token] = true
	for expected_key in expected_keys:
		if not seen_keys.has(expected_key):
			_fail("Localization is missing placeholder {%s} for %s" % [expected_key, localization_key])
	var unique_tokens: Array[String] = []
	for token in tokens:
		if not unique_tokens.has(token):
			unique_tokens.append(token)
	if unique_tokens != expected_keys:
		_fail("Localization placeholder order mismatch for %s" % localization_key)


func _placeholder_tokens(value: String) -> Array[String]:
	var result: Array[String] = []
	var regex := RegEx.create_from_string("\\{([A-Za-z0-9_]+)\\}")
	for match in regex.search_all(value):
		result.append(match.get_string(1))
	return result


func _placeholder_sets_equal(left: Array[String], right: Array[String]) -> bool:
	var left_set := {}
	var right_set := {}
	for token in left:
		left_set[token] = true
	for token in right:
		right_set[token] = true
	return left_set == right_set


func _parse_csv_line(line: String) -> Array[String]:
	var columns: Array[String] = []
	var current := ""
	var quoted := false
	var index := 0
	while index < line.length():
		var character := line[index]
		if character == '"':
			if quoted and index + 1 < line.length() and line[index + 1] == '"':
				current += '"'
				index += 1
			else:
				quoted = not quoted
		elif character == ',' and not quoted:
			columns.append(current)
			current = ""
		else:
			current += character
		index += 1
	columns.append(current)
	return columns


func _read_string_constants(path: String, _type_name: String) -> Dictionary:
	var text := _read_text(path)
	var result := {}
	for match in _all_matches(text, "public\\s+const\\s+string\\s+([A-Za-z0-9_]+)\\s*=\\s*\"([^\"]+)\""):
		result[match.get_string(1)] = match.get_string(2)
	return result


func _read_text(path: String) -> String:
	var file := FileAccess.open(path, FileAccess.READ)
	return file.get_as_text() if file != null else ""


func _all_matches(text: String, pattern: String) -> Array[RegExMatch]:
	var regex := RegEx.create_from_string(pattern)
	return regex.search_all(text)


func _canonical_value(value) -> String:
	if value is bool:
		return "true" if value else "false"
	return str(value)


func _sha256(value: String) -> String:
	var context := HashingContext.new()
	context.start(HashingContext.HASH_SHA256)
	context.update(value.to_utf8_buffer())
	return context.finish().hex_encode()


func _fail(message: String) -> void:
	_failed = true
	printerr("ITEM_CARD_MANIFEST_ERROR %s" % message)


func _quit_failure() -> void:
	quit(1)
