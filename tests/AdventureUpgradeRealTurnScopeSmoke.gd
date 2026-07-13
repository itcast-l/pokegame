extends SceneTree

const Support := preload("res://tests/AdventureUpgradeInteractionSmokeSupport.gd")
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
		"DebugConfigureAdventureGapRootingScenario",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugGetTableCardCode",
		"DebugGetTableCardCount",
		"DebugGetGameLoopPhaseName",
	], "real-turn scope"):
		return
	if not await _support.wait_for_actionable(board):
		return
	if not bool(board.call("DebugConfigureAdventureGapRootingScenario")) \
		or not bool(board.call("DebugAcquireAdventureUpgrade", "clubs_gap_rooting")):
		_fail("Failed to configure the first real-turn Gap Rooting capture")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the first Gap Rooting capture")
		return
	if not await _support.wait_for_actionable_table_count(board, 0, "first real-turn capture"):
		return

	if not bool(board.call("DebugConfigureAdventureGapRootingScenario")):
		_fail("Failed to configure the second capture without crossing a turn boundary")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the second Gap Rooting capture")
		return
	if not await _support.wait_for_actionable_table_count(board, 1, "second real-turn capture"):
		return

	if String(board.call("DebugGetTableCardCode", 0)) != "Clubs:2":
		_fail("The second capture left an unexpected card instead of the used Gap Rooting target")
		return

	print("ADVENTURE_UPGRADE_REAL_TURN_SCOPE_SMOKE_OK")
	await _support.dispose_audio()
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_REAL_TURN_SCOPE_SMOKE_FAIL %s" % message)
	quit(1)
