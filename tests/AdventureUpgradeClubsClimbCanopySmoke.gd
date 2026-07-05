extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0
const SUIT_COLLECT_ITEM_ID := "suit_collect"

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
		or not board.has_method("DebugConfigureAdventureSuitCollectReturnScenario") \
		or not board.has_method("DebugAttachItemEffectToMainPlayerHandCard") \
		or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
		or not board.has_method("DebugGetTotalHandCardCountForPlayer") \
		or not board.has_method("DebugGetHandCardCode") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing climb canopy debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if not bool(board.call("DebugConfigureAdventureSuitCollectReturnScenario")):
		_fail("Failed to configure suit-collect return scenario")
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", "clubs_climb_canopy")):
		_fail("Failed to inject clubs_climb_canopy")
		return

	if not bool(board.call("DebugAttachItemEffectToMainPlayerHandCard", 0, SUIT_COLLECT_ITEM_ID)):
		_fail("Failed to attach suit_collect to the main player's card")
		return

	var hand_count_before := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	var played_card_code := String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, 0))

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the suit-collect capture card")
		return

	if not await _wait_for_actionable_state(board):
		return

	var hand_count_after := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	if hand_count_after < hand_count_before + 1:
		_fail("Expected climb canopy to return the played card after attached effect trigger")
		return

	var returned_found := false
	for hand_index in range(hand_count_after):
		if String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, hand_index)) == played_card_code:
			returned_found = true
			break

	if not returned_found:
		_fail("Expected the suit-collect capture card to return to hand")
		return

	print("ADVENTURE_UPGRADE_CLUBS_CLIMB_CANOPY_SMOKE_OK")
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


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_CLUBS_CLIMB_CANOPY_SMOKE_FAIL %s" % message)
	quit(1)
