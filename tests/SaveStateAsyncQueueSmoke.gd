extends SceneTree

const TEST_SAVE_ROOT := "user://save_state_async_queue_smoke"
const WORKER_DELAY_MSEC := 250

var _failed := false
var _save_completions: Array[Dictionary] = []
var _flush_completions: Dictionary = {}


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
	save_manager.set("auto_save_debounce_sec", 10.0)
	save_manager.call("clear_kv_cache")
	var mkdir_error := DirAccess.make_dir_recursive_absolute(
		ProjectSettings.globalize_path(TEST_SAVE_ROOT)
	)
	if mkdir_error != OK:
		_fail("Failed to create async smoke save root: %d" % mkdir_error)
		return

	save_manager.async_save_finished.connect(_on_async_save_finished)
	save_manager.async_flush_finished.connect(_on_async_flush_finished)
	if not await _test_fifo_and_frame_progress(save_manager):
		return
	if not await _test_failure_preserves_committed_files(save_manager):
		return
	if not await _test_stale_kv_snapshot_keeps_dirty(save_manager):
		return
	if not await _test_shutdown_seal_preserves_only_preexisting_dirty_data(save_manager):
		return

	print("SAVE_STATE_ASYNC_QUEUE_SMOKE_OK")
	quit(0)


func _test_fifo_and_frame_progress(save_manager: Node) -> bool:
	_reset_completions()
	var slot_a := &"async_fifo_a"
	var slot_b := &"async_fifo_b"
	save_manager.call("ensure_slot_for_file_base", slot_a, String(slot_a))
	save_manager.call("ensure_slot_for_file_base", slot_b, String(slot_b))
	save_manager.call("configure_async_write_debug", WORKER_DELAY_MSEC, 0)
	var writes_before := int(save_manager.call("get_async_physical_write_count_debug"))

	if int(save_manager.call("queue_save_to_slot", 1001, slot_a, {"sequence": 1})) != OK:
		return _fail("Failed to queue first FIFO write")
	if int(save_manager.call("save_to_slot_sync", slot_a, {"sequence": 99})) != ERR_BUSY:
		return _fail("Synchronous same-slot write was not rejected as busy")
	if int(save_manager.call("queue_save_to_slot", 1002, slot_a, {"sequence": 2})) != OK:
		return _fail("Failed to queue second FIFO write")
	if int(save_manager.call("queue_save_to_slot", 1003, slot_b, {"sequence": 3})) != OK:
		return _fail("Failed to queue cross-slot FIFO write")
	if int(save_manager.call("queue_flush_barrier", 1004)) != OK:
		return _fail("Failed to queue FIFO flush barrier")

	var frame_count := await _wait_for_flush(1004)
	if frame_count < 3:
		return _fail("Main thread advanced only %d frames during delayed writes" % frame_count)
	if _save_completions.size() != 3:
		return _fail("Expected 3 async save completions, got %d" % _save_completions.size())
	for index in range(3):
		var expected_request_id := 1001 + index
		var completion: Dictionary = _save_completions[index]
		if int(completion.get("request_id", 0)) != expected_request_id:
			return _fail("FIFO completion order mismatch at %d" % index)
		if int(completion.get("error", FAILED)) != OK:
			return _fail("FIFO request %d failed" % expected_request_id)
	if String(_save_completions[0].get("slot_id", &"")) != String(slot_a) \
		or String(_save_completions[1].get("slot_id", &"")) != String(slot_a) \
		or String(_save_completions[2].get("slot_id", &"")) != String(slot_b):
		return _fail("Async request IDs were paired with the wrong slots")

	var writes_after := int(save_manager.call("get_async_physical_write_count_debug"))
	if writes_after - writes_before != 3:
		return _fail("FIFO issued %d worker writes instead of 3" % (writes_after - writes_before))
	var loaded_a := save_manager.call("load_from_slot_sync", slot_a) as Dictionary
	var loaded_b := save_manager.call("load_from_slot_sync", slot_b) as Dictionary
	if int(loaded_a.get("sequence", 0)) != 2 or int(loaded_b.get("sequence", 0)) != 3:
		return _fail("FIFO did not leave the newest same-slot snapshots on disk")
	return true


func _test_failure_preserves_committed_files(save_manager: Node) -> bool:
	_reset_completions()
	var slot := &"async_failure_preserves"
	save_manager.call("ensure_slot_for_file_base", slot, String(slot))
	save_manager.call("configure_async_write_debug", 0, 0)
	if int(save_manager.call("save_to_slot_sync", slot, {"marker": "backup"})) != OK:
		return _fail("Failed to seed async failure backup")
	if int(save_manager.call("save_to_slot_sync", slot, {"marker": "primary"})) != OK:
		return _fail("Failed to seed async failure primary")
	var path := TEST_SAVE_ROOT.path_join(String(slot) + ".json")
	var primary_before := _read_all_bytes(path)
	var backup_before := _read_all_bytes(path + ".bak")

	save_manager.call("configure_async_write_debug", WORKER_DELAY_MSEC, 1)
	if int(save_manager.call("queue_save_to_slot", 2001, slot, {"marker": "rejected"})) != OK:
		return _fail("Failed to queue injected async failure")
	if int(save_manager.call("queue_flush_barrier", 2002)) != OK:
		return _fail("Failed to queue failure flush barrier")
	await _wait_for_flush(2002)
	if _save_completions.size() != 1 \
		or int(_save_completions[0].get("request_id", 0)) != 2001 \
		or int(_save_completions[0].get("error", OK)) == OK:
		return _fail("Injected failure completion was missing or crossed request IDs")
	if _read_all_bytes(path) != primary_before or _read_all_bytes(path + ".bak") != backup_before:
		return _fail("Failed async write changed the committed primary or backup")
	if FileAccess.file_exists(path + ".tmp"):
		return _fail("Failed async write leaked a temporary file")
	return true


