@tool
extends VBoxContainer

const CATEGORY_ID := "adventure_suit_upgrades"
const REQUIRED_PROPERTIES := {
	"suit": "string",
	"rarity": "string",
	"trigger_text": "string",
	"effect_text": "string",
	"notes": "string",
	"enabled": "bool",
	"sort_order": "int",
}
const HELP_TEXT := (
	"[b]Adventure Suit Upgrades[/b]\n"
	+ "Management-only config. This dock edits Pandora data and does not wire the upgrades into gameplay yet.\n\n"
	+ "[b]Workbook metadata[/b]\n"
	+ "- Scope: Adventure mode only\n"
	+ "- Status: Design draft\n"
	+ "- Rule Note: Max HP abilities require shared Current HP / Max HP across the run.\n"
	+ "- Imported: Hearts 14, Diamonds 14, Spades 15, Clubs 14."
)

var _help_label: RichTextLabel
var _status_label: Label
var _save_button: Button
var _reload_button: Button
var _open_pandora_button: Button
var _repair_schema_button: Button
var _rows_container: VBoxContainer
var _row_states: Array[Dictionary] = []


func _ready() -> void:
	_help_label = get_node("HelpLabel") as RichTextLabel
	_status_label = get_node("StatusLabel") as Label
	_save_button = get_node("ActionBar/SaveButton") as Button
	_reload_button = get_node("ActionBar/ReloadButton") as Button
	_open_pandora_button = get_node("ActionBar/OpenPandoraButton") as Button
	_repair_schema_button = get_node("ActionBar/RepairSchemaButton") as Button
	_rows_container = get_node("RowsScroll/RowsContainer") as VBoxContainer

	_help_label.text = HELP_TEXT
	_save_button.pressed.connect(_on_save_pressed)
	_reload_button.pressed.connect(refresh)
	_open_pandora_button.pressed.connect(_on_open_pandora_pressed)
	_repair_schema_button.pressed.connect(_on_repair_schema_pressed)

	refresh()


func refresh() -> void:
	_clear_rows()
	_row_states.clear()

	var pandora = _get_pandora()
	if pandora == null:
		_set_status("Pandora autoload is unavailable. Enable Pandora before editing adventure suit upgrades.", true)
		_set_action_state(false, false, false, false)
		return

	if not pandora.is_loaded():
		_set_status("Pandora data is still loading. Try Reload in a moment.", true)
		_set_action_state(false, true, true, false)
		return

	var category = _get_category()
	if category == null:
		_set_status("Missing Pandora category adventure_suit_upgrades. Seed data has not been added yet.", true)
		_set_action_state(false, true, true, false)
		return

	var missing_properties := _get_missing_schema_properties(category)
	if not missing_properties.is_empty():
		_set_status(
			"Schema is incomplete: %s. Click Repair Schema to add the missing properties."
			% ", ".join(missing_properties),
			true
		)
		_set_action_state(false, true, true, true)
		return

	var entities = pandora.get_all_entities(category)
	entities.sort_custom(func(a, b): return _sort_entities(a, b))
	for entity in entities:
		if entity == null:
			continue
		_row_states.append(_build_row(entity))

	_set_status(
		"Loaded %d adventure suit upgrades from Pandora. Management only; gameplay remains unchanged."
		% _row_states.size(),
		false
	)
	_set_action_state(not _row_states.is_empty(), true, true, false)


func _get_pandora() -> Node:
	return get_node_or_null("/root/Pandora")


func _get_category():
	var pandora = _get_pandora()
	if pandora == null or not pandora.is_loaded():
		return null
	return pandora.get_category(CATEGORY_ID)


func _get_missing_schema_properties(category) -> PackedStringArray:
	var missing := PackedStringArray()
	for property_name in REQUIRED_PROPERTIES.keys():
		if not category.has_entity_property(property_name):
			missing.append(property_name)
	return missing


