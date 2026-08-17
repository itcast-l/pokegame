extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const RECORDING_SETUP_ID := "jellyfish_capture_showcase_01"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "jellyfish")
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	GameBoardSmokeFactory.configure_recording(board, RECORDING_SETUP_ID)
	root.add_child(board)
	await process_frame

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugAcquireAdventureUpgrade") \
		or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
		or not board.has_method("DebugGetTotalHandCardCountForPlayer") \
		or not board.has_method("DebugGetHandCardCode") \
		or not board.has_method("DebugGetCaptureDamageTargetPlayerIndexForHandCard") \
		or not board.has_method("DebugGetCapturePointsForHandCard") \
		or not board.has_method("DebugSetPlayerScore") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing clubs return debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", "clubs_primary_root_return")):
		_fail("Failed to inject clubs_primary_root_return")
		return

	var damage_target := int(board.call(
		"DebugGetCaptureDamageTargetPlayerIndexForHandCard",
		MAIN_PLAYER_INDEX,
		0
	))
	var capture_points := int(board.call("DebugGetCapturePointsForHandCard", MAIN_PLAYER_INDEX, 0))
	if damage_target < 0 or capture_points <= 0:
		_fail("Expected the recording setup hand card to produce positive capture damage")
		return

	var other_player := 1 if damage_target == MAIN_PLAYER_INDEX else MAIN_PLAYER_INDEX
	if not bool(board.call("DebugSetPlayerScore", damage_target, capture_points + 100)) \
		or not bool(board.call("DebugSetPlayerScore", other_player, 100)):
		_fail("Failed to prepare non-terminal health values")
		return

	var hand_count_before := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	var played_card_code := String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, 0))
	if played_card_code.is_empty():
		_fail("Expected a playable base card before clubs return smoke")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the recording setup hand card")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to resolve post-capture upgrade selection")
		return

	if not await _wait_for_actionable_state(board):
		return

	var hand_count_after := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	if hand_count_after < hand_count_before + 1:
		var hand_codes := []
		for hand_index in range(hand_count_after):
			hand_codes.append(String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, hand_index)))
		_fail("Expected clubs primary root return to leave the main player with at least one extra hand card (before=%d after=%d cards=%s)" % [hand_count_before, hand_count_after, str(hand_codes)])
		return

	var returned_found := false
	for hand_index in range(hand_count_after):
		if String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, hand_index)) == played_card_code:
			returned_found = true
			break

	if not returned_found:
		_fail("Expected the played base card to return to hand")
		return

	print("ADVENTURE_UPGRADE_CLUBS_RETURN_SMOKE_OK")
	quit()


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for upgrade selection")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

	_fail("Timed out waiting for clubs return upgrade selection")
	return false


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for actionable state")
			return false

		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true

	_fail("Timed out waiting for actionable state (phase=%s)" % String(board.call("DebugGetGameLoopPhaseName")))
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_CLUBS_RETURN_SMOKE_FAIL %s" % message)
	quit(1)
