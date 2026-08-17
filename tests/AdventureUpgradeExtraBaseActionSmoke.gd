extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0

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

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugAcquireAdventureUpgrade") \
		or not board.has_method("DebugSimulateAdventureEnemyMiss") \
		or not board.has_method("DebugHasAdventurePendingExtraBaseCardAction") \
		or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
		or not board.has_method("DebugGetCurrentTurnPlayerIndex") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing extra-base-action debug hooks")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to dismiss opening adventure upgrade selection")
		return

	if not await _wait_for_main_action(board):
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", "spades_intercept_beat")):
		_fail("Failed to inject spades_intercept_beat")
		return

	if not bool(board.call("DebugSimulateAdventureEnemyMiss", 1, false)):
		_fail("Failed to simulate enemy miss for intercept beat")
		return

	if not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("Expected intercept beat to arm an extra base-card action")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to spend the extra base-card action")
		return

	if not await _wait_for_main_action(board):
		return

	if int(board.call("DebugGetCurrentTurnPlayerIndex")) != MAIN_PLAYER_INDEX:
		_fail("Expected current turn to remain on the main player after intercept beat action")
		return

	if bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("Expected extra base-card action to be consumed after one play")
		return

	print("ADVENTURE_UPGRADE_EXTRA_BASE_ACTION_SMOKE_OK")
	quit()


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for adventure upgrade selection")
			return false
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true
	_fail("Timed out waiting for opening adventure upgrade selection")
	return false


func _wait_for_main_action(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for main action")
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for main action")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_EXTRA_BASE_ACTION_SMOKE_FAIL %s" % message)
	quit(1)
