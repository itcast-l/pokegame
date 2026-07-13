extends SceneTree

const DOCK_SCENE := preload("res://addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.tscn")
const DATA_PATH := "res://data.pandora"
const CATEGORY_ID := "adventure_suit_upgrades"
const EXPECTED_ROW_COUNT := 53
const STATUS_PATH := "StatusLabel"
const ROWS_PATH := "RowsScroll/RowsContainer"
const SMOKE_SUFFIX := " [smoke]"

var _failed := false
var _original_data_text := ""


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var dock := await _create_dock()
	if _failed:
		return

	var rows_container := dock.get_node(ROWS_PATH) as VBoxContainer
	_assert(rows_container.get_child_count() == EXPECTED_ROW_COUNT, "Expected %d dock rows, got %d" % [EXPECTED_ROW_COUNT, rows_container.get_child_count()])
	_assert(
		(dock.get_node(STATUS_PATH) as Label).text.contains("Loaded %d" % EXPECTED_ROW_COUNT),
		"Dock status did not report %d loaded upgrades" % EXPECTED_ROW_COUNT
	)
	if _failed:
		return

	var row_states: Array = dock.get("_row_states")
	_assert(not row_states.is_empty(), "Dock did not cache any row state")
	if _failed:
		return

	var first_row: Dictionary = row_states[0]
	var name_edit := first_row["name_edit"] as LineEdit
	var notes_edit := first_row["notes_edit"] as TextEdit
	var original_name := name_edit.text
	var original_notes := notes_edit.text

	name_edit.text = ""
	dock.call("_on_save_pressed")
	await process_frame
	_assert(
		(dock.get_node(STATUS_PATH) as Label).text.contains("Name cannot be blank"),
		"Dock did not block save when name was blank"
	)
	if _failed:
		return
	name_edit.text = original_name

	var smoke_notes := original_notes + SMOKE_SUFFIX if not original_notes.ends_with(SMOKE_SUFFIX) else original_notes
	notes_edit.text = smoke_notes
	dock.call("_on_save_pressed")
	await process_frame

	var reloaded_dock := await _create_dock()
	if _failed:
		return

	var reloaded_first_row: Dictionary = (reloaded_dock.get("_row_states") as Array)[0]
	var reloaded_notes_edit := reloaded_first_row["notes_edit"] as TextEdit
	_assert(reloaded_notes_edit.text == smoke_notes, "Saved notes change did not persist after reload")
	if _failed:
		return

	reloaded_notes_edit.text = original_notes
	reloaded_dock.call("_on_save_pressed")
	await process_frame

	var restored_dock := await _create_dock()
	if _failed:
		return

	var restored_first_row: Dictionary = (restored_dock.get("_row_states") as Array)[0]
	var restored_notes_edit := restored_first_row["notes_edit"] as TextEdit
	_assert(restored_notes_edit.text == original_notes, "Original notes were not restored after smoke save")
	if _failed:
		return

	_original_data_text = FileAccess.get_file_as_string(DATA_PATH)
	var pandora = root.get_node("/root/Pandora")
	var category = pandora.get_category(CATEGORY_ID)
	var notes_property = category.get_entity_property("notes")
	pandora._entity_backend.delete_property(notes_property)

	var broken_dock := await _create_dock()
	if _failed:
		return
	_assert(
		(broken_dock.get_node(STATUS_PATH) as Label).text.contains("Schema is incomplete"),
		"Dock did not show schema warning when a property was removed"
	)
	if _failed:
		return

	broken_dock.call("_on_repair_schema_pressed")
	await process_frame
	_assert(category.has_entity_property("notes"), "Repair Schema did not restore the missing notes property")
	if _failed:
		return

	if not _restore_original_data():
		_fail("Failed to restore original Pandora data after schema repair smoke")
		return

	print("ADVENTURE_SUIT_UPGRADE_DOCK_SMOKE_OK")
	quit(0)


func _create_dock() -> Control:
	var dock := DOCK_SCENE.instantiate()
	if dock == null:
		_fail("Failed to instantiate adventure suit upgrades dock scene")
		return null

	root.add_child(dock)
	await process_frame
	await process_frame
	return dock


func _assert(condition: bool, message: String) -> void:
	if condition or _failed:
		return

	_fail(message)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	_restore_original_data()
	push_error(message)
	print("ADVENTURE_SUIT_UPGRADE_DOCK_SMOKE_FAIL %s" % message)
	quit(1)


func _restore_original_data() -> bool:
	if _original_data_text.is_empty():
		return true

	var restored_file := FileAccess.open(DATA_PATH, FileAccess.WRITE)
	if restored_file == null:
		return false
	restored_file.store_string(_original_data_text)
	restored_file.close()
	_original_data_text = ""
	return true
