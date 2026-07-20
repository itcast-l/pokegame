extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const PROFILE_SLOT := &"profile_0"
const DAMAGED_RUN_JSON := "{damaged adventure run"
const TIMEOUT_SECONDS := 20.0
const TUTORIAL_AVATAR_ID := "fish"
const MAIN_FIRST_AVATAR_ID := "shrimp"

var _failed := false

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(960, 540)
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return

	save_manager.call("ensure_slot_for_file_base", PROFILE_SLOT, String(PROFILE_SLOT))
	save_manager.call("set_value", &"migration.settings_cfg.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v2", true)
	save_manager.call("set_value", &"progress.active_profile_slot", String(PROFILE_SLOT))
	if int(save_manager.call("persist")) != OK:
		_fail("Failed to persist active recovery profile")
		return

	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 2,
		"highest_completed_main_adventure_stage": 3,
		"completed_adventure_tutorial_ids_csv": "fish",
		"selected_avatar_id": "fox",
		"player_name": "Recovery",
		"pending_avatar_unlock_queue": "shrimp",
		"pending_adventure_run_json": DAMAGED_RUN_JSON,
		"playtime_seconds": 123.0,
		"created_unix_time": now - 100,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", PROFILE_SLOT, profile)) != OK:
		_fail("Failed to seed damaged adventure run")
		return

	var packed_menu := load(MENU_SCENE_PATH) as PackedScene
	var menu := packed_menu.instantiate() if packed_menu != null else null
	if menu == null:
		_fail("Failed to instantiate adventure menu")
		return
	menu.set("selected_avatar_id", TUTORIAL_AVATAR_ID)
	root.add_child(menu)
	for _frame_index in range(12):
		await process_frame
	if bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
		_fail("Damaged main payload should not show recovery while the tutorial is selected")
		return

	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null:
		_fail("Tutorial menu is missing StartButton")
		return
	start_button.emit_signal("pressed")
	var tutorial_board := await _wait_for_board()
	if _failed or tutorial_board == null:
		return
	if not bool(tutorial_board.get("adventure_tutorial_mode")) \
		or String(tutorial_board.get("adventure_enemy_avatar_id")) != TUTORIAL_AVATAR_ID:
		_fail("Damaged main payload prevented the selected fish tutorial from starting")
		return

	var preserved := save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary
	if String(preserved.get("pending_adventure_run_json", "")) != DAMAGED_RUN_JSON:
		_fail("Starting the tutorial overwrote the damaged main payload")
		return

	tutorial_board.queue_free()
	await process_frame
	await process_frame

	var main_menu := packed_menu.instantiate()
	if main_menu == null:
		_fail("Failed to instantiate the main adventure menu")
		return
	main_menu.set("selected_avatar_id", MAIN_FIRST_AVATAR_ID)
	root.add_child(main_menu)
	if not await _wait_for_dialog(main_menu):
		return

	var dialog := main_menu.get_node_or_null("AdventureRunResumeDialog") as Control
	var continue_frame := dialog.get_node_or_null("DialogMotion/Panel/ContentMargin/Layout/ButtonRow/ContinueFrame") as Control if dialog != null else null
	var panel := dialog.get_node_or_null("DialogMotion/Panel") as Control if dialog != null else null
	if dialog == null or continue_frame == null or panel == null or continue_frame.visible:
		_fail("Returning to the main campaign must show recovery and hide Continue")
		return
	var viewport_rect := root.get_visible_rect()
	var panel_rect := panel.get_global_rect()
	if not viewport_rect.encloses(panel_rect):
		_fail("Recovery dialog overflows the minimum viewport")
		return

	if not bool(main_menu.call("DebugSelectAdventureRunResumeRestart")):
		_fail("Failed to confirm adventure recovery")
		return
	var board := await _wait_for_board()
	if _failed or board == null:
		return
	if bool(board.get("adventure_tutorial_mode")) \
		or String(board.get("adventure_enemy_avatar_id")) != MAIN_FIRST_AVATAR_ID:
		_fail("Recovery did not restart from the first main stage")
		return

	var recovered := save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary
	var recovered_run: Variant = JSON.parse_string(String(recovered.get("pending_adventure_run_json", "")))
	if typeof(recovered_run) != TYPE_DICTIONARY or int((recovered_run as Dictionary).get("MainStageIndex", 0)) != 1:
		_fail("Recovery did not persist a new first-stage run")
		return
	for key in ["highest_completed_main_adventure_stage", "completed_adventure_tutorial_ids_csv", "selected_avatar_id", "player_name", "pending_avatar_unlock_queue", "playtime_seconds", "created_unix_time"]:
		if recovered.get(key) != preserved.get(key):
			_fail("Recovery changed unrelated profile field %s" % key)
			return

	print("ADVENTURE_RUN_RECOVERY_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)

func _wait_for_dialog(menu: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if is_instance_valid(menu) and bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
			return true
	_fail("Timed out waiting for recovery dialog")
	return false

func _wait_for_board() -> Node:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		var scene := current_scene
		if scene != null and is_instance_valid(scene) and scene.has_method("DebugGetAdventureAcquiredUpgradeCount"):
			return scene
	_fail("Timed out waiting for recovered adventure board")
	return null

func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_RUN_RECOVERY_SMOKE_FAIL %s" % message)
	quit(1)

func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
		await process_frame
		await process_frame
