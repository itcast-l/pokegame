extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 25.0

var _failed := false

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	if not await _run_dragon_reference_abort_and_reward():
		return
	var board := await _create_board("shrimp")
	if board == null or not await _wait_for_actionable(board):
		return
	if not await _run_normal_pre_apply_reference_abort(board):
		return
	if not await _run_normal_post_apply_restart_abort(board):
		return
	if not await _run_collect_table_reference_abort(board):
		return
	await _dispose_board(board)
	_assert_required_source_guards()
	if _failed:
		return
	print("GAME_BOARD_CAPTURE_GENERATION_OWNERSHIP_SMOKE_OK")
	quit()

func _run_normal_pre_apply_reference_abort(board: Node) -> bool:
	if not bool(board.call("DebugConfigureAdventureCrownCanopyScenario")) or not bool(board.call("DebugArmCaptureBeforeApplyBarrier")):
		_fail("Failed to prepare normal pre-apply ownership probe")
		return false
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")) or not await _wait_for_barrier(board, "DebugHasEnteredCaptureBeforeApplyBarrier"):
		return false
	var before := _state_fingerprint(board)
	if not bool(board.call("DebugReplaceTableCardReference", 0)) or not bool(board.call("DebugReleaseCaptureBeforeApplyBarrier")):
		_fail("Failed to invalidate and release normal pre-apply capture")
		return false
	if not await _wait_for_actionable(board):
		return false
	if _state_fingerprint(board) != before:
		_fail("Pre-apply reference abort mutated score, deck, table, or hands")
		return false
	return true

func _run_normal_post_apply_restart_abort(board: Node) -> bool:
	if not bool(board.call("DebugConfigureAdventureCrownCanopyScenario")) or not bool(board.call("DebugArmCaptureAfterApplyBarrier")):
		_fail("Failed to prepare normal post-apply ownership probe")
		return false
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")) or not await _wait_for_barrier(board, "DebugHasEnteredCaptureAfterApplyBarrier"):
		return false
	if not bool(board.call("DebugRestartMatchForCaptureOwnershipProbe")) or not bool(board.call("DebugReleaseCaptureAfterApplyBarrier")):
		_fail("Failed to restart and release normal post-apply capture")
		return false
	if not await _wait_for_observed_result(board):
		return false
	if not bool(board.call("DebugGetObservedCaptureOwnershipApplied")) or not bool(board.call("DebugGetObservedCaptureOwnershipAborted")):
		_fail("Post-apply restart was not observed as applied-and-aborted")
		return false
	return true

func _run_collect_table_reference_abort(board: Node) -> bool:
	if not bool(board.call("DebugConfigureAdventureCrownCanopyScenario")) or not bool(board.call("DebugArmCaptureBeforeApplyBarrier")):
		_fail("Failed to prepare collect_table ownership probe")
		return false
	var result := {}
	if not bool(board.call("DebugStartCollectTableCaptureOwnershipProbe", result)) or not await _wait_for_barrier(board, "DebugHasEnteredCaptureBeforeApplyBarrier"):
		return false
	board.call("DebugReplaceTableCardReference", 0)
	board.call("DebugReleaseCaptureBeforeApplyBarrier")
	if not await _wait_for_result(result):
		return false
	if String(result.get("error", "")) != "" or bool(result.get("played", true)):
		_fail("collect_table continued after reference ownership loss: %s" % str(result))
		return false
	return true

