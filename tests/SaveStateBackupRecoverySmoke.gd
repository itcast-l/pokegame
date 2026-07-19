extends SceneTree

const TEST_SAVE_ROOT := "user://save_state_backup_recovery_smoke"

var _failed := false
var _load_completed_count := 0
var _load_failed_errors: Array[int] = []
var _backup_recoveries: Array[Dictionary] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return

	save_manager.set("save_root", TEST_SAVE_ROOT)
	save_manager.set("use_json", true)
	save_manager.set("backup_on_commit", true)
	var mkdir_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(TEST_SAVE_ROOT))
	if mkdir_error != OK:
		_fail("Failed to create smoke save root: %d" % mkdir_error)
		return
	save_manager.load_completed.connect(_on_load_completed)
	save_manager.load_failed.connect(_on_load_failed)
	save_manager.backup_recovered.connect(_on_backup_recovered)

	if not _test_valid_primary_wins(save_manager):
		return
	if not _test_corrupt_primary_recovers_and_repairs(save_manager):
		return
	if not _test_backup_only_recovers_when_rolling_backup_disabled(save_manager):
		return
	if not _test_failed_repair_preserves_backup_until_successful_save(save_manager):
		return
	if not _test_both_files_corrupt_fail_once(save_manager):
		return
	if not _test_missing_primary_failure_errors(save_manager):
		return
	if not _test_business_json_damage_does_not_roll_back(save_manager):
		return

	print("SAVE_STATE_BACKUP_RECOVERY_SMOKE_OK")
	quit(0)


func _test_valid_primary_wins(save_manager: Node) -> bool:
	var slot := &"backup_smoke_primary_wins"
	if not _save(save_manager, slot, {"marker": "backup"}):
		return false
	if not _save(save_manager, slot, {"marker": "primary"}):
		return false
	_reset_signal_counts()

	var loaded := save_manager.call("load_from_slot_sync", slot) as Dictionary
	if String(loaded.get("marker", "")) != "primary":
		return _fail("Valid primary did not win over an older backup")
	return _expect_signal_counts(1, 0, 0, "valid primary")


func _test_corrupt_primary_recovers_and_repairs(save_manager: Node) -> bool:
	var slot := &"backup_smoke_corrupt_primary"
	if not _save(save_manager, slot, {"marker": "recovery"}):
		return false
	if not _save(save_manager, slot, {"marker": "newer"}):
		return false
	var path := _slot_path(slot)
	if not _write_corrupt_file(path):
		return false
	_reset_signal_counts()

	var loaded := save_manager.call("load_from_slot_sync", slot) as Dictionary
	if String(loaded.get("marker", "")) != "recovery":
		return _fail("Corrupt primary did not return valid backup data")
	if not _expect_signal_counts(1, 0, 1, "corrupt primary recovery"):
		return false
	if int(_backup_recoveries[0].get("primary_error", OK)) != ERR_FILE_CORRUPT:
		return _fail("Recovery signal did not preserve the primary parse error")
	if int(_backup_recoveries[0].get("repair_error", FAILED)) != OK:
		return _fail("Recovery signal reported a failed primary repair")
	if not FileAccess.file_exists(path) or not FileAccess.file_exists(path + ".bak"):
		return _fail("Successful recovery did not preserve both primary and backup files")
	if _read_all_bytes(path) != _read_all_bytes(path + ".bak"):
		return _fail("Repaired primary bytes do not match the validated backup")
	return true


