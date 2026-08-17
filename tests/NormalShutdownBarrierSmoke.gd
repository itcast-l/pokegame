extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const HARNESS_SCRIPT := preload("res://tests/NormalShutdownBarrierHarness.cs")
const TEST_SAVE_ROOT := "user://normal_shutdown_barrier_smoke"
const SHUTDOWN_KEY := "shutdown.barrier.marker"
const PROFILE_SLOT := "profile_1"

var _failed := false
var _save_manager: Node
var _harness: RefCounted
var _writes_before := 0
var _flush_completion_count := 0
var _frames_after_close := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	_save_manager = root.get_node_or_null("SaveManager")
	if _save_manager == null:
		_fail("SaveManager autoload is missing")
		return
	_save_manager.set("save_root", TEST_SAVE_ROOT)
	_save_manager.set("use_json", true)
	_save_manager.set("backup_on_commit", false)
	_save_manager.set("auto_save_debounce_sec", 10.0)
	_save_manager.call("clear_kv_cache")
	if DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(TEST_SAVE_ROOT)) != OK:
		_fail("Failed to create shutdown smoke save root")
		return

	_harness = HARNESS_SCRIPT.new()
	if int(_harness.call("ConfigureProfile", PROFILE_SLOT, "Before", "red_hat_girl")) != OK:
		_fail("Failed to configure the shutdown smoke profile")
		return

	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load the main menu scene")
		return
	var menu := packed_scene.instantiate()
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(3)
	_save_manager.async_flush_finished.connect(_on_async_flush_finished)
	_save_manager.call("set_value", StringName(SHUTDOWN_KEY), "committed")
	_save_manager.call("mark_dirty")
	_writes_before = int(_save_manager.call("get_async_physical_write_count_debug"))
	_save_manager.call("configure_async_write_debug", 250, 0)
	_harness.call("StartProfileWrites")
	if int(_harness.call("GetAdmittedOperationCount")) != 2:
		_fail("Both profile writes were not admitted before shutdown")
		return
	if int(_save_manager.call("get_async_queue_depth")) != 1:
		_fail("The second profile write entered SaveManager before obtaining the coordinator gate")
		return

	menu.notification(Node.NOTIFICATION_WM_CLOSE_REQUEST)
	_harness.call("StartSealedWriteProbe")
	packed_scene = null
	menu = null
	while not _failed:
		await process_frame
		_frames_after_close += 1
		if _frames_after_close > 1200:
			_fail("Timed out waiting for the normal shutdown barrier")
			return


func _on_async_flush_finished(_request_id: int, error: int) -> void:
	if error != OK:
		_fail("Shutdown flush or barrier failed with error %d" % error)
		return
	_flush_completion_count += 1
	if _flush_completion_count < 2:
		return
	if _frames_after_close < 3:
		_fail("Main thread did not advance while the shutdown write was delayed")
		return
	if int(_save_manager.call("get_async_physical_write_count_debug")) != _writes_before + 3:
		_fail("Normal shutdown did not persist both profile writes and one dirty KV write")
		return
	if not bool(_save_manager.call("is_async_write_idle")):
		_fail("Shutdown barrier completed before the FIFO became idle")
		return
	var persisted := _save_manager.call("load_from_slot_sync", &"slot_0") as Dictionary
	if String(persisted.get(SHUTDOWN_KEY, "")) != "committed":
		_fail("Shutdown barrier completed without the deferred value on disk")
		return
	if not bool(_harness.call("AreProfileWritesCompleted")):
		_fail("Shutdown barrier completed before both admitted profile writes settled")
		return
	if int(_harness.call("GetFirstWriteResult")) != OK \
			or int(_harness.call("GetSecondWriteResult")) != OK:
		_fail("An admitted profile write failed during shutdown")
		return
	if int(_harness.call("GetSealedWriteProbeResult")) != ERR_UNAVAILABLE:
		_fail("A profile write started after the coordinator shutdown seal")
		return
	if int(_harness.call("GetAdmittedOperationCount")) != 0:
		_fail("Profile coordinator did not drain every admitted operation")
		return
	var profile := _save_manager.call("load_from_slot_sync", StringName(PROFILE_SLOT)) as Dictionary
	if String(profile.get("player_name", "")) != "FirstWrite":
		_fail("The first admitted profile mutation was not persisted")
		return
	if int(profile.get("highest_completed_main_adventure_stage", 0)) != 2:
		_fail("The coordinator waiter profile mutation was not persisted")
		return
	print("NORMAL_SHUTDOWN_BARRIER_SMOKE_OK")


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("NORMAL_SHUTDOWN_BARRIER_SMOKE_FAIL %s" % message)
	quit(1)
