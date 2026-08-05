extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
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

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	await process_frame

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugAcquireAdventureUpgrade") \
		or not board.has_method("DebugConfigureAdventurePairCaptureReturnScenario") \
			or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
			or not board.has_method("DebugGetTotalHandCardCountForPlayer") \
			or not board.has_method("DebugGetHandCardCode") \
			or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing clubs short return debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if not bool(board.call("DebugConfigureAdventurePairCaptureReturnScenario")):
		_fail("Failed to configure pair-capture return scenario")
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", "clubs_short_branch_return")):
		_fail("Failed to inject clubs_short_branch_return")
		return

	var hand_count_before := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	if hand_count_before != 1:
		_fail("Expected pair-capture setup to begin with exactly one hand card")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the pair-capture setup hand card")
		return

	if not await _wait_for_actionable_state(board):
		return

	var hand_count_after := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	if hand_count_after < 2:
		_fail("Expected short branch return to leave at least two cards in hand, got %d" % hand_count_after)
		return

	var hand_codes := []
	for hand_index in range(hand_count_after):
		hand_codes.append(String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, hand_index)))

	if not hand_codes.has("Hearts:5") or not hand_codes.has("Clubs:5"):
		_fail("Expected both pair-captured cards to return to hand, got %s" % str(hand_codes))
		return

	print("ADVENTURE_UPGRADE_CLUBS_SHORT_RETURN_SMOKE_OK")
	quit()


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
	print("ADVENTURE_UPGRADE_CLUBS_SHORT_RETURN_SMOKE_FAIL %s" % message)
	quit(1)
