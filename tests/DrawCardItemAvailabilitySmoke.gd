extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0

var _failed := false

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var board := await _create_board()
	if board == null:
		return
	if not await _wait_for_actionable_state(board):
		return
	if not bool(board.call("DebugConfigureDrawCardItemAvailabilityScenario", 0, 8)):
		_fail("Failed to configure unavailable draw-item scenario")
		return
	var unavailable_item_count := int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	if bool(board.call("DebugCanClickDrawCardItem")):
		_fail("UI exposed a draw item with an empty deck")
		return
	if int(board.call("DebugCountAiPlayableDrawCardItems")) != 0:
		_fail("AI selected a draw item with an empty deck")
		return
	board.call("DebugClickDrawCardItem")
	await process_frame
	if int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX)) != unavailable_item_count:
		_fail("Unavailable UI draw item was consumed")
		return
	if bool(board.call("DebugNetworkAuthorityAcceptsDrawCardItem")):
		_fail("Network authority accepted a draw item with an empty deck")
		return
	if int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX)) != unavailable_item_count:
		_fail("Unavailable network draw item was consumed")
		return

	if not bool(board.call("DebugConfigureDrawCardItemAvailabilityScenario", 1, 8)):
		_fail("Failed to configure network full-hand draw-item scenario")
		return
	if not bool(board.call("DebugNetworkAuthorityAcceptsDrawCardItem")):
		_fail("Network authority rejected legal full-hand draw item")
		return
	if not await _wait_for_fulfilled_draw(board):
		return

	if not bool(board.call("DebugConfigureDrawCardItemAvailabilityScenario", 1, 8)):
		_fail("Failed to configure UI full-hand draw-item scenario")
		return
	if not bool(board.call("DebugCanClickDrawCardItem")):
		_fail("UI rejected legal draw item after its own hand slot is released")
		return
	if int(board.call("DebugCountAiPlayableDrawCardItems")) != 1:
		_fail("AI did not see legal full-hand draw item")
		return
	board.call("DebugClickDrawCardItem")
	if not await _wait_for_fulfilled_draw(board):
		return

	print("DRAW_CARD_ITEM_AVAILABILITY_SMOKE_OK")
	quit()

func _create_board() -> Node:
	await process_frame
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load game board scene")
		return null
	var board := GameBoardSmokeFactory.create_local(packed_scene)
	root.add_child(board)
	await process_frame
	for method in ["DebugConfigureDrawCardItemAvailabilityScenario", "DebugCanClickDrawCardItem", "DebugClickDrawCardItem", "DebugCountAiPlayableDrawCardItems", "DebugNetworkAuthorityAcceptsDrawCardItem", "DebugGetTotalItemHandCardCountForPlayer", "DebugGetTotalHandCardCountForPlayer", "DebugGetDeckCardCountForPlayer"]:
		if not board.has_method(method):
			_fail("Missing draw item debug hook: %s" % method)
			return null
	return board

func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed before initial deal completed")
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for initial actionable board state")
	return false

func _wait_for_fulfilled_draw(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed while resolving draw item")
			return false
		if int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX)) == 0 \
				and int(board.call("DebugGetDeckCardCountForPlayer", MAIN_PLAYER_INDEX)) == 0:
			if int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX)) != 8:
				_fail("Fulfilled draw did not preserve total hand count at eight")
				return false
			return true
	_fail("Timed out waiting for legal draw item to resolve (items=%d deck=%d hand=%d processing=%s snapshot=%d phase=%s)" % [int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX)), int(board.call("DebugGetDeckCardCountForPlayer", MAIN_PLAYER_INDEX)), int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX)), str(bool(board.call("DebugIsProcessingCardPlayForOwnershipProbe"))), int(board.call("DebugGetNetworkAuthoritativeVisualSnapshotId")), String(board.call("DebugGetGameLoopPhaseName"))])
	return false

func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error("DRAW_CARD_ITEM_AVAILABILITY_SMOKE_FAIL %s" % message)
	quit(1)
