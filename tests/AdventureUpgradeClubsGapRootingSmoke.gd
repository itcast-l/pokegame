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
		or not board.has_method("DebugConfigureAdventureGapRootingScenario") \
		or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
		or not board.has_method("DebugGetTotalHandCardCountForPlayer") \
		or not board.has_method("DebugGetHandCardCode") \
		or not board.has_method("DebugGetDeckCardCountForPlayer") \
		or not board.has_method("DebugGetDeckCardCode") \
		or not board.has_method("DebugGetPlayerScoreValue") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing gap rooting debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if not bool(board.call("DebugConfigureAdventureGapRootingScenario")):
		_fail("Failed to configure gap rooting scenario")
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", "clubs_gap_rooting")):
		_fail("Failed to inject clubs_gap_rooting")
		return

	var enemy_health_before := int(board.call("DebugGetPlayerScoreValue", 1))
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play gap rooting setup card")
		return

	if not await _wait_for_settled_state(board):
		return
	if int(board.call("DebugGetPlayerScoreValue", 1)) != enemy_health_before:
		_fail("Gap Rooting incorrectly scored its 2-point capture extension")
		return

	var found_gap_rooting_card := false
	var visible_codes := []
	var hand_count_after := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	for hand_index in range(hand_count_after):
		var hand_code := String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, hand_index))
		if hand_index < 5:
			visible_codes.append("hand:%s" % hand_code)
		if hand_code == "Clubs:2":
			found_gap_rooting_card = true
			break

	if not found_gap_rooting_card:
		var deck_count_after := int(board.call("DebugGetDeckCardCountForPlayer", MAIN_PLAYER_INDEX))
		for deck_index in range(deck_count_after):
			var deck_code := String(board.call("DebugGetDeckCardCode", MAIN_PLAYER_INDEX, deck_index))
			if deck_index < 5:
				visible_codes.append("deck:%s" % deck_code)
			if deck_code == "Clubs:2":
				found_gap_rooting_card = true
				break

	if not found_gap_rooting_card:
		_fail("Expected left adjacent base card to be collected into the hand or deck, saw %s" % str(visible_codes))
		return

	print("ADVENTURE_UPGRADE_CLUBS_GAP_ROOTING_SMOKE_OK")
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
	_fail("Timed out waiting for actionable state")
	return false


func _wait_for_settled_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for settled state")
			return false
		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "AwaitingMainPlayerAction" or phase == "GameOver":
			return true
	_fail("Timed out waiting for settled state")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_CLUBS_GAP_ROOTING_SMOKE_FAIL %s" % message)
	quit(1)
