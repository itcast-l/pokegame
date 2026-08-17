extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const PROFILE_SLOT := &"profile_0"
const ADVENTURE_PLAYER_AVATAR_ID := "bear"
const ADVENTURE_PLAYER_AVATAR_ROOT := "res://art/characters/player_bear_avatar/run-001"
const TIMEOUT_SECONDS := 25.0
const MODE_SPECS := [
	{
		"name": "solo_low",
		"player_count": 2,
		"item_mode": true,
		"winning_score": 100,
		"solo_mode": true,
		"solo_ai_difficulty": 0,
		"adventure_mode": false,
	},
	{
		"name": "solo_high",
		"player_count": 2,
		"item_mode": true,
		"winning_score": 100,
		"solo_mode": true,
		"solo_ai_difficulty": 2,
		"adventure_mode": false,
	},
	{
		"name": "solo_high_four_player",
		"player_count": 4,
		"item_mode": true,
		"winning_score": 100,
		"solo_mode": true,
		"solo_ai_difficulty": 2,
		"adventure_mode": false,
	},
	{
		"name": "adventure",
		"player_count": 2,
		"item_mode": true,
		"winning_score": 100,
		"solo_mode": true,
		"adventure_mode": true,
		"adventure_enemy_avatar_id": "shrimp",
		"expected_player_avatar_root": ADVENTURE_PLAYER_AVATAR_ROOT,
	},
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var requested_mode := _get_requested_mode()
	for spec in MODE_SPECS:
		if _failed:
			return
		if not requested_mode.is_empty() and String(spec.get("name", "")) != requested_mode:
			continue

		await _smoke_mode(spec)

	if not _failed:
		print("GAME_BOARD_MODE_REGRESSION_SMOKE_OK")
		await _dispose_audio_before_quit()
		quit(0)


func _get_requested_mode() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--mode="):
			return argument.trim_prefix("--mode=")
	return ""


func _smoke_mode(spec: Dictionary) -> void:
	if spec.has("expected_player_avatar_root") and not _configure_selected_avatar_profile():
		return

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board: Node
	if bool(spec.get("adventure_mode", false)):
		board = GameBoardSmokeFactory.create_adventure(
			packed_scene,
			String(spec.get("adventure_enemy_avatar_id", "fish")),
			int(spec.get("winning_score", 100)),
			bool(spec.get("item_mode", true)),
			int(spec.get("solo_ai_difficulty", 1)))
	else:
		board = GameBoardSmokeFactory.create_solo(
			packed_scene,
			int(spec.get("player_count", 2)),
			bool(spec.get("item_mode", true)),
			int(spec.get("winning_score", 100)),
			int(spec.get("solo_ai_difficulty", 1)))
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	var mode_name := String(spec.get("name", "unknown"))
	var mode_label := mode_name.to_upper()
	root.add_child(board)
	await process_frame
	if spec.has("expected_player_avatar_root"):
		var avatar_area := board.get_node_or_null("%Player1AvatarArea")
		var player_avatar := avatar_area.get_node_or_null("Player1Avatar") if avatar_area != null else null
		var actual_avatar_root := String(player_avatar.get("AvatarRoot")) if player_avatar != null else ""
		var expected_avatar_root := String(spec.get("expected_player_avatar_root", ""))
		if actual_avatar_root != expected_avatar_root:
			_fail("%s player avatar mismatch: expected %s, got %s" % [
				mode_name,
				expected_avatar_root,
				actual_avatar_root,
			])
			return

	if not board.has_method("DebugRestartWithoutOpeningDealAnimation"):
		_fail("%s fast mode setup hook missing" % mode_name)
		return
	if not await _prepare_fast_board(board):
		_fail("%s fast mode setup hook rejected the board" % mode_name)
		return
	await process_frame

	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	var played_card := false
	var saw_opponent_turn := false

	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("%s board freed unexpectedly" % mode_name)
			return

		if not board.has_method("DebugGetGameLoopPhaseName") \
			or not board.has_method("DebugGetCurrentTurnPlayerIndex") \
			or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard"):
			_fail("%s debug hooks missing" % mode_name)
			return

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		var current_turn := int(board.call("DebugGetCurrentTurnPlayerIndex"))

		if not played_card:
			if phase == "OpeningDeal":
				continue

			if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
				continue

			played_card = true
			print("%s_PLAYED_CARD" % mode_label)
			continue

		if current_turn != 0 and not saw_opponent_turn:
			saw_opponent_turn = true
			print("%s_SAW_OPPONENT_TURN" % mode_label)

		if saw_opponent_turn and current_turn == 0 and phase == "AwaitingMainPlayerAction":
			board.queue_free()
			await process_frame
			await process_frame
			print("MODE_OK %s" % mode_name)
			return

	_fail("%s mode timed out" % mode_name)


func _prepare_fast_board(board: Node) -> bool:
	for frame_index in range(300):
		await process_frame
		if board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
				and bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
				return false
			continue
		if frame_index % 5 == 0 \
				and bool(board.call("DebugRestartWithoutOpeningDealAnimation")):
			return true
	return false


func _configure_selected_avatar_profile() -> bool:
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return false

	save_manager.call("ensure_slot_for_file_base", PROFILE_SLOT, String(PROFILE_SLOT))
	save_manager.call("set_value", &"progress.active_profile_slot", String(PROFILE_SLOT))
	if int(save_manager.call("persist")) != OK:
		_fail("Failed to persist the active avatar smoke profile")
		return false

	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 2,
		"highest_completed_main_adventure_stage": 0,
		"completed_adventure_tutorial_ids_csv": "",
		"selected_avatar_id": ADVENTURE_PLAYER_AVATAR_ID,
		"player_name": "Avatar Smoke",
		"pending_avatar_unlock_queue": "",
		"pending_adventure_run_json": "",
		"playtime_seconds": 0.0,
		"created_unix_time": now,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", PROFILE_SLOT, profile)) != OK:
		_fail("Failed to write the avatar smoke profile")
		return false

	return true


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("GAME_BOARD_MODE_REGRESSION_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
