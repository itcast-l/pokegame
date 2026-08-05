extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 25.0
const IDLE_SETTLE_FRAMES := 20
const IDLE_OBSERVE_FRAMES := 300
# 2026-08-04 reference run: 10/10 samples were 0 bytes, so P95 was 0 bytes.
const MAX_CSHARP_ALLOCATION_GROWTH_BYTES := 1 * 1024 * 1024

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

		if bool(board.call("DebugIsNetworkVisualQueueProcessing")) \
			or int(board.call("DebugGetNetworkVisualEventQueueCount")) != 0:
			_fail("Network visual queue was not idle before observation")
			return

		var resource_load_count := int(board.call("DebugGetResourceLoadCount"))
		var background_load_count := int(board.call("DebugGetDynamicBackgroundResourceLoadCount"))
		var background_rebuild_count := int(board.call("DebugGetDynamicBackgroundRebuildCount"))
		var network_activity_count := int(board.call("DebugGetNetworkVisualActivityCount"))
		var allocated_bytes := int(board.call("DebugGetCurrentThreadAllocatedBytes"))
		print("IDLE_WINDOW_BEGIN")
		for _frame in IDLE_OBSERVE_FRAMES:
			await process_frame
			if bool(board.call("DebugIsNetworkVisualQueueProcessing")) \
				or int(board.call("DebugGetNetworkVisualEventQueueCount")) != 0:
				_fail("Network visual queue became active during idle observation")
				return
		print("IDLE_WINDOW_END")

		var allocation_growth := int(board.call("DebugGetCurrentThreadAllocatedBytes")) - allocated_bytes
		print("IDLE_CSHARP_ALLOCATION_GROWTH_BYTES %d" % allocation_growth)
		if int(board.call("DebugGetResourceLoadCount")) != resource_load_count:
			_fail("Board resource-load count changed during idle observation")
			return
		if int(board.call("DebugGetDynamicBackgroundResourceLoadCount")) != background_load_count:
			_fail("Dynamic background resource-load count changed during idle observation")
			return
		if int(board.call("DebugGetDynamicBackgroundRebuildCount")) != background_rebuild_count:
			_fail("Dynamic background rebuild count changed during idle observation")
			return
		if int(board.call("DebugGetNetworkVisualActivityCount")) != network_activity_count:
			_fail("Network visual activity count changed during idle observation")
			return
		if allocation_growth < 0 or allocation_growth > MAX_CSHARP_ALLOCATION_GROWTH_BYTES:
			_fail("C# allocation growth exceeded idle ceiling: %d" % allocation_growth)
			return

		print("GAME_BOARD_IDLE_PERFORMANCE_TRACE_SMOKE_OK")
		board.queue_free()
		await process_frame
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
	for _frame in 2:
		await process_frame
	await create_timer(0.15).timeout
	for _frame in 2:
		await process_frame
