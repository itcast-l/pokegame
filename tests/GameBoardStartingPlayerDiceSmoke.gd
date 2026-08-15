extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const STEP_TIMEOUT_SECONDS := 18.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	if not await _run_three_player_ai_start_case():
		return
	if not await _run_four_player_and_restart_case():
		return
	if not await _run_two_player_case():
		return
	print("GAME_BOARD_STARTING_PLAYER_DICE_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _run_three_player_ai_start_case() -> bool:
	var board := await _create_idle_board()
	if board == null:
		return false
	if not bool(board.call("DebugStartOpeningDiceMatch", 3, 2)):
		_fail("Could not start forced three-player opening")
		return false
	if not await _assert_dice_barrier(board, 2, 2, "three-player"):
		return false

	var deadline := _deadline()
	var saw_ai_phase := false
	while _now() < deadline:
		await process_frame
		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "ResolvingOpponentTurns":
			saw_ai_phase = true
		if saw_ai_phase and phase == "AwaitingMainPlayerAction" \
				and int(board.call("DebugGetCurrentTurnPlayerIndex")) == 0:
			board.queue_free()
			await process_frame
			return true
	_fail("Three-player AI starting turn did not run automatically")
	return false


func _run_four_player_and_restart_case() -> bool:
	var board := await _create_idle_board()
	if board == null:
		return false
	if not bool(board.call("DebugStartOpeningDiceMatch", 4, 5)):
		_fail("Could not start forced four-player opening")
		return false
	if not await _assert_dice_barrier(board, 5, 0, "four-player"):
		return false
	if not await _wait_for_local_action(board, "four-player"):
		return false
	var first_generation := int(board.call("DebugGetTurnSequenceGeneration"))

	if not bool(board.call("DebugStartOpeningDiceMatch", 4, 1)):
		_fail("Could not restart forced four-player opening")
		return false
	if not await _assert_dice_barrier(board, 1, 0, "four-player restart"):
		return false
	if int(board.call("DebugGetTurnSequenceGeneration")) <= first_generation:
		_fail("Four-player restart did not advance turn generation")
		return false
	if not await _wait_for_local_action(board, "four-player restart"):
		return false
	board.queue_free()
	await process_frame
	return true


func _run_two_player_case() -> bool:
	var board := await _create_idle_board()
	if board == null:
		return false
	if not bool(board.call("DebugStartOpeningDiceMatch", 2, 0)):
		_fail("Could not start two-player opening")
		return false
	var deadline := _deadline()
	while _now() < deadline:
		await process_frame
		if bool(board.call("DebugIsDiceRollPlaying")):
			_fail("Two-player opening unexpectedly played dice")
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) != "OpeningDeal":
			if int(board.call("DebugGetOpeningDiceRollResult")) != 0 \
					or int(board.call("DebugGetOpeningStartingTurnSeat")) != 0 \
					or int(board.call("DebugGetLastDiceRollResult")) != 0:
				_fail("Two-player opening recorded a dice result")
				return false
			if not bool(board.call("DebugCanMainPlayerAct")):
				_fail("Two-player opening did not unlock local action")
				return false
			board.queue_free()
			await process_frame
			return true
	_fail("Two-player opening did not complete")
	return false


func _create_idle_board() -> Node:
	var packed_scene := load(SCENE_PATH) as PackedScene
	var board := packed_scene.instantiate() if packed_scene != null else null
	if board == null:
		_fail("Could not instantiate game board")
		return null
	board.set("network_wait_for_initialize", true)
	board.set("solo_mode", true)
	board.set("item_mode", false)
	root.add_child(board)
	await process_frame
	for method_name in [
		"DebugStartOpeningDiceMatch",
		"DebugGetOpeningDiceRollResult",
		"DebugGetOpeningStartingTurnSeat",
		"DebugAreOpeningCardsFullyRevealed",
		"DebugIsDiceRollPlaying",
		"DebugGetGameLoopPhaseName",
		"DebugCanMainPlayerAct",
	]:
		if not board.has_method(method_name):
			_fail("Missing opening dice debug hook: %s" % method_name)
			return null
	return board


func _assert_dice_barrier(
		board: Node,
		expected_roll: int,
		expected_starting_seat: int,
		label: String) -> bool:
	var deadline := _deadline()
	while _now() < deadline:
		await process_frame
		if not bool(board.call("DebugIsDiceRollPlaying")):
			continue
		if String(board.call("DebugGetGameLoopPhaseName")) != "OpeningDeal":
			_fail("%s dice released OpeningDeal early" % label)
			return false
		if bool(board.call("DebugCanMainPlayerAct")):
			_fail("%s dice allowed input during playback" % label)
			return false
		if not bool(board.call("DebugAreOpeningCardsFullyRevealed")):
			_fail("%s dice started before dealing completed" % label)
			return false
		if int(board.call("DebugGetOpeningDiceRollResult")) != expected_roll \
				or int(board.call("DebugGetLastDiceRollResult")) != expected_roll:
			_fail("%s played the wrong forced dice result" % label)
			return false
		if int(board.call("DebugGetOpeningStartingTurnSeat")) != expected_starting_seat \
				or int(board.call("DebugGetCurrentTurnPlayerIndex")) != expected_starting_seat:
			_fail("%s wrote the wrong starting seat" % label)
			return false
		return await _wait_for_dice_completion(board, label)
	_fail("%s dice never started" % label)
	return false


func _wait_for_dice_completion(board: Node, label: String) -> bool:
	var deadline := _deadline()
	while _now() < deadline:
		await process_frame
		if not bool(board.call("DebugIsDiceRollPlaying")):
			if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
				await process_frame
			return true
	_fail("%s dice never completed" % label)
	return false


func _wait_for_local_action(board: Node, label: String) -> bool:
	var deadline := _deadline()
	while _now() < deadline:
		await process_frame
		if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			continue
		if bool(board.call("DebugCanMainPlayerAct")):
			return true
	_fail("%s did not unlock local action" % label)
	return false


func _now() -> float:
	return Time.get_ticks_msec() / 1000.0


func _deadline() -> float:
	return _now() + STEP_TIMEOUT_SECONDS


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("GAME_BOARD_STARTING_PLAYER_DICE_SMOKE_FAIL %s" % message)
	quit(1)