func _sort_entities(a, b) -> bool:
	var a_order: int = a.get_integer("sort_order")
	var b_order: int = b.get_integer("sort_order")
	if a_order == b_order:
		return String(a.get_entity_id()) < String(b.get_entity_id())
	return a_order < b_order


func _build_row(entity) -> Dictionary:
	var panel := PanelContainer.new()
	panel.name = String(entity.get_entity_id())
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_theme_constant_override("margin_left", 8)
	panel.add_theme_constant_override("margin_top", 8)
	panel.add_theme_constant_override("margin_right", 8)
	panel.add_theme_constant_override("margin_bottom", 8)
	_rows_container.add_child(panel)

	var body := VBoxContainer.new()
	body.add_theme_constant_override("separation", 8)
	panel.add_child(body)

	var title := Label.new()
	title.text = entity.get_entity_id()
	title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	body.add_child(title)

	var grid := GridContainer.new()
	grid.columns = 2
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation", 8)
	grid.add_theme_constant_override("v_separation", 6)
	body.add_child(grid)

	var id_edit := _create_line_edit(entity.get_entity_id(), false)
	var suit_edit := _create_line_edit(entity.get_string("suit"), false)
	var name_edit := _create_line_edit(entity.get_entity_name(), true)
	var rarity_edit := _create_line_edit(entity.get_string("rarity"), true)
	var trigger_edit := _create_text_edit(entity.get_string("trigger_text"))
	var effect_edit := _create_text_edit(entity.get_string("effect_text"))
	var notes_edit := _create_text_edit(entity.get_string("notes"))
	var enabled_check := CheckBox.new()
	enabled_check.button_pressed = entity.get_bool("enabled")
	var sort_order_spin := SpinBox.new()
	sort_order_spin.min_value = -999999
	sort_order_spin.max_value = 999999
	sort_order_spin.step = 1
	sort_order_spin.rounded = true
	sort_order_spin.value = entity.get_integer("sort_order")
	sort_order_spin.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	_add_field(grid, "ID", id_edit)
	_add_field(grid, "Suit", suit_edit)
	_add_field(grid, "Name", name_edit)
	_add_field(grid, "Rarity", rarity_edit)
	_add_field(grid, "Trigger", trigger_edit)
	_add_field(grid, "Effect", effect_edit)
	_add_field(grid, "Notes", notes_edit)
	_add_field(grid, "Enabled", enabled_check)
	_add_field(grid, "Sort Order", sort_order_spin)

	return {
		"entity": entity,
		"id_edit": id_edit,
		"suit_edit": suit_edit,
		"name_edit": name_edit,
		"rarity_edit": rarity_edit,
		"trigger_edit": trigger_edit,
		"effect_edit": effect_edit,
		"notes_edit": notes_edit,
		"enabled_check": enabled_check,
		"sort_order_spin": sort_order_spin,
	}


func _add_field(container: GridContainer, label_text: String, control: Control) -> void:
	var label := Label.new()
	label.text = label_text
	label.vertical_alignment = VERTICAL_ALIGNMENT_TOP
	container.add_child(label)
	container.add_child(control)


func _create_line_edit(value: String, editable: bool) -> LineEdit:
	var edit := LineEdit.new()
	edit.text = value
	edit.editable = editable
	edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return edit


func _create_text_edit(value: String) -> TextEdit:
	var edit := TextEdit.new()
	edit.text = value
	edit.custom_minimum_size = Vector2(0, 72)
	edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return edit


func _clear_rows() -> void:
	for child in _rows_container.get_children():
		child.queue_free()


func _set_action_state(can_save: bool, can_reload: bool, can_open_pandora: bool, can_repair: bool) -> void:
	_save_button.disabled = not can_save
	_reload_button.disabled = not can_reload
	_open_pandora_button.disabled = not can_open_pandora
	_repair_schema_button.disabled = not can_repair