func _test_stale_kv_snapshot_keeps_dirty(save_manager: Node) -> bool:
	_reset_completions()
	save_manager.call("configure_async_write_debug", WORKER_DELAY_MSEC, 0)
	save_manager.call("set_value", &"async.versioned", "old")
	save_manager.call("mark_dirty")
	if int(save_manager.call("flush_dirty_async", 3001)) != OK:
		return _fail("Failed to queue first dirty flush")
	await process_frame
	save_manager.call("set_value", &"async.versioned", "new")
	save_manager.call("mark_dirty")
	await _wait_for_flush(3001)

	if int(save_manager.call("flush_dirty_async", 3002)) != OK:
		return _fail("Stale completion incorrectly cleared the newer dirty snapshot")
	await _wait_for_flush(3002)
	var loaded := save_manager.call("load_from_slot_sync", &"slot_0") as Dictionary
	if String(loaded.get("async.versioned", "")) != "new":
		return _fail("Second dirty flush did not persist the newest KV version")
	return true


func _test_shutdown_seal_preserves_only_preexisting_dirty_data(save_manager: Node) -> bool:
	_reset_completions()
	save_manager.set("auto_save_debounce_sec", 0.05)
	save_manager.call("configure_async_write_debug", 0, 0)
	save_manager.call("set_value", &"async.shutdown.preexisting", "persisted")
	if int(save_manager.call("mark_dirty")) != OK:
		return _fail("Failed to mark the pre-shutdown KV value dirty")

	save_manager.call("begin_async_shutdown")
	var writes_before_wait := int(save_manager.call("get_async_physical_write_count_debug"))
	if int(save_manager.call("mark_dirty")) != ERR_UNAVAILABLE:
		return _fail("Shutdown seal continued accepting debounced dirty writes")
	await create_timer(0.15).timeout
	if int(save_manager.call("get_async_physical_write_count_debug")) != writes_before_wait:
		return _fail("Shutdown seal allowed the debounce timer to enqueue a physical write")

	if int(save_manager.call("flush_dirty_async", 4001)) != OK:
		return _fail("Shutdown flush rejected dirty data admitted before the seal")
	await _wait_for_flush(4001)
	if _failed:
		return false
	if int(save_manager.call("get_async_physical_write_count_debug")) != writes_before_wait + 1:
		return _fail("Shutdown dirty flush did not issue exactly one physical write")

	if int(save_manager.call("queue_flush_barrier", 4002)) != OK:
		return _fail("Failed to queue the post-shutdown flush barrier")
	await _wait_for_flush(4002)
	if _failed:
		return false
	if not bool(save_manager.call("is_async_write_idle")) \
			or int(save_manager.call("get_async_queue_depth")) != 0:
		return _fail("Shutdown barrier completed before the async queue became idle")

	var loaded := save_manager.call("load_from_slot_sync", &"slot_0") as Dictionary
	if String(loaded.get("async.shutdown.preexisting", "")) != "persisted":
		return _fail("Shutdown flush lost KV data marked dirty before the seal")
	return true


func _wait_for_flush(request_id: int) -> int:
	var frame_count := 0
	while not _flush_completions.has(request_id):
		if frame_count > 10000:
			_fail("Timed out waiting for async flush request %d" % request_id)
			return frame_count
		await process_frame
		frame_count += 1
	if int(_flush_completions.get(request_id, FAILED)) != OK:
		_fail("Async flush request %d failed" % request_id)
	return frame_count


func _read_all_bytes(path: String) -> PackedByteArray:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return PackedByteArray()
	var bytes := file.get_buffer(file.get_length())
	file.close()
	return bytes


func _reset_completions() -> void:
	_save_completions.clear()
	_flush_completions.clear()


func _on_async_save_finished(request_id: int, slot_id: StringName, error: int) -> void:
	_save_completions.append({
		"request_id": request_id,
		"slot_id": slot_id,
		"error": error,
	})


func _on_async_flush_finished(request_id: int, error: int) -> void:
	_flush_completions[request_id] = error


func _fail(message: String) -> bool:
	if not _failed:
		_failed = true
		push_error(message)
		print("SAVE_STATE_ASYNC_QUEUE_SMOKE_FAIL %s" % message)
		quit(1)
	return false
