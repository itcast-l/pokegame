extends SceneTree

const ADVENTURE_MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const TEST_SAVE_ROOT := "user://non_main_shutdown_routing_smoke"
const SHUTDOWN_KEY := "shutdown.non_main.marker"

var _failed := false
var _save_manager: Node
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
		_fail("Failed to create non-main shutdown smoke save root")
		return

	var packed_scene := load(ADVENTURE_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load the adventure menu scene")
		return
	var menu := packed_scene.instantiate()
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(3)
	if not menu.has_method("RequestApplicationQuit"):
		_fail("Adventure menu does not expose the shared application quit route")
		return
	_save_manager.async_flush_finished.connect(_on_async_flush_finished)
	_save_manager.call("set_value", StringName(SHUTDOWN_KEY), "committed")
	_save_manager.call("mark_dirty")

	_save_manager.notification(Node.NOTIFICATION_WM_CLOSE_REQUEST)
	packed_scene = null
	menu = null
	while not _failed:
		await process_frame
		_frames_after_close += 1
		if _frames_after_close > 1200:
			_fail("Timed out waiting for the non-main shutdown route")
			return


func _on_async_flush_finished(_request_id: int, error: int) -> void:
	if error != OK:
		_fail("Non-main shutdown flush failed with error %d" % error)
		return
	_flush_completion_count += 1
	if _flush_completion_count < 2:
		return
	if not bool(_save_manager.call("is_async_write_idle")):
		_fail("Non-main shutdown barrier completed before the FIFO became idle")
		return
	var persisted := _save_manager.call("load_from_slot_sync", &"slot_0") as Dictionary
	if String(persisted.get(SHUTDOWN_KEY, "")) != "committed":
		_fail("Non-main shutdown route lost its deferred value")
		return
	print("NON_MAIN_SHUTDOWN_ROUTING_SMOKE_OK")


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("NON_MAIN_SHUTDOWN_ROUTING_SMOKE_FAIL %s" % message)
	quit(1)