func _run_dragon_reference_abort_and_reward() -> bool:
	var board := await _create_board("dragon_prince_j")
	if board == null or not await _wait_for_actionable(board):
		return false
	var dragon_configured := bool(board.call("DebugConfigureDragonQuestionCaptureOwnershipProbe"))
	var dragon_barrier_armed := bool(board.call("DebugArmCaptureBeforeApplyBarrier"))
	if not dragon_configured or not dragon_barrier_armed:
		_fail("Failed to prepare Dragon ownership probe")
		return false
	var abort_result := {}
	var dragon_started := bool(board.call("DebugStartDragonQuestionCaptureOwnershipProbe", abort_result))
	if not dragon_started or not await _wait_for_barrier(board, "DebugHasEnteredCaptureBeforeApplyBarrier"):
		return false
	board.call("DebugReplaceTableCardReference", 0)
	board.call("DebugReleaseCaptureBeforeApplyBarrier")
	if not await _wait_for_result(abort_result):
		return false
	if String(abort_result.get("error", "")) != "" or bool(abort_result.get("played", true)):
		_fail("Dragon outer capture continued after ownership loss: %s" % str(abort_result))
		return false
	if not bool(board.call("DebugConfigureDragonQuestionCaptureOwnershipProbe")) or not bool(board.call("DebugForceNextQuestionRewardItemCardId", "magnet")):
		_fail("Failed to prepare Dragon reward probe")
		return false
	var reward_result := {}
	if not bool(board.call("DebugStartDragonQuestionCaptureOwnershipProbe", reward_result)) or not await _wait_for_result(reward_result):
		return false
	if String(reward_result.get("error", "")) != "" \
		or not bool(reward_result.get("played", false)) \
		or not bool(reward_result.get("captured", false)) \
		or not bool(reward_result.get("completes_turn", false)) \
		or String(reward_result.get("reward_item_id", "")) != "magnet" \
		or int(reward_result.get("pending_attachment_count", -1)) != 0 \
		or int(reward_result.get("enemy_magnet_attachment_count", 0)) != 1:
		_fail("Dragon did not consume its question reward: %s" % str(reward_result))
		return false
	await _dispose_board(board)
	return true

func _dispose_board(board: Node) -> void:
	if is_instance_valid(board):
		board.queue_free()
	await process_frame
	await process_frame

func _create_board(enemy_avatar_id: String) -> Node:
	await process_frame
	var packed := load(SCENE_PATH) as PackedScene
	if packed == null:
		_fail("Failed to load game board scene")
		return null
	var board := packed.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", enemy_avatar_id)
	root.add_child(board)
	await process_frame
	for method in ["DebugArmCaptureBeforeApplyBarrier", "DebugArmCaptureAfterApplyBarrier", "DebugRestartMatchForCaptureOwnershipProbe", "DebugReplaceTableCardReference", "DebugConfigureDragonQuestionCaptureOwnershipProbe", "DebugStartCollectTableCaptureOwnershipProbe"]:
		if not board.has_method(method):
			_fail("Missing capture ownership hook: %s" % method)
			return null
	return board

func _state_fingerprint(board: Node) -> String:
	var parts := [str(board.call("DebugGetPlayerScoreValue", 0)), str(board.call("DebugGetPlayerScoreValue", 1)), str(board.call("DebugGetTableCardCount"))]
	for player in [0, 1]:
		parts.append(str(board.call("DebugGetDeckCardCountForPlayer", player)))
		parts.append(str(board.call("DebugGetTotalHandCardCountForPlayer", player)))
	return "|".join(parts)

func _wait_for_actionable(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for actionable state")
	return false

func _wait_for_barrier(board: Node, method: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(board.call(method)):
			return true
	_fail("Timed out waiting for capture barrier %s" % method)
	return false

func _wait_for_observed_result(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(board.call("DebugWasCaptureOwnershipResultObserved")):
			return true
	_fail("Timed out waiting for post-apply ownership result")
	return false

func _wait_for_result(result: Dictionary) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for probe result")
	return false

func _assert_required_source_guards() -> void:
	var flow := FileAccess.get_file_as_string("res://scripts/GameBoardScene.TableCaptureFlow.cs")
	var card_play := FileAccess.get_file_as_string("res://scripts/GameBoardScene.CardPlay.cs")
	var coordinator := FileAccess.get_file_as_string("res://scripts/GameBoardScene.CaptureResolutionCoordinator.cs")
	var dragon := FileAccess.get_file_as_string("res://scripts/GameBoardScene.DragonPrinceTableAbility.cs")
	var effects := FileAccess.get_file_as_string("res://scripts/GameBoardScene.EffectResolver.cs")
	var commit_apply := "commitImmediatelyBeforeApply?.Invoke();\n\tCaptureStateResult captureStateResult =\n\t\t_captureStateService.ApplyCapture"
	if not card_play.contains("ResolvePlayedCardCaptureAsync(") \
		or coordinator.count("expectedTableCardReferences:") < 1 \
		or dragon.count("expectedTableCardReferences:") < 1 \
		or effects.count("expectedTableCardReferences:") < 1 \
		or not flow.contains(commit_apply) \
		or not coordinator.contains("captureResult.Aborted") \
		or not dragon.contains("captureResult.Aborted") \
		or not effects.contains("captureResult.Aborted"):
		_fail("Required capture ownership caller guards are missing")

func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("GAME_BOARD_CAPTURE_GENERATION_OWNERSHIP_SMOKE_FAIL %s" % message)
	quit(1)
