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
		_fail("Failed to load game board scene")
		return

	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "shrimp")
	root.add_child(board)
	await process_frame

	if not await _wait_for_upgrade_selection(board):
		return
	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to dismiss opening upgrade selection")
		return
	if not await _wait_for_main_action(board):
		return
	if not bool(board.call("DebugAcquireAdventureUpgrade", "spades_black_ledger")):
		_fail("Failed to acquire black ledger")
		return
	if not bool(board.call("DebugConfigureBlackLedgerCaptureProbe")):
		_fail("Failed to configure black ledger capture probe")
		return

	var result := {}
	if not bool(board.call("DebugStartBlackLedgerCaptureProbe", result)):
		_fail("Failed to start black ledger capture probe")
		return
	if not await _wait_for_result_and_observe_revealed_bomb(board, result):
		return
	if not String(result.get("error", "")).is_empty():
		_fail("Black ledger probe error: %s" % result["error"])
		return
	if not bool(result.get("played", false)) or not bool(result.get("captured", false)):
		_fail("Expected enemy play to capture")
		return
	if int(result.get("actual_damage", -1)) != 0:
		_fail("Triggered bomb should redirect the capture score to its owner: %s" % [result])
		return
	if int(result.get("main_deck_count", 0)) != 1:
		_fail("Expected exactly one redirected card in main deck: %s" % [result])
		return
	if not bool(result.get("main_has_ten", false)) or bool(result.get("enemy_has_ten", true)):
		_fail("Expected highest-value middle card only in main deck: %s" % [result])
		return

	print("ADVENTURE_UPGRADE_BLACK_LEDGER_SMOKE_OK")
	quit()


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true
	_fail("Timed out waiting for opening upgrade selection")
	return false


func _wait_for_main_action(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for main action")
	return false


func _wait_for_result_and_observe_revealed_bomb(board: Node, result: Dictionary) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	var saw_revealed_bomb := false
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed while waiting for the black ledger capture")
			return false
		if int(board.call("DebugGetTableCardCount")) > 2 \
				and bool(board.call("DebugIsTableCardFaceUp", 2)):
			saw_revealed_bomb = true
		if bool(result.get("completed", false)):
			if not saw_revealed_bomb:
				_fail("Enemy capture did not persist the triggered bomb's face-up state")
				return false
			return true
	_fail("Timed out waiting for black ledger probe")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_BLACK_LEDGER_SMOKE_FAIL %s" % message)
	quit(1)