func _test_backup_only_recovers_when_rolling_backup_disabled(save_manager: Node) -> bool:
	var slot := &"backup_smoke_backup_only"
	if not _save(save_manager, slot, {"marker": "backup_only"}):
		return false
	var path := _slot_path(slot)
	var rename_error := DirAccess.rename_absolute(path, path + ".bak")
	if rename_error != OK:
		return _fail("Failed to create backup-only fixture: %d" % rename_error)
	save_manager.set("backup_on_commit", false)
	_reset_signal_counts()

	var loaded := save_manager.call("load_from_slot_sync", slot) as Dictionary
	save_manager.set("backup_on_commit", true)
	if String(loaded.get("marker", "")) != "backup_only":
		return _fail("Backup-only slot did not recover")
	if not _expect_signal_counts(1, 0, 1, "backup-only recovery"):
		return false
	if not FileAccess.file_exists(path) or not FileAccess.file_exists(path + ".bak"):
		return _fail("Backup-only recovery did not rebuild primary while preserving backup")
	return true


func _test_failed_repair_preserves_backup_until_successful_save(save_manager: Node) -> bool:
	var slot := &"backup_smoke_failed_repair"
	if not _save(save_manager, slot, {"marker": "protected_backup"}):
		return false
	if not _save(save_manager, slot, {"marker": "discarded_primary"}):
		return false
	var path := _slot_path(slot)
	if not _write_corrupt_file(path):
		return false
	var tmp_absolute := ProjectSettings.globalize_path(path + ".tmp")
	var mkdir_error := DirAccess.make_dir_recursive_absolute(tmp_absolute)
	if mkdir_error != OK:
		return _fail("Failed to block primary repair: %d" % mkdir_error)
	var protected_backup_bytes := _read_all_bytes(path + ".bak")
	_reset_signal_counts()

	var loaded := save_manager.call("load_from_slot_sync", slot) as Dictionary
	if String(loaded.get("marker", "")) != "protected_backup":
		return _fail("Failed primary repair did not return validated backup data")
	if not _expect_signal_counts(1, 0, 1, "failed primary repair"):
		return false
	if int(_backup_recoveries[0].get("repair_error", OK)) == OK:
		return _fail("Blocked primary repair unexpectedly succeeded")
	if int(save_manager.call("save_to_slot_sync", slot, {"marker": "failed_save"})) == OK:
		return _fail("Blocked save unexpectedly succeeded")
	if DirAccess.remove_absolute(tmp_absolute) != OK:
		return _fail("Failed to remove blocked repair fixture")

	if not _save(save_manager, slot, {"marker": "first_success"}):
		return false
	if _read_all_bytes(path + ".bak") != protected_backup_bytes:
		return _fail("Successful retry rotated the corrupt primary over the protected backup")
	var first_success_bytes := _read_all_bytes(path)
	if not _save(save_manager, slot, {"marker": "second_success"}):
		return false
	if _read_all_bytes(path + ".bak") != first_success_bytes:
		return _fail("Successful save did not clear the no-rotation recovery marker")
	return true


func _test_both_files_corrupt_fail_once(save_manager: Node) -> bool:
	var slot := &"backup_smoke_both_corrupt"
	save_manager.call("ensure_slot_for_file_base", slot, String(slot))
	var path := _slot_path(slot)
	if not _write_corrupt_file(path) or not _write_corrupt_file(path + ".bak"):
		return false
	_reset_signal_counts()

	var loaded := save_manager.call("load_from_slot_sync", slot) as Dictionary
	if not loaded.is_empty():
		return _fail("Two corrupt files returned non-empty data")
	if not _expect_signal_counts(0, 1, 0, "two corrupt files"):
		return false
	if _load_failed_errors[0] != ERR_FILE_CORRUPT:
		return _fail("Two corrupt files did not report the primary parse error")
	return true


