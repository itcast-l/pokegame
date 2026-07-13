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
		"DebugConfigureAdventureQuestionRewardTimingScenario",
		"DebugConfigureAdventureQuestionRewardTimingFollowUpCapture",
		"DebugGetAdventureQuestionRewardTimingState",
		"DebugGetPlayerScoreValue",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugGetGameLoopPhaseName",
	], "question reward timing"):
		return
	if not await _support.wait_for_actionable(board):
		return
	if not bool(board.call("DebugConfigureAdventureQuestionRewardTimingScenario")):
		_fail("Failed to configure the tenth question reward capture")
		return
	if not _assert_progress(board, 9, 4, false, "before the tenth question reward"):
		return

	var enemy_health_before := int(board.call("DebugGetPlayerScoreValue", 1))
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the tenth question reward capture")
		return
	if not await _support.wait_for_actionable(board, "the tenth question reward capture"):
		return
	var tenth_capture_damage := enemy_health_before \
		- int(board.call("DebugGetPlayerScoreValue", 1))
	if tenth_capture_damage != 7:
		_fail(
			"The tenth question reward capture used new modifiers; expected 7 damage, got %d"
			% tenth_capture_damage
		)
		return
	if not _assert_progress(board, 10, 5, true, "after the tenth question reward"):
		return

	if not bool(board.call("DebugConfigureAdventureQuestionRewardTimingFollowUpCapture")):
		_fail("Failed to configure the follow-up capture")
		return
	if not _assert_progress(board, 10, 5, true, "before the follow-up capture"):
		return

	enemy_health_before = int(board.call("DebugGetPlayerScoreValue", 1))
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the follow-up capture")
		return
	if not await _support.wait_for_actionable(board, "the follow-up capture"):
		return
	var follow_up_damage := enemy_health_before \
		- int(board.call("DebugGetPlayerScoreValue", 1))
	if follow_up_damage != 16:
		_fail(
			"The next capture did not use the new modifiers; expected 16 damage, got %d"
			% follow_up_damage
		)
		return
	if not _assert_progress(board, 10, 5, true, "after the follow-up capture"):
		return

	print("ADVENTURE_UPGRADE_QUESTION_REWARD_TIMING_SMOKE_OK")
	await _support.dispose_audio()
	quit(0)


func _assert_progress(
	board: Node,
	expected_count: int,
	expected_bonus: int,
	expected_final_double: bool,
	label: String
) -> bool:
	var state := board.call("DebugGetAdventureQuestionRewardTimingState") as Dictionary
	if state.is_empty():
		_fail("Missing question reward timing state %s" % label)
		return false
	if int(state.get("question_reward_count", -1)) != expected_count \
		or int(state.get("permanent_capture_damage_bonus", -1)) != expected_bonus \
		or bool(state.get("final_double_capture_damage_active", false)) != expected_final_double:
		_fail("Unexpected question reward timing state %s: %s" % [label, str(state)])
		return false
	return true


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_QUESTION_REWARD_TIMING_SMOKE_FAIL %s" % message)
	quit(1)
