extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const PROFILE_SLOT := &"profile_0"
const PROFILE_NAME := "Ocean"
const NETWORK_NAMES := ["Host", "Guest", "Third", "Fourth"]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	if not _seed_profile():
		return

	if not await _verify_adventure_player_name():
		return

	if not await _verify_network_player_names():
		return

	print("GAME_BOARD_WINNER_PLAYER_NAME_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _seed_profile() -> bool:
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return false

	save_manager.call("ensure_slot_for_file_base", PROFILE_SLOT, String(PROFILE_SLOT))
	save_manager.call("set_value", &"migration.settings_cfg.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v2", true)
	save_manager.call("set_value", &"progress.active_profile_slot", String(PROFILE_SLOT))
	if int(save_manager.call("persist")) != OK:
		_fail("Failed to persist active winner-name smoke profile")
		return false

	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 3,
		"highest_completed_main_adventure_stage": 0,
		"completed_adventure_tutorial_ids_csv": "",
		"selected_avatar_id": "cat",
		"player_name": PROFILE_NAME,
		"pending_avatar_unlock_queue": "",
		"pending_adventure_run_json": "",
		"unlocked_skill_codex_upgrade_ids_csv": "",
		"playtime_seconds": 0.0,
		"created_unix_time": now,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", PROFILE_SLOT, profile)) != OK:
		_fail("Failed to write winner-name smoke profile")
		return false

	return true


func _verify_adventure_player_name() -> bool:
	var board := await _instantiate_board({
		"player_count": 2,
		"solo_mode": true,
		"adventure_mode": true,
		"adventure_enemy_avatar_id": "fish",
		"network_wait_for_initialize": true,
	})
	if board == null:
		return false

	var winner_text := String(board.call("DebugGetWinnerTextForPlayer", 0))
	var expected_text := tr("GAME_WINNER").format([PROFILE_NAME])
	if winner_text != expected_text:
		_fail("Adventure winner should use profile name: expected '%s', got '%s'" % [expected_text, winner_text])
		return false

	await _dispose_board(board)
	return true


func _verify_network_player_names() -> bool:
	var board := await _instantiate_board({
		"player_count": NETWORK_NAMES.size(),
		"solo_mode": false,
		"network_session_id": "winner-name-smoke-session",
		"network_match_id": "winner-name-smoke-match",
		"local_player_index": 1,
		"player_names_csv": ",".join(NETWORK_NAMES),
		"player_avatar_ids_csv": "cat,fox,bear,crab",
		"network_wait_for_initialize": true,
	})
	if board == null:
		return false

	for player_index in range(NETWORK_NAMES.size()):
		var player_name := String(NETWORK_NAMES[player_index])
		var winner_text := String(board.call("DebugGetWinnerTextForPlayer", player_index))
		var expected_text := tr("GAME_WINNER").format([player_name])
		if winner_text != expected_text:
			_fail("Network player %d winner should use '%s': got '%s'" % [player_index + 1, player_name, winner_text])
			return false

	await _dispose_board(board)
	return true


func _instantiate_board(properties: Dictionary) -> Node:
	var packed_scene := load(SCENE_PATH) as PackedScene
	var board := packed_scene.instantiate() if packed_scene != null else null
	if board == null:
		_fail("Failed to instantiate game board scene")
		return null

	for property_name in properties:
		board.set(property_name, properties[property_name])
	root.add_child(board)
	await process_frame
	if not board.has_method("DebugGetWinnerTextForPlayer"):
		_fail("Winner player name debug hook is missing")
		return null
	return board


func _dispose_board(board: Node) -> void:
	if is_instance_valid(board):
		board.queue_free()
	await process_frame
	await process_frame


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("GAME_BOARD_WINNER_PLAYER_NAME_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
		await process_frame
		await process_frame
