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
		"DebugConfigureAdventureGapRootingScenario",
		"DebugConfigureAdventurePairCaptureReturnScenario",
		"DebugConfigureAdventureTraceHuntMarkerScenario",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugGetTotalHandCardCountForPlayer",
		"DebugGetHandCardCode",
		"DebugGetTableCardCount",
		"DebugGetTableCardCode",
		"DebugIsTableCardFaceUp",
		"DebugGetGameLoopPhaseName",
	], "complete capture context"):
		return
	if not await _support.wait_for_actionable(board):
		return
	if not bool(board.call("DebugConfigureAdventureGapRootingScenario")):
		_fail("Failed to configure the complete-capture extension fixture")
		return
	for upgrade_id in ["clubs_gap_rooting", "clubs_short_branch_return"]:
		if not bool(board.call("DebugAcquireAdventureUpgrade", upgrade_id)):
			_fail("Failed to inject adventure upgrade %s" % upgrade_id)
			return

	if int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX)) != 1 \
		or int(board.call("DebugGetTableCardCount")) != 2:
		_fail("Complete capture fixture did not start from the expected 1/2 state")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the gap-rooting pair capture")
		return
	var first_pre_refill_hand_count: int = await _support.wait_for_pre_refill_hand_count_after_table_clear(
		board,
		"complete capture before refill"
	)
	if first_pre_refill_hand_count != 0:
		_fail(
			"Short Branch returned cards instead of seeing the complete three-card " \
			+ "Gap Rooting context"
		)
		return
	if not await _support.wait_for_actionable_table_count(board, 0, "complete capture settlement"):
		return

	if not bool(board.call("DebugConfigureAdventurePairCaptureReturnScenario")):
		_fail("Failed to configure the follow-up pair capture in the same real turn")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the follow-up pair capture")
		return
	var second_pre_refill_hand_count: int = await _support.wait_for_pre_refill_hand_count_after_table_clear(
		board,
		"follow-up Short Branch return before refill"
	)
	if second_pre_refill_hand_count != 2:
		_fail(
			"Short Branch consumed its once-per-turn flag before seeing the complete " \
			+ "three-card Gap Rooting context (hand=%d)" % second_pre_refill_hand_count
		)
		return
	var returned_hand_codes := [
		String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, 0)),
		String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, 1)),
	]
	if returned_hand_codes != ["Clubs:5", "Hearts:5"]:
		_fail("Short Branch returned an unexpected captured pair: %s" % str(returned_hand_codes))
		return
	if not await _support.wait_for_actionable_table_count(board, 0, "follow-up pair settlement"):
		return
	if not bool(board.call("DebugAcquireAdventureUpgrade", "clubs_hollow_burst")):
		_fail("Failed to inject clubs_hollow_burst")
		return

	for capture_index in range(2):
		if not bool(board.call("DebugConfigureAdventureTraceHuntMarkerScenario")):
			_fail("Failed to configure Hollow Burst capture %d" % (capture_index + 1))
			return
		if int(board.call("DebugGetTableCardCount")) != 3 \
			or bool(board.call("DebugIsTableCardFaceUp", 1)):
			_fail("Hollow Burst fixture did not retain its hidden survivor")
			return
		if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
			_fail("Failed to play standard Hollow Burst capture %d" % (capture_index + 1))
			return
		if not await _support.wait_for_actionable_table_count(
			board,
			2,
			"Hollow Burst capture %d settlement" % (capture_index + 1)
		):
			return
		if String(board.call("DebugGetTableCardCode", 0)) != "Spades:2" \
			or String(board.call("DebugGetTableCardCode", 1)) != "Diamonds:7":
			_fail("Hollow Burst capture removed cards outside the endpoint pair")
			return
		if not bool(board.call("DebugIsTableCardFaceUp", 1)):
			_fail("Hollow Burst did not reveal the remaining hidden card")
			return

	print("ADVENTURE_UPGRADE_COMPLETE_CAPTURE_CONTEXT_SMOKE_OK")
	await _support.dispose_audio()
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_COMPLETE_CAPTURE_CONTEXT_SMOKE_FAIL %s" % message)
	quit(1)
