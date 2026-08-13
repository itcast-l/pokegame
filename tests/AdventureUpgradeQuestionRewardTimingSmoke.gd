extends SceneTree

const Support := preload("res://tests/AdventureUpgradeInteractionSmokeSupport.gd")
const HAND_AREA_PATH := "BoardCanvas/LayoutAreas/Player1HandArea"
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
		"DebugConfigureAdventureDoubleQuestionRewardCapacityScenario",
		"DebugAcquireAdventureUpgrade",
		"DebugGetAdventureQuestionRewardTimingState",
		"DebugGetPlayerScoreValue",
		"DebugGetQuestionRewardGrantCount",
		"DebugGetLastQuestionRewardItemCardId",
		"DebugGetTotalHandCardCountForPlayer",
		"DebugGetTotalItemHandCardCountForPlayer",
		"DebugGetPendingItemSelectionModeName",
		"DebugCompletePendingHandLimitDiscard",
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
	if not bool(board.call("DebugAcquireAdventureUpgrade", "clubs_primary_root_return")):
		_fail("Failed to enable the base-card return index-shift boundary")
		return

	var enemy_health_before := int(board.call("DebugGetPlayerScoreValue", 1))
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the tenth question reward capture")
		return
	if not await _wait_for_question_reward_delivery(
		board,
		"the tenth question reward capture",
		true
	):
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

	var grants_before := int(board.call("DebugGetQuestionRewardGrantCount"))
	if not bool(board.call("DebugConfigureAdventureDoubleQuestionRewardCapacityScenario")):
		_fail("Failed to configure the double question reward capacity scenario")
		return
	if int(board.call("DebugGetTotalHandCardCountForPlayer", 0)) != 8:
		_fail("Double question reward scenario did not begin with a full hand")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the double question reward capture")
		return
	if not await _wait_for_hand_limit_discard(board):
		return
	if not bool(board.call("DebugCompletePendingHandLimitDiscard", 0)):
		_fail("Failed to complete the planned double-reward hand discard")
		return
	if not await _support.wait_for_actionable(board, "the double question reward capture"):
		return
	var final_hand_count := int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
	if final_hand_count > 8:
		_fail("Double question rewards exceeded the hand limit: %d" % final_hand_count)
		return
	if int(board.call("DebugGetQuestionRewardGrantCount")) != grants_before + 2:
		_fail("Double question reward capture did not grant exactly two planned rewards")
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


func _wait_for_hand_limit_discard(board: Node) -> bool:
	for _frame_index in range(3600):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board disappeared while planning the double-reward discard")
			return false
		if String(board.call("DebugGetPendingItemSelectionModeName")) == "HandLimitDiscard":
			return true
	_fail("Timed out waiting for the double-reward hand discard plan")
	return false


func _wait_for_question_reward_delivery(
	board: Node,
	label: String,
	expect_base_hand_index_shift: bool = false
) -> bool:
	var saw_logical_reward := false
	var saw_transform := false
	var pre_transform_base_hand_counts := {}
	for _frame_index in range(3600):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board disappeared while observing %s" % label)
			return false
		var item_count := int(board.call("DebugGetTotalItemHandCardCountForPlayer", 0))
		if item_count <= 0:
			continue
		saw_logical_reward = true
		var transform := board.get_node_or_null("QuestionCardRewardTransform") as Control
		if transform != null:
			saw_transform = true
		elif not saw_transform:
			var total_count := int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
			pre_transform_base_hand_counts[total_count - item_count] = true
		if not saw_transform or transform != null:
			var early_slot := _get_first_item_card_slot(board)
			if early_slot != null and early_slot.visible:
				_fail("Question reward appeared in hand before transforming during %s" % label)
				return false
		elif String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			if expect_base_hand_index_shift and pre_transform_base_hand_counts.size() < 2:
				_fail(
					"Base-hand count did not move ahead of the question reward during %s: %s"
					% [label, str(pre_transform_base_hand_counts.keys())]
				)
				return false
			var delivered_slot := _get_first_item_card_slot(board)
			if delivered_slot == null or not delivered_slot.visible:
				_fail("Question reward stayed hidden after returning to hand during %s" % label)
				return false
			return true
	_fail("Timed out observing %s (reward=%s logical=%s transform=%s)" % [
		label,
		String(board.call("DebugGetLastQuestionRewardItemCardId")),
		str(saw_logical_reward),
		str(saw_transform),
	])
	return false


func _get_first_item_card_slot(board: Node) -> Control:
	var item_count := int(board.call("DebugGetTotalItemHandCardCountForPlayer", 0))
	var total_count := int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
	if item_count <= 0 or total_count < item_count:
		return null
	return board.get_node_or_null(
		"%s/HandCard_%d" % [HAND_AREA_PATH, total_count - item_count]) as Control


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_QUESTION_REWARD_TIMING_SMOKE_FAIL %s" % message)
	quit(1)