func _test_missing_primary_failure_errors(save_manager: Node) -> bool:
	var corrupt_backup_slot := &"backup_smoke_missing_primary_corrupt_backup"
	save_manager.call("ensure_slot_for_file_base", corrupt_backup_slot, String(corrupt_backup_slot))
	if not _write_corrupt_file(_slot_path(corrupt_backup_slot) + ".bak"):
		return false
	_reset_signal_counts()
	var corrupt_backup_result := save_manager.call("load_from_slot_sync", corrupt_backup_slot) as Dictionary
	if not corrupt_backup_result.is_empty() or not _expect_signal_counts(0, 1, 0, "missing primary with corrupt backup"):
		return false
	if _load_failed_errors[0] != ERR_FILE_CORRUPT:
		return _fail("Missing primary did not report the corrupt backup error")

	var missing_slot := &"backup_smoke_both_missing"
	save_manager.call("ensure_slot_for_file_base", missing_slot, String(missing_slot))
	_reset_signal_counts()
	var missing_result := save_manager.call("load_from_slot_sync", missing_slot) as Dictionary
	if not missing_result.is_empty() or not _expect_signal_counts(0, 1, 0, "both files missing"):
		return false
	if _load_failed_errors[0] != ERR_FILE_NOT_FOUND:
		return _fail("Two missing files did not report ERR_FILE_NOT_FOUND")
	return true


func _test_business_json_damage_does_not_roll_back(save_manager: Node) -> bool:
	var slot := &"backup_smoke_business_damage"
	if not _save(save_manager, slot, {
		"player_name": "older backup",
		"pending_adventure_run_json": "{\"MainStageIndex\":1}",
	}):
		return false
	if not _save(save_manager, slot, {
		"player_name": "current primary",
		"pending_adventure_run_json": "{damaged adventure run",
	}):
		return false
	_reset_signal_counts()

	var loaded := save_manager.call("load_from_slot_sync", slot) as Dictionary
	if String(loaded.get("player_name", "")) != "current primary":
		return _fail("Business-field damage incorrectly rolled the whole profile back")
	if String(loaded.get("pending_adventure_run_json", "")) != "{damaged adventure run":
		return _fail("Business-field damage was not preserved for business recovery")
	return _expect_signal_counts(1, 0, 0, "business-field damage")


func _save(save_manager: Node, slot: StringName, data: Dictionary) -> bool:
	save_manager.call("ensure_slot_for_file_base", slot, String(slot))
	var result := int(save_manager.call("save_to_slot_sync", slot, data))
	return result == OK or _fail("Failed to seed slot %s: %d" % [String(slot), result])


func _slot_path(slot: StringName) -> String:
	return TEST_SAVE_ROOT.path_join(String(slot) + ".json")


func _write_corrupt_file(path: String) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return _fail("Failed to write corrupt fixture %s: %d" % [path, FileAccess.get_open_error()])
	file.store_string("not a SaveState file")
	var error := file.get_error()
	file.close()
	return error == OK or _fail("Failed to close corrupt fixture %s: %d" % [path, error])


func _read_all_bytes(path: String) -> PackedByteArray:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return PackedByteArray()
	var bytes := file.get_buffer(file.get_length())
	file.close()
	return bytes


func _reset_signal_counts() -> void:
	_load_completed_count = 0
	_load_failed_errors.clear()
	_backup_recoveries.clear()


func _expect_signal_counts(completed: int, failed: int, recovered: int, scenario: String) -> bool:
	if _load_completed_count != completed or _load_failed_errors.size() != failed or _backup_recoveries.size() != recovered:
		return _fail(
			"Unexpected signals for %s: completed=%d failed=%d recovered=%d"
			% [scenario, _load_completed_count, _load_failed_errors.size(), _backup_recoveries.size()]
		)
	return true


func _on_load_completed(_slot_id: StringName, _data: Dictionary) -> void:
	_load_completed_count += 1


func _on_load_failed(_slot_id: StringName, error: int) -> void:
	_load_failed_errors.append(error)


func _on_backup_recovered(slot_id: StringName, primary_error: int, repair_error: int) -> void:
	_backup_recoveries.append({
		"slot_id": slot_id,
		"primary_error": primary_error,
		"repair_error": repair_error,
	})


func _fail(message: String) -> bool:
	if not _failed:
		_failed = true
		push_error(message)
		print("SAVE_STATE_BACKUP_RECOVERY_SMOKE_FAIL %s" % message)
		quit(1)
	return false
