extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 25.0
const IDLE_SETTLE_FRAMES := 20
const IDLE_OBSERVE_FRAMES := 90

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("solo_ai_difficulty", 0)
	board.set("adventure_mode", false)
	board.set("debug_performance_trace", true)
	board.set("debug_performance_trace_min_ms", 0.0)

	root.add_child(board)
	await process_frame

	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	var played_card := false
	var saw_opponent_turn := false

	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly")
			return

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		var current_turn := int(board.call("DebugGetCurrentTurnPlayerIndex"))

		if not played_card:
			if phase == "OpeningDeal":
				continue

			if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
				continue

			played_card = true
			print("IDLE_TRACE_PLAYED_CARD")
			continue

		if current_turn != 0:
			saw_opponent_turn = true
			continue

		if not saw_opponent_turn or phase != "AwaitingMainPlayerAction":
			continue

		for _frame in IDLE_SETTLE_FRAMES:
			await process_frame

		print("IDLE_WINDOW_BEGIN")
		for _frame in IDLE_OBSERVE_FRAMES:
			await process_frame
		print("IDLE_WINDOW_END")
		print("GAME_BOARD_IDLE_PERFORMANCE_TRACE_SMOKE_OK")
		await _dispose_audio_before_quit()
		quit(0)
		return

	_fail("Idle performance trace smoke timed out")


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("GAME_BOARD_IDLE_PERFORMANCE_TRACE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