func _set_status(message: String, is_error: bool) -> void:
	_status_label.text = message
	_status_label.modulate = Color(1.0, 0.45, 0.45) if is_error else Color(0.85, 0.95, 1.0)


func _on_save_pressed() -> void:
	var pandora = _get_pandora()
	if pandora == null or not pandora.is_loaded():
		_set_status("Pandora is unavailable, so the dock cannot save right now.", true)
		return

	for row_state in _row_states:
		var validation_error := _validate_row_state(row_state)
		if not validation_error.is_empty():
			_set_status(validation_error, true)
			return

	for row_state in _row_states:
		var entity = row_state["entity"]
		var suit_edit := row_state["suit_edit"] as LineEdit
		var name_edit := row_state["name_edit"] as LineEdit
		var rarity_edit := row_state["rarity_edit"] as LineEdit
		var trigger_edit := row_state["trigger_edit"] as TextEdit
		var effect_edit := row_state["effect_edit"] as TextEdit
		var notes_edit := row_state["notes_edit"] as TextEdit
		var enabled_check := row_state["enabled_check"] as CheckBox
		var sort_order_spin := row_state["sort_order_spin"] as SpinBox

		entity.set_entity_name(name_edit.text.strip_edges())
		entity.get_entity_property("suit").set_default_value(suit_edit.text.strip_edges())
		entity.get_entity_property("rarity").set_default_value(rarity_edit.text.strip_edges())
		entity.get_entity_property("trigger_text").set_default_value(trigger_edit.text.strip_edges())
		entity.get_entity_property("effect_text").set_default_value(effect_edit.text.strip_edges())
		entity.get_entity_property("notes").set_default_value(notes_edit.text.strip_edges())
		entity.get_entity_property("enabled").set_default_value(enabled_check.button_pressed)
		entity.get_entity_property("sort_order").set_default_value(int(sort_order_spin.value))

	pandora.save_data()
	_set_status("Saved adventure suit upgrades to Pandora.", false)
	refresh()


func _validate_row_state(row_state: Dictionary) -> String:
	var entity = row_state["entity"]
	var id_edit := row_state["id_edit"] as LineEdit
	var suit_edit := row_state["suit_edit"] as LineEdit
	var name_edit := row_state["name_edit"] as LineEdit
	var rarity_edit := row_state["rarity_edit"] as LineEdit
	var sort_order_spin := row_state["sort_order_spin"] as SpinBox

	if name_edit.text.strip_edges().is_empty():
		return "Name cannot be blank for %s." % id_edit.text
	if rarity_edit.text.strip_edges().is_empty():
		return "Rarity cannot be blank for %s." % id_edit.text
	if suit_edit.text.strip_edges().is_empty():
		return "Suit cannot be blank for %s." % id_edit.text
	if int(sort_order_spin.value) != sort_order_spin.value:
		return "Sort order must be an integer for %s." % id_edit.text
	if entity == null:
		return "Encountered an invalid row while saving."
	return ""


func _on_repair_schema_pressed() -> void:
	var pandora = _get_pandora()
	var category = _get_category()
	if pandora == null or category == null:
		_set_status("Cannot repair schema because Pandora or the category is unavailable.", true)
		return

	var repaired_any := false
	for property_name in REQUIRED_PROPERTIES.keys():
		if category.has_entity_property(property_name):
			continue
		pandora.create_property(category, property_name, REQUIRED_PROPERTIES[property_name])
		repaired_any = true

	if repaired_any:
		pandora.save_data()
		_set_status("Repaired missing Pandora schema properties for adventure suit upgrades.", false)
	else:
		_set_status("Schema is already complete.", false)

	refresh()


func _on_open_pandora_pressed() -> void:
	if Engine.has_meta("PandoraEditorPlugin"):
		EditorInterface.set_main_screen_editor("Pandora")
		_set_status("Focused the Pandora editor.", false)
		return

	_set_status("Pandora main screen is unavailable. Open the Pandora tab manually.", true)
