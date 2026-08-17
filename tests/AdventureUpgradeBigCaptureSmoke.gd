extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const RECORDING_SETUP_ID := "jellyfish_capture_showcase_01"
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

	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "jellyfish")
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	GameBoardSmokeFactory.configure_recording(board, RECORDING_SETUP_ID)

	root.add_child(board)
	await process_frame

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing adventure big-capture debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 0:
		_fail("Expected no acquired upgrades before the big capture play")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the recording setup hand card")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to resolve big-capture upgrade selection")
		return

	if not await _wait_for_post_selection_progress(board):
		return

	var acquired_count := int(board.call("DebugGetAdventureAcquiredUpgradeCount"))
	if acquired_count < 1:
		_fail("Expected at least one acquired upgrade after big-capture selection")
		return

	if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
		if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
			_fail("Failed to resolve follow-up upgrade selection")
			return
		if not await _wait_for_post_selection_progress(board):
			return
		acquired_count = int(board.call("DebugGetAdventureAcquiredUpgradeCount"))

	if acquired_count < 1:
		_fail("Expected acquired upgrades to persist after capture flow")
		return

	board.queue_free()
	await process_frame
	print("ADVENTURE_UPGRADE_BIG_CAPTURE_SMOKE_OK")
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

	_fail("Timed out waiting for big-capture upgrade selection")
	return false


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for actionable state")
			return false

		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true

	_fail("Timed out waiting for actionable state")
	return false


func _wait_for_post_selection_progress(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for post-selection progress")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "AwaitingMainPlayerAction" or phase == "ResolvingTurnSequence" or phase == "GameOver":
			return true

	_fail("Timed out waiting for post-selection progress")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_BIG_CAPTURE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
