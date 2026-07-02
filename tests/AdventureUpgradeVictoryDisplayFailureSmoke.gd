extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0
const FORCED_VICTORY_POINTS := 100

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
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")

	root.add_child(board)
	await process_frame

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugAddScoreAndCheckWinner") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugGetGameLoopPhaseName") \
		or not board.has_method("DebugForceNextAdventureUpgradeOverlayDisplayFailure"):
		_fail("Missing adventure upgrade victory display-failure debug hooks")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to complete run-start upgrade selection")
		return

	if not await _wait_for_actionable_state(board):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Expected one acquired upgrade after run-start selection")
		return

	board.call("DebugForceNextAdventureUpgradeOverlayDisplayFailure")

	if not bool(board.call("DebugAddScoreAndCheckWinner", MAIN_PLAYER_INDEX, FORCED_VICTORY_POINTS)):
		_fail("Failed to force victory through debug score hook")
		return

	if not await _wait_for_winner_phase(board):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 2:
		_fail("Victory display failure should auto-resolve one upgrade before winner phase")
		return

	if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
		_fail("Victory display failure should not leave the upgrade selection overlay visible")
		return

	board.queue_free()
	for _frame in range(12):
		await process_frame
	print("ADVENTURE_UPGRADE_VICTORY_DISPLAY_FAILURE_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for upgrade selection")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

	_fail("Timed out waiting for upgrade selection")
	return false


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for actionable state")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			continue

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "AwaitingMainPlayerAction":
			return true

	_fail("Timed out waiting for actionable state")
	return false


func _wait_for_winner_phase(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for winner state")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			continue

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "GameOver":
			return true

	_fail("Timed out waiting for game-over state after victory display failure")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_VICTORY_DISPLAY_FAILURE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(12):
		await process_frame
