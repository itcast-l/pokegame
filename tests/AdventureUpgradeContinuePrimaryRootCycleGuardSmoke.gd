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
		"DebugSimulateAdventureMainPlayerMiss",
		"DebugHasAdventurePendingExtraBaseCardAction",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugGetTotalHandCardCountForPlayer",
		"DebugGetHandCardCode",
		"DebugGetTableCardCount",
		"DebugGetGameLoopPhaseName",
	], "continue/primary-root cycle guard"):
		return
	if not await _support.wait_for_actionable(board):
		return
	if not bool(board.call("DebugConfigureAdventurePairCaptureReturnScenario")):
		_fail("Failed to configure the primary-root cycle fixture")
		return
	for upgrade_id in ["clubs_continue_branch", "clubs_primary_root_return"]:
		if not bool(board.call("DebugAcquireAdventureUpgrade", upgrade_id)):
			_fail("Failed to inject adventure upgrade %s" % upgrade_id)
			return
	if not bool(board.call("DebugSimulateAdventureMainPlayerMiss", 6)) \
		or not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("The first six-card miss did not grant Continue Branch")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to consume the Continue Branch action with a pair capture")
		return
	var pre_refill_hand_count: int = await _support.wait_for_pre_refill_hand_count_after_table_clear(
		board,
		"Primary Root return before refill"
	)
	if pre_refill_hand_count != 1:
		_fail("Primary Root did not return exactly one played card before refill")
		return
	var first_hand_code := String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, 0))
	if first_hand_code != "Hearts:5":
		_fail("Primary Root returned an unexpected played card: %s" % first_hand_code)
		return
	if not await _support.wait_for_actionable_table_count(board, 0, "primary-root capture settlement"):
		return
	if bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("The consumed Continue Branch grant remained queued after capture")
		return

	if not bool(board.call("DebugSimulateAdventureMainPlayerMiss", 6)):
		_fail("Failed to evaluate the repeated Continue Branch trigger")
		return
	if bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("Continue Branch re-entered after a successful capture in the same real turn")
		return

	print("ADVENTURE_UPGRADE_CONTINUE_PRIMARY_ROOT_CYCLE_GUARD_SMOKE_OK")
	await _support.dispose_audio()
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_CONTINUE_PRIMARY_ROOT_CYCLE_GUARD_SMOKE_FAIL %s" % message)
	quit(1)
