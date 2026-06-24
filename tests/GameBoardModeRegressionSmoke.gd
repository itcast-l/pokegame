extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
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
		"adventure_enemy_avatar_id": "fish",
	},
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	for spec in MODE_SPECS:
		if _failed:
			return

		await _smoke_mode(spec)

	if not _failed:
		print("GAME_BOARD_MODE_REGRESSION_SMOKE_OK")
		await _dispose_audio_before_quit()
		quit(0)


func _smoke_mode(spec: Dictionary) -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	var mode_name := String(spec.get("name", "unknown"))
	var mode_label := mode_name.to_upper()
	board.set("player_count", int(spec.get("player_count", 2)))
	board.set("item_mode", bool(spec.get("item_mode", true)))
	board.set("winning_score", int(spec.get("winning_score", 100)))
	board.set("solo_mode", bool(spec.get("solo_mode", true)))
	board.set("solo_ai_difficulty", int(spec.get("solo_ai_difficulty", 1)))
	board.set("adventure_mode", bool(spec.get("adventure_mode", false)))
	if spec.has("adventure_enemy_avatar_id"):
		board.set("adventure_enemy_avatar_id", String(spec.get("adventure_enemy_avatar_id", "fish")))

	root.add_child(board)
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
			print("MODE_OK %s" % mode_name)
			return

	_fail("%s mode timed out" % mode_name)


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
