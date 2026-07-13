extends SceneTree

const Support := preload("res://tests/AdventureUpgradeInteractionSmokeSupport.gd")
const MAIN_PLAYER_INDEX := 0

var _failed := false
var _support: RefCounted


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_support = Support.new(self, Callable(self, "_fail"))
	var board: Node = await _support.create_board()
	if _failed or board == null:
		return
	if not _support.require_methods(board, [
		"DebugAcquireAdventureUpgrade",
		"DebugConfigureAdventurePairCaptureReturnScenario",
		"DebugSimulateAdventureEnemyMiss",
		"DebugSimulateAdventureMainPlayerMiss",
		"DebugHasAdventurePendingExtraBaseCardAction",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugGetCurrentTurnPlayerIndex",
		"DebugGetTableCardCount",
		"DebugGetGameLoopPhaseName",
	], "intercept/continue chain"):
		return
	if not await _support.wait_for_actionable(board):
		return
	if not bool(board.call("DebugConfigureAdventurePairCaptureReturnScenario")):
		_fail("Failed to configure the intercept/continue fixture")
		return
	for upgrade_id in ["spades_intercept_beat", "clubs_continue_branch"]:
		if not bool(board.call("DebugAcquireAdventureUpgrade", upgrade_id)):
			_fail("Failed to inject adventure upgrade %s" % upgrade_id)
			return

	if not bool(board.call("DebugSimulateAdventureEnemyMiss", 6, false)) \
		or not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("Intercept Beat did not create its promised extra action")
		return
	if not bool(board.call("DebugSimulateAdventureMainPlayerMiss", 6)):
		_fail("Failed to derive Continue Branch inside the intercept root chain")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to consume the Intercept Beat grant")
		return
	if not await _support.wait_for_actionable_table_count(board, 0, "intercept action settlement"):
		return
	if not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("Continue Branch was not queued behind Intercept Beat")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to consume the derived Continue Branch grant")
		return
	if not await _support.wait_for_actionable_pending_state(board, false, "normal turn resumption"):
		return
	if int(board.call("DebugGetCurrentTurnPlayerIndex")) != MAIN_PLAYER_INDEX:
		_fail("The intercept root chain ended without restoring the promised normal main turn")
		return
	if int(board.call("DebugGetTableCardCount")) != 1:
		_fail("The enemy acted before the promised normal main turn was restored")
		return

	print("ADVENTURE_UPGRADE_INTERCEPT_CONTINUE_SMOKE_OK")
	await _support.dispose_audio()
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_INTERCEPT_CONTINUE_SMOKE_FAIL %s" % message)
	quit(1)
