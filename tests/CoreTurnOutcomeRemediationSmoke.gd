extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	for player_count in [3, 4]:
		for scenario in ["human_last_noncompleting_item", "ai_last_noncompleting_item", "consecutive_skips", "all_blocked"]:
			await _run_case(player_count, scenario)
			if _failed:
				return
	print("CORE_TURN_OUTCOME_REMEDIATION_SMOKE_OK")
	quit(0)


func _run_case(player_count: int, scenario: String) -> void:
	var packed := load(SCENE_PATH) as PackedScene
	var board := GameBoardSmokeFactory.create_solo(packed, player_count)
	root.add_child(board)
	if not await _wait_for_actionable(board):
		return
	var result := {}
	if not bool(board.call("DebugStartCoreTurnResolutionProbe", scenario, result)):
		_fail("Probe rejected %s/%d" % [scenario, player_count])
		return
	if not await _wait_for_result(result):
		return
	if String(result.get("error", "")) != "" or bool(result.get("pending_selection", true)):
		_fail("Probe failed %s/%d: %s" % [scenario, player_count, result])
		return
	if scenario == "all_blocked":
		if String(result.get("outcome", "")) != "Draw" or int(result.get("winner", -2)) != -1:
			_fail("All-blocked case did not draw %d: %s" % [player_count, result])
	elif String(result.get("outcome", "")) != "InProgress" \
			or int(result.get("current_turn", -1)) != int(result.get("expected_turn", -2)):
		_fail("Turn resolution mismatch %s/%d: %s" % [scenario, player_count, result])
	if scenario == "ai_last_noncompleting_item" and not bool(result.get("actor_ai_controlled", false)):
		_fail("AI probe actor was not classified as AI")
	board.queue_free()
	for _frame in range(3):
		await process_frame


func _wait_for_actionable(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for actionable board")
	return false


func _wait_for_result(result: Dictionary) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for core turn result")
	return false


func _fail(message: String) -> void:
	_failed = true
	push_error(message)
	print("CORE_TURN_OUTCOME_REMEDIATION_SMOKE_FAIL %s" % message)
	quit(1)
