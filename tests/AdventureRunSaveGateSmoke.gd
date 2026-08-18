extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const TIMEOUT_SECONDS := 25.0
const EXHAUSTED_SAVE_ATTEMPTS := 4

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if OS.get_cmdline_user_args().has("opening-only"):
		if not await _test_opening_stage_alignment_exhaustion():
			return
		print("ADVENTURE_RUN_SAVE_GATE_OPENING_SMOKE_OK")
		await _dispose_audio_before_quit()
		quit(0)
		return

	if not await _test_capture_retry_success():
		return
	if not await _test_direct_damage_exhaustion():
		return
	if not await _test_real_turn_end_exhaustion():
		return
	print("ADVENTURE_RUN_SAVE_GATE_CORE_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _test_capture_retry_success() -> bool:
	var board := await _create_actionable_board("shrimp")
	if _failed or board == null:
		return false
	if not await _test_routine_save_stays_unobtrusive(board):
		return false
	if not bool(board.call("DebugConfigureAdventureQuestionRewardTimingScenario")):
		_fail("Failed to configure the real capture save-gate scenario")
		return false

	var table_before := int(board.call("DebugGetTableCardCount"))
	var hand_before := int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
	var player_health_before := int(board.call("DebugGetPlayerScoreValue", 0))
	var enemy_health_before := int(board.call("DebugGetPlayerScoreValue", 1))
	var turn_before := int(board.call("DebugGetCurrentTurnPlayerIndex"))
	if not bool(board.call("DebugConfigureAdventurePersistFailures", 1)) \
			or not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to start the real capture retry-success probe")
		return false
	if not await _wait_for_persist_attempt(board, 1, "capture first failure"):
		return false
	if not _assert_gate_locked(board, "capture retry"):
		return false
	if not await _assert_retry_status_has_no_result_actions(board):
		return false
	if int(board.call("DebugGetTableCardCount")) != table_before \
			or int(board.call("DebugGetTotalHandCardCountForPlayer", 0)) != hand_before \
			or int(board.call("DebugGetPlayerScoreValue", 0)) != player_health_before \
			or int(board.call("DebugGetPlayerScoreValue", 1)) != enemy_health_before \
			or int(board.call("DebugGetCurrentTurnPlayerIndex")) != turn_before:
		_fail(
			"Capture mutated before persistence succeeded: table %d/%d hand %d/%d player %d/%d enemy %d/%d turn %d/%d attempts=%d"
			% [
				int(board.call("DebugGetTableCardCount")),
				table_before,
				int(board.call("DebugGetTotalHandCardCountForPlayer", 0)),
				hand_before,
				int(board.call("DebugGetPlayerScoreValue", 0)),
				player_health_before,
				int(board.call("DebugGetPlayerScoreValue", 1)),
				enemy_health_before,
				int(board.call("DebugGetCurrentTurnPlayerIndex")),
				turn_before,
				int(board.call("DebugGetAdventurePersistAttemptCount")),
			]
		)
		return false

	if not await _wait_for_actionable(board, "capture retry success"):
		return false
	if int(board.call("DebugGetAdventurePersistAttemptCount")) != 2:
		_fail("Capture did not retry exactly once before succeeding")
		return false
	if int(board.call("DebugGetTableCardCount")) >= table_before \
			or int(board.call("DebugGetPlayerScoreValue", 1)) >= enemy_health_before:
		_fail("Committed capture did not apply table and enemy-health effects")
		return false
	if not bool(board.call("DebugCanMainPlayerAct")):
		_fail("Input did not resume after the real capture retry succeeded")
		return false
	var committed_state := board.call("DebugGetAdventureSaveConsistencyState") as Dictionary
	if not _assert_runtime_matches_disk(committed_state, "capture retry commit"):
		return false
	if int(committed_state.get("runtime_question_reward_count", -1)) != 10 \
			or int(committed_state.get("runtime_permanent_capture_damage_bonus", -1)) != 5 \
			or not bool(committed_state.get("runtime_final_double_active", false)):
		_fail("Capture retry committed the wrong question-reward effect state")
		return false
	var revision_after_capture := int(committed_state.get("runtime_revision", -1))
	var question_rewards_after_capture := int(
		committed_state.get("runtime_question_reward_count", -1)
	)
	var permanent_bonus_after_capture := int(
		committed_state.get("runtime_permanent_capture_damage_bonus", -1)
	)
	var half_rescue_after_capture := bool(
		committed_state.get("runtime_half_health_rescue_used", false)
	)
	var quarter_rescue_after_capture := bool(
		committed_state.get("runtime_quarter_health_rescue_used", false)
	)
	if not bool(board.call("DebugStartAdventureDirectDamageSaveGateProbe", 1)):
		_fail("Failed to start the post-capture persistence regression probe")
		return false
	if not await _wait_for_persist_attempt(board, 3, "post-capture persistence"):
		return false
	if not await _wait_for_actionable(board, "post-capture persistence"):
		return false
	var follow_up_state := board.call("DebugGetAdventureSaveConsistencyState") as Dictionary
	if not _assert_runtime_matches_disk(follow_up_state, "post-capture persistence"):
		return false
	if int(follow_up_state.get("runtime_revision", -1)) <= revision_after_capture \
			or int(follow_up_state.get("runtime_question_reward_count", -1)) \
				!= question_rewards_after_capture \
			or int(follow_up_state.get("runtime_permanent_capture_damage_bonus", -1)) \
				!= permanent_bonus_after_capture \
			or bool(follow_up_state.get("runtime_half_health_rescue_used", false)) \
				!= half_rescue_after_capture \
			or bool(follow_up_state.get("runtime_quarter_health_rescue_used", false)) \
				!= quarter_rescue_after_capture:
		_fail("A later adventure write regressed the committed capture candidate")
		return false
	await _dispose_board(board)
	return true


func _test_direct_damage_exhaustion() -> bool:
	var board := await _create_actionable_board("shrimp")
	if _failed or board == null:
		return false
	var health_before := int(board.call("DebugGetPlayerScoreValue", 0))
	var table_before := int(board.call("DebugGetTableCardCount"))
	if not bool(board.call(
			"DebugConfigureAdventurePersistFailures",
			EXHAUSTED_SAVE_ATTEMPTS
	)) or not bool(board.call("DebugStartAdventureDirectDamageSaveGateProbe", 17)):
		_fail("Failed to start the real direct-damage exhaustion probe")
		return false
	if not await _wait_for_persist_attempt(board, 1, "direct damage first failure"):
		return false
	if not _assert_gate_locked(board, "direct damage retry"):
		return false
	if int(board.call("DebugGetPlayerScoreValue", 0)) != health_before \
			or int(board.call("DebugGetTableCardCount")) != table_before \
			or bool(board.call("DebugWasAdventureDirectDamageSaveGateProbeApplied")):
		_fail("Direct damage applied before persistence succeeded")
		return false
	if not await _wait_for_exhaustion_before_menu(
		board,
		"direct damage",
		Callable(self, "_assert_direct_damage_unchanged").bind(health_before, table_before)
	):
		return false
	return await _wait_for_adventure_menu("direct damage exhaustion")


func _test_real_turn_end_exhaustion() -> bool:
	var board := await _create_actionable_board("shrimp")
	if _failed or board == null:
		return false
	var turn_before := int(board.call("DebugGetCurrentTurnPlayerIndex"))
	if turn_before != 0:
		_fail("Real-turn-end probe did not begin on the main player")
		return false
	if not bool(board.call(
			"DebugConfigureAdventurePersistFailures",
			EXHAUSTED_SAVE_ATTEMPTS
	)) or not bool(board.call("DebugStartAdventureRealTurnEndSaveGateProbe")):
		_fail("Failed to start the real turn-end exhaustion probe")
		return false
	if not await _wait_for_persist_attempt(board, 1, "real turn end first failure"):
		return false
	if not _assert_gate_locked(board, "real turn end retry"):
		return false
	if int(board.call("DebugGetCurrentTurnPlayerIndex")) != turn_before:
		_fail("Current player changed before real-turn-end persistence succeeded")
		return false
	if not await _wait_for_exhaustion_before_menu(
		board,
		"real turn end",
		Callable(self, "_assert_current_turn_unchanged").bind(turn_before)
	):
		return false
	return await _wait_for_adventure_menu("real turn end exhaustion")


func _test_opening_stage_alignment_exhaustion() -> bool:
	var seed_board := await _create_actionable_board("shrimp")
	if _failed or seed_board == null:
		return false
	if not bool(seed_board.call("DebugResetAdventureRunToFirstStage")) \
			or not bool(seed_board.call("DebugConfigureAdventureNextStageWriteAsInvalid")):
		_fail("Failed to arm the invalid opening stage-alignment write")
		return false
	if int(seed_board.call("DebugGetAdventureRunMainStageIndex")) != 1 \
			or not bool(seed_board.call("DebugHasAdventureNextStageWriteOverride")):
		_fail("Opening stage-alignment precondition was not retained on the seed board")
		return false
	await _dispose_board(seed_board)

	var board := await _create_board("crab")
	if _failed or board == null:
		return false
	if not await _wait_for_nonretryable_opening_abort(board):
		return false
	if not _assert_opening_deal_not_applied(board):
		return false
	return await _wait_for_adventure_menu("opening alignment exhaustion")


func _create_actionable_board(enemy_avatar_id: String) -> Node:
	var board := await _create_board(enemy_avatar_id)
	if _failed or board == null:
		return null
	if not await _wait_for_actionable(board, "opening %s board" % enemy_avatar_id):
		return null
	return board


func _create_board(enemy_avatar_id: String) -> Node:
	await _dispose_current_scene()
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return null
	var board := GameBoardSmokeFactory.create_adventure(packed_scene, enemy_avatar_id)
	root.add_child(board)
	current_scene = board
	await process_frame
	for method in [
		"DebugConfigureAdventurePersistFailures",
		"DebugConfigureAdventureNextStageWriteAsInvalid",
		"DebugResetAdventureRunToFirstStage",
		"DebugGetAdventureRunMainStageIndex",
		"DebugHasAdventureNextStageWriteOverride",
		"DebugGetAdventurePersistAttemptCount",
		"DebugIsAdventureSaveInProgress",
		"DebugCanMainPlayerAct",
		"DebugGetAdventureSaveStatusKey",
		"DebugStartAdventureSavePresentationProbe",
		"DebugIsAdventureSavePresentationProbeCompleted",
		"DebugGetGameLoopPhaseName",
		"DebugGetCurrentTurnPlayerIndex",
		"DebugGetTableCardCount",
		"DebugGetTotalHandCardCountForPlayer",
		"DebugGetPlayerScoreValue",
		"DebugIsAdventureUpgradeSelectionVisible",
		"DebugSelectAdventureUpgradeOption",
		"DebugConfigureAdventureQuestionRewardTimingScenario",
		"DebugGetAdventureSaveConsistencyState",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugStartAdventureDirectDamageSaveGateProbe",
		"DebugWasAdventureDirectDamageSaveGateProbeApplied",
		"DebugStartAdventureRealTurnEndSaveGateProbe",
	]:
		if not board.has_method(method):
			_fail("Missing save gate debug hook: %s" % method)
			return null
	return board


func _test_routine_save_stays_unobtrusive(board: Node) -> bool:
	var result_banner := board.get_node_or_null("WinnerBannerRoot") as Control
	if result_banner == null:
		_fail("Routine-save presentation probe could not find the result banner")
		return false
	if not bool(board.call("DebugStartAdventureSavePresentationProbe")):
		_fail("Failed to start the routine-save presentation probe")
		return false
	if String(board.call("DebugGetAdventureSaveStatusKey")) != "GAME_ADVENTURE_SAVING" \
			or not bool(board.call("DebugIsAdventureSaveInProgress")):
		_fail("Routine-save presentation probe did not enter the saving state")
		return false
	if result_banner.visible:
		_fail("Routine adventure save flashed the result banner")
		return false

	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(board.call("DebugIsAdventureSavePresentationProbeCompleted")):
			if result_banner.visible:
				_fail("Routine-save result banner remained visible after success")
				return false
			return true
	_fail("Routine-save presentation probe did not complete")
	return false


func _assert_retry_status_has_no_result_actions(board: Node) -> bool:
	var result_banner := board.get_node_or_null("WinnerBannerRoot") as Control
	var action_row := board.get_node_or_null(
		"WinnerBannerRoot/WinnerCenter/WinnerBannerPanel/WinnerButtonRow"
	) as Control
	if result_banner == null or action_row == null:
		_fail("Retry presentation could not find the result banner action row")
		return false

	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if String(board.call("DebugGetAdventureSaveStatusKey")) \
				== "GAME_ADVENTURE_SAVE_RETRYING":
			if not result_banner.visible:
				_fail("Adventure save retry did not show its status banner")
				return false
			if action_row.visible or action_row.is_visible_in_tree():
				_fail("Adventure save retry exposed result actions")
				return false
			return true
	_fail("Adventure save retry status was not presented")
	return false


func _wait_for_actionable(board: Node, label: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board disappeared while waiting for %s" % label)
			return false
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
				_fail("Failed to resolve run-start upgrade while waiting for %s" % label)
				return false
		elif String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for %s" % label)
	return false


func _wait_for_persist_attempt(board: Node, expected_attempts: int, label: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board disappeared before %s" % label)
			return false
		if int(board.call("DebugGetAdventurePersistAttemptCount")) >= expected_attempts:
			return true
	_fail("Timed out waiting for %s" % label)
	return false


func _wait_for_exhaustion_before_menu(
	board: Node,
	label: String,
	assert_unchanged: Callable
) -> bool:
	if not await _wait_for_persist_attempt(board, EXHAUSTED_SAVE_ATTEMPTS, "%s exhaustion" % label):
		return false
	if not is_instance_valid(board):
		_fail("Board disappeared before %s exhaustion could be inspected" % label)
		return false
	if not bool(assert_unchanged.call(board)):
		return false
	if bool(board.call("DebugCanMainPlayerAct")):
		_fail("Input resumed after %s exhausted all retries" % label)
		return false
	return true


func _assert_runtime_matches_disk(state: Dictionary, label: String) -> bool:
	if not bool(state.get("disk_valid", false)):
		_fail("%s did not leave a readable adventure snapshot on disk" % label)
		return false
	for field in [
		"question_reward_count",
		"permanent_capture_damage_bonus",
		"current_health",
		"max_health",
	]:
		if int(state.get("runtime_%s" % field, -1)) \
				!= int(state.get("disk_%s" % field, -2)):
			_fail("%s diverged between runtime and disk for %s" % [label, field])
			return false
	return true


func _wait_for_nonretryable_opening_abort(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	var last_status := ""
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Opening board disappeared before its invalid-stage abort was inspected")
			return false
		var status := String(board.call("DebugGetAdventureSaveStatusKey"))
		last_status = status
		if status == "GAME_ADVENTURE_SAVE_FAILED_REPLAY" \
				or status == "GAME_ADVENTURE_RETURNING_TO_MENU":
			if bool(board.call("DebugCanMainPlayerAct")):
				_fail("Input resumed after the invalid opening stage alignment")
				return false
			return _assert_opening_deal_not_applied(board)
	_fail(
		"Invalid opening stage alignment did not enter the abort state (status=%s stage=%d override=%s table=%d hand=%d)"
		% [
			last_status,
			int(board.call("DebugGetAdventureRunMainStageIndex")),
			str(bool(board.call("DebugHasAdventureNextStageWriteOverride"))),
			int(board.call("DebugGetTableCardCount")),
			int(board.call("DebugGetTotalHandCardCountForPlayer", 0)),
		]
	)
	return false


func _assert_gate_locked(board: Node, label: String) -> bool:
	if not bool(board.call("DebugIsAdventureSaveInProgress")):
		_fail("Save gate was not active during %s" % label)
		return false
	if bool(board.call("DebugCanMainPlayerAct")):
		_fail("Main-player input stayed enabled during %s" % label)
		return false
	return true


func _assert_direct_damage_unchanged(board: Node, health_before: int, table_before: int) -> bool:
	if int(board.call("DebugGetPlayerScoreValue", 0)) != health_before \
			or int(board.call("DebugGetTableCardCount")) != table_before \
			or bool(board.call("DebugWasAdventureDirectDamageSaveGateProbeApplied")):
		_fail("Direct damage changed health or table after retry exhaustion")
		return false
	return true


func _assert_current_turn_unchanged(board: Node, turn_before: int) -> bool:
	if int(board.call("DebugGetCurrentTurnPlayerIndex")) != turn_before:
		_fail("Current player changed after real-turn-end retry exhaustion")
		return false
	return true


func _assert_opening_deal_not_applied(board: Node) -> bool:
	if int(board.call("DebugGetTableCardCount")) != 0 \
			or int(board.call("DebugGetTotalHandCardCountForPlayer", 0)) != 0 \
			or int(board.call("DebugGetTotalHandCardCountForPlayer", 1)) != 0:
		_fail("Opening cards were dealt before stage alignment persisted")
		return false
	return true


func _wait_for_adventure_menu(label: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	var last_scene_path := "<null>"
	var last_scene_name := "<null>"
	var last_save_status := ""
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		var scene := current_scene
		if scene != null and is_instance_valid(scene):
			last_scene_path = scene.scene_file_path
			last_scene_name = String(scene.name)
			if scene.has_method("DebugGetAdventureSaveStatusKey"):
				last_save_status = String(scene.call("DebugGetAdventureSaveStatusKey"))
			if scene.scene_file_path == MENU_SCENE_PATH:
				return true
	_fail(
		"%s did not return to the adventure menu (scene=%s path=%s save_status=%s)"
		% [label, last_scene_name, last_scene_path, last_save_status]
	)
	return false


func _dispose_board(board: Node) -> void:
	if is_instance_valid(board):
		if current_scene == board:
			current_scene = null
		board.queue_free()
	await process_frame
	await process_frame


func _dispose_current_scene() -> void:
	var scene := current_scene
	if scene != null and is_instance_valid(scene):
		current_scene = null
		scene.queue_free()
		await process_frame
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_RUN_SAVE_GATE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
