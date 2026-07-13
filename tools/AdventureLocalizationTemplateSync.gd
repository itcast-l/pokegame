extends SceneTree

const PANDORA_PATH := "res://data.pandora"
const LOCALIZATION_PATH := "res://localization/ui.csv"
const ADVENTURE_CATEGORY := "adventure_suit_upgrades"
const QUESTION_MARK_INTEREST_KEY := "UPGRADE_DESC_DIAMONDS_QUESTION_MARK_INTEREST"

var _failed := false


func _initialize() -> void:
	if not OS.get_cmdline_args().has("--write"):
		printerr("Usage: godot --headless --path . --script res://tools/AdventureLocalizationTemplateSync.gd --write")
		quit(1)
		return
	call_deferred("_run")


func _run() -> void:
	var pandora_root := _load_json(PANDORA_PATH)
	if pandora_root.is_empty():
		_quit_failure()
		return

	var defaults := _get_category_defaults(pandora_root, ADVENTURE_CATEGORY)
	var expected_by_key := {}
	for entity in _get_category_entities(pandora_root, ADVENTURE_CATEGORY):
		var id := String(entity.get("_id", "")).strip_edges()
		var description_key_result := _get_pandora_property(entity, "description_key", defaults)
		var description_key := String(description_key_result.value if description_key_result.found else "").strip_edges()
		if description_key.is_empty():
			description_key = "UPGRADE_DESC_%s" % id.to_upper()
		var value_keys_result := _get_pandora_property(entity, "description_value_keys", defaults)
		expected_by_key[description_key] = _parse_value_key_list(
			String(value_keys_result.value if value_keys_result.found else ""))

	var file := FileAccess.open(LOCALIZATION_PATH, FileAccess.READ)
	if file == null:
		_fail("Unable to read %s" % LOCALIZATION_PATH)
		_quit_failure()
		return
	var source := file.get_as_text()
	var lines := source.split("\n")
	var output_lines: Array[String] = []
	for line in lines:
		var cells := _parse_csv_line(line)
		if cells.is_empty() or not expected_by_key.has(String(cells[0].value)):
			output_lines.append(line)
			continue

		var localization_key := String(cells[0].value)
		var value_keys: Array[String] = expected_by_key[localization_key]
		if localization_key == QUESTION_MARK_INTEREST_KEY:
			for index in range(1, cells.size()):
				cells[index].value = _ensure_interest_placeholder(String(cells[index].value))
		else:
			if value_keys.is_empty():
				output_lines.append(_serialize_csv_line(cells))
				continue
			for index in range(1, cells.size()):
				var cell_value := String(cells[index].value)
				var display_plan := _get_display_plan(localization_key, value_keys, cell_value)
				cells[index].value = _replace_numeric_tokens(
					cell_value,
					value_keys,
					display_plan)

		output_lines.append(_serialize_csv_line(cells))

	var write_file := FileAccess.open(LOCALIZATION_PATH, FileAccess.WRITE)
	if write_file == null:
		_fail("Unable to write %s" % LOCALIZATION_PATH)
	else:
		write_file.store_string("\n".join(output_lines))
	if _failed:
		_quit_failure()
	else:
		print("ADVENTURE_LOCALIZATION_TEMPLATE_SYNC_OK")
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
	for entity in root.get("_entity_data", {}).get("_entities", []):
		if entity is Dictionary and String(entity.get("_category_id", "")) == category_id:
			result.append(entity)
	return result


func _get_category_defaults(root: Dictionary, category_id: String) -> Dictionary:
	var defaults := {}
	for property in root.get("_entity_data", {}).get("_properties", []):
		if property is Dictionary and String(property.get("_category_id", "")) == category_id:
			defaults[String(property.get("_name", ""))] = property.get("_default_value", null)
	return defaults


func _get_pandora_property(entity: Dictionary, property_name: String, defaults: Dictionary) -> Dictionary:
	var overrides: Dictionary = entity.get("_property_overrides", {})
	if overrides.has(property_name):
		return {"found": true, "value": overrides[property_name].get("value", "")}
	if defaults.has(property_name):
		return {"found": true, "value": defaults[property_name]}
	return {"found": false, "value": ""}


func _parse_value_key_list(raw_value: String) -> Array[String]:
	var result: Array[String] = []
	for raw_key in raw_value.split(","):
		var key := String(raw_key).strip_edges()
		if not key.is_empty():
			result.append(key)
	return result


