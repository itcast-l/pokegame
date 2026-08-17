extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const PROFILE_SLOT := &"profile_2"
const AVATAR_ID := "shrimp"
const TIMEOUT_SECONDS := 20.0

var _failed := false
var _save_manager: Node


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	_save_manager = await _configure_profile()
	if _failed or _save_manager == null:
		return

	var menu := await _instantiate_menu()
	if menu == null:
		return
	if not await _wait_for_consumed_unlock_prompt(menu):
		return

	var confirm_button := menu.get_node_or_null(
		"AvatarUnlockPromptRoot/PromptCenter/AvatarUnlockPromptPanel/PromptMargin/PromptLayout/PromptActions/ConfirmFrame/ConfirmButton"
	) as Button
	if confirm_button == null:
		_fail("Adventure avatar unlock prompt is missing its Confirm button")
		return
	confirm_button.emit_signal("pressed")
	if not await _wait_for_unlock_consumption_idle(menu):
		return

	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null or start_button.disabled:
		_fail("Adventure Start was unavailable after avatar unlock consumption")
		return
	start_button.emit_signal("pressed")
	if not await _wait_for_persisted_run():
		return

	var persisted := _load_profile()
	if not String(persisted.get("pending_avatar_unlock_queue", "")).is_empty():
		_fail("Starting a run restored the consumed avatar unlock queue")
		return
	if String(persisted.get("unlocked_skill_codex_upgrade_ids_csv", "")) != "hearts_thick_blood" \
			or String(persisted.get("player_name", "")) != "UnlockTest" \
			or not is_equal_approx(float(persisted.get("playtime_seconds", 0.0)), 345.5):
		_fail(
			"Starting a run regressed unrelated committed profile fields (player=%s skills=%s playtime=%s)"
			% [
				String(persisted.get("player_name", "")),
				String(persisted.get("unlocked_skill_codex_upgrade_ids_csv", "")),
				str(float(persisted.get("playtime_seconds", 0.0))),
			]
		)
		return

	await _wait_for_board_or_settled_menu(menu)
	await _dispose_runtime_scene()
	print("ADVENTURE_AVATAR_UNLOCK_PERSISTENCE_SMOKE_OK")
	quit(0)


func _configure_profile() -> Node:
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return null

	save_manager.call("ensure_slot_for_file_base", PROFILE_SLOT, String(PROFILE_SLOT))
	for key in [
		&"migration.settings_cfg.v1",
		&"migration.progress_profile.v1",
		&"migration.progress_profile.v2",
	]:
		save_manager.call("set_value", key, true)
	save_manager.call("set_value", &"progress.active_profile_slot", String(PROFILE_SLOT))
	if int(save_manager.call("persist")) != OK:
		_fail("Could not activate the isolated avatar unlock profile")
		return null

	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 3,
		"highest_completed_main_adventure_stage": 1,
		"completed_adventure_tutorial_ids_csv": "",
		"selected_avatar_id": "fox",
		"player_name": "UnlockTest",
		"pending_avatar_unlock_queue": AVATAR_ID,
		"pending_adventure_run_json": "",
		"unlocked_skill_codex_upgrade_ids_csv": "hearts_thick_blood",
		"playtime_seconds": 345.5,
		"created_unix_time": now,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", PROFILE_SLOT, profile)) != OK:
		_fail("Could not seed the pending avatar unlock profile")
		return null
	return save_manager


func _instantiate_menu() -> Node:
	var packed_scene := load(MENU_SCENE_PATH) as PackedScene
	var menu := packed_scene.instantiate() if packed_scene != null else null
	if menu == null:
		_fail("Could not instantiate the adventure avatar menu")
		return null
	menu.set("selected_avatar_id", AVATAR_ID)
	root.add_child(menu)
	current_scene = menu
	await process_frame
	return menu


func _wait_for_consumed_unlock_prompt(menu: Node) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		if not is_instance_valid(menu):
			return _fail("Adventure menu disappeared while consuming the avatar unlock")
		if bool(menu.call("DebugIsAdventureAvatarUnlockPromptVisible")) \
				and not bool(menu.call("DebugIsAdventureAvatarUnlockConsumptionInProgress")):
			var profile := _load_profile()
			if String(profile.get("pending_avatar_unlock_queue", "")).is_empty():
				return true
	return _fail("Timed out waiting for the persisted avatar unlock prompt")


func _wait_for_unlock_consumption_idle(menu: Node) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		if not is_instance_valid(menu):
			return _fail("Adventure menu disappeared after closing the avatar unlock prompt")
		if not bool(menu.call("DebugIsAdventureAvatarUnlockPromptVisible")) \
				and not bool(menu.call("DebugIsAdventureAvatarUnlockConsumptionInProgress")):
			return true
	return _fail("Avatar unlock consumption did not settle after closing the prompt")


func _wait_for_persisted_run() -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var profile := _load_profile()
		if not String(profile.get("pending_adventure_run_json", "")).is_empty():
			return true
	return _fail("Starting adventure did not persist a new run")


func _wait_for_board_or_settled_menu(menu: Node) -> void:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		if not is_instance_valid(menu) or current_scene != menu:
			return


func _load_profile() -> Dictionary:
	return _save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary


func _dispose_runtime_scene() -> void:
	var scene := current_scene
	current_scene = null
	if scene != null and is_instance_valid(scene):
		scene.queue_free()
	for _frame in 8:
		await process_frame
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
	for _frame in 4:
		await process_frame


func _deadline() -> float:
	return Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS


func _before_deadline(deadline: float) -> bool:
	return Time.get_ticks_msec() / 1000.0 < deadline


func _fail(message: String) -> bool:
	if not _failed:
		_failed = true
		push_error(message)
		print("ADVENTURE_AVATAR_UNLOCK_PERSISTENCE_SMOKE_FAIL %s" % message)
		quit(1)
	return false
