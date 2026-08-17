extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "shrimp")
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return


	root.add_child(board)
	await process_frame

	if not board.has_method("DebugForceNextAdventureUpgradeOverlayDisplayFailure") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugGetPendingAdventureUpgradeTriggerCount") \
		or not board.has_method("DebugIsAdventureUpgradeResolverBusy") \
		or not board.has_method("DebugIsAdventureUpgradeSelectionVisible"):
		_fail("Missing adventure upgrade display-failure debug hooks")
		return

	board.call("DebugForceNextAdventureUpgradeOverlayDisplayFailure")

	if not await _wait_for_failure_path_to_complete(board):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Display failure should auto-resolve exactly one adventure upgrade")
		return

	if int(board.call("DebugGetPendingAdventureUpgradeTriggerCount")) != 0:
		_fail("Display failure should consume the pending trigger after auto-resolving the reward")
		return

	if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
		_fail("Adventure upgrade selection overlay should not remain visible after display failure")
		return

	board.queue_free()
	for _frame in range(12):
		await process_frame
	print("ADVENTURE_UPGRADE_OVERLAY_DISPLAY_FAILURE_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _wait_for_failure_path_to_complete(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for display-failure resolution")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			continue

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "AwaitingMainPlayerAction" and not bool(board.call("DebugIsAdventureUpgradeResolverBusy")):
			return true

	_fail("Timed out waiting for adventure upgrade display failure path to complete")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_OVERLAY_DISPLAY_FAILURE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(12):
		await process_frame