func _parse_csv_line(line: String) -> Array:
	if line.is_empty():
		return []
	var cells: Array = []
	var value := ""
	var quoted := false
	var originally_quoted := false
	var index := 0
	while index < line.length():
		var character := line[index]
		if character == '"' and quoted and index + 1 < line.length() and line[index + 1] == '"':
			value += '"'
			index += 1
		elif character == '"' and !quoted and value.is_empty():
			quoted = true
			originally_quoted = true
		elif character == '"' and quoted:
			quoted = false
		elif character == ',' and !quoted:
			cells.append({"value": value, "quoted": originally_quoted})
			value = ""
			originally_quoted = false
		else:
			value += character
		index += 1
	cells.append({"value": value, "quoted": originally_quoted})
	return cells


func _serialize_csv_line(cells: Array) -> String:
	var encoded: Array[String] = []
	for cell in cells:
		var value := String(cell.value)
		var should_quote := bool(cell.quoted) or value.contains(",") or value.contains('"')
		if should_quote:
			encoded.append('"' + value.replace('"', '""') + '"')
		else:
			encoded.append(value)
	return ",".join(encoded)


func _get_display_plan(
	localization_key: String,
	value_keys: Array[String],
	value: String) -> Array:
	var numeric_matches := RegEx.create_from_string("\\d+").search_all(value)
	var plan: Array = value_keys.duplicate()
	match localization_key:
		"UPGRADE_DESC_HEARTS_THICK_BLOOD":
			plan = ["amount", "amount"]
		"UPGRADE_DESC_HEARTS_CHAIN_TIDE_BREATH":
			plan = ["count", "amount"]
			if numeric_matches.size() >= 3:
				plan = ["", "count", "amount"]
		"UPGRADE_DESC_HEARTS_RED_TIDE_RETURN":
			plan = ["threshold", "amount", "count"]
			if numeric_matches.size() >= 4:
				plan = ["threshold", "", "amount", "count"]
		"UPGRADE_DESC_HEARTS_RETURN_TIDE_HEART":
			plan = ["first_count", "first_amount", "second_count", "second_amount"]
			if numeric_matches.size() >= 5:
				plan = ["", "first_count", "first_amount", "second_count", "second_amount"]
		"UPGRADE_DESC_HEARTS_REVIVAL_DRAW":
			plan = ["threshold", "amount", "count"]
			if numeric_matches.size() >= 4:
				var first_value := numeric_matches[0].get_string()
				var second_value := numeric_matches[1].get_string()
				plan = ["", "threshold", "amount", "count"] if first_value == "1" else ["threshold", "", "amount", "count"]
		"UPGRADE_DESC_DIAMONDS_SMALL_INSURANCE":
			plan = ["threshold", "amount"]
			if numeric_matches.size() >= 3:
				plan = ["", "threshold", "amount"]
		"UPGRADE_DESC_DIAMONDS_QUESTION_MARK_WINDFALL":
			plan = ["maximum", "threshold"]
		"UPGRADE_DESC_CLUBS_CONTINUE_BRANCH":
			plan = ["threshold", "count"]
	return plan


func _replace_numeric_tokens(
	value: String,
	value_keys: Array[String],
	display_plan: Array) -> String:
	if value_keys.is_empty():
		return value
	var regex := RegEx.create_from_string("\\d+")
	var matches := regex.search_all(value)
	var result := ""
	var cursor := 0
	for index in range(matches.size()):
		var match: RegExMatch = matches[index]
		result += value.substr(cursor, match.get_start() - cursor)
		if index < display_plan.size() and not String(display_plan[index]).is_empty():
			result += "{%s}" % display_plan[index]
		else:
			result += match.get_string()
		cursor = match.get_end()
	result += value.substr(cursor)
	var existing_tokens := _placeholder_tokens(result)
	for key in value_keys:
		if not existing_tokens.has(key):
			result += " ({%s})" % key
			existing_tokens.append(key)
	return result


func _ensure_interest_placeholder(value: String) -> String:
	if value.contains("{current_item_count}"):
		return value
	return value + " ({current_item_count})"


func _placeholder_tokens(value: String) -> Array[String]:
	var result: Array[String] = []
	var regex := RegEx.create_from_string("\\{([A-Za-z0-9_]+)\\}")
	for match in regex.search_all(value):
		result.append(match.get_string(1))
	return result


func _fail(message: String) -> void:
	_failed = true
	printerr("ADVENTURE_LOCALIZATION_TEMPLATE_ERROR %s" % message)


func _quit_failure() -> void:
	quit(1)
