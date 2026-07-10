extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const EXPECTED_MARKED_TABLE_INDEX := 1
const RETARGETED_TABLE_INDEX := 0
const STABLE_MARKER_TEXTURE_PATH := "res://art/fx/adventure_trace_hunt_marker/run-004/processed/fx-4.png"

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

	if not board.has_method("DebugAcquireAdventureUpgrade") \
		or not board.has_method("DebugConfigureAdventureTraceHuntMarkerScenario") \
		or not board.has_method("DebugRetargetAdventureTraceHuntMarker") \
		or not board.has_method("DebugRemoveMarkedAdventureTraceHuntCardWithBombPair") \
		or not board.has_method("DebugRemoveMarkedAdventureTraceHuntCardWithLeafyBlade") \
		or not board.has_method("DebugClearAdventureUpgradeRunForTraceHuntMarker") \
		or not board.has_method("DebugGetAdventureTraceHuntMarkedTableCardIndex") \
		or not board.has_method("DebugHasAdventureTraceHuntMarkerOverlay") \
		or not board.has_method("DebugGetAdventureTraceHuntMarkerOverlayTexturePath") \
		or not board.has_method("DebugHasAdventureTraceHuntMarkerIntro") \
		or not board.has_method("DebugIsTableCardFaceUp") \
		or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing trace hunt marker debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if not bool(board.call("DebugConfigureAdventureTraceHuntMarkerScenario")):
		_fail("Failed to configure trace hunt marker scenario")
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", "spades_trace_hunt")):
		_fail("Failed to inject spades_trace_hunt")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play trace hunt setup card")
		return

	if not await _wait_for_marker(board):
		return

	if bool(board.call("DebugIsTableCardFaceUp", EXPECTED_MARKED_TABLE_INDEX)):
		_fail("Trace hunt marker revealed the face-down target card")
		return

	if not bool(board.call("DebugHasAdventureTraceHuntMarkerOverlay")):
		_fail("Trace hunt marker overlay is not rendered")
		return

	if String(board.call("DebugGetAdventureTraceHuntMarkerOverlayTexturePath")) != STABLE_MARKER_TEXTURE_PATH:
		_fail("Trace hunt marker did not settle on the stable fourth frame")
		return

	if not bool(board.call("DebugHasAdventureTraceHuntMarkerIntro")):
		_fail("Trace hunt marker intro did not start before retargeting")
		return

	await create_timer(0.08).timeout
	if not bool(board.call("DebugRetargetAdventureTraceHuntMarker", RETARGETED_TABLE_INDEX)):
		_fail("Failed to retarget the trace hunt marker during its intro")
		return

	await create_timer(0.26).timeout
	if int(board.call("DebugGetAdventureTraceHuntMarkedTableCardIndex")) != RETARGETED_TABLE_INDEX:
		_fail("Trace hunt marker did not move to the retargeted table card")
		return

	if not bool(board.call("DebugHasAdventureTraceHuntMarkerIntro")):
		_fail("Previous trace hunt intro canceled the newer retargeted intro")
		return

	await create_timer(0.12).timeout
	await create_timer(1.0).timeout
	if bool(board.call("DebugHasAdventureTraceHuntMarkerIntro")):
		_fail("Trace hunt marker intro did not clean itself up")
		return

	if not bool(board.call("DebugHasAdventureTraceHuntMarkerOverlay")):
		_fail("Trace hunt marker overlay did not persist after its intro")
		return

	if not bool(board.call("DebugRemoveMarkedAdventureTraceHuntCardWithBombPair")):
		_fail("Failed to remove the marked card with a bomb pair")
		return

	if not await _assert_marker_cleared(board, "bomb-pair removal"):
		return

	if not await _configure_and_mark_trace_hunt_target(board):
		return

	if not bool(board.call("DebugRemoveMarkedAdventureTraceHuntCardWithLeafyBlade")):
		_fail("Failed to remove the marked card with leafy blade")
		return

	if not await _assert_marker_cleared(board, "leafy blade removal"):
		return

	if not await _configure_and_mark_trace_hunt_target(board):
		return

	if not bool(board.call("DebugClearAdventureUpgradeRunForTraceHuntMarker")):
		_fail("Failed to clear the adventure upgrade run")
		return

	if not await _assert_marker_cleared(board, "adventure run clearing"):
		return

	print("ADVENTURE_UPGRADE_TRACE_HUNT_MARKER_SMOKE_OK")
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


func _wait_for_marker(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for trace hunt marker")
			return false
		if int(board.call("DebugGetAdventureTraceHuntMarkedTableCardIndex")) == EXPECTED_MARKED_TABLE_INDEX \
			and bool(board.call("DebugHasAdventureTraceHuntMarkerOverlay")):
			return true
	_fail("Timed out waiting for trace hunt marker")
	return false


func _configure_and_mark_trace_hunt_target(board: Node) -> bool:
	if not bool(board.call("DebugConfigureAdventureTraceHuntMarkerScenario")):
		_fail("Failed to configure trace hunt marker scenario")
		return false

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play trace hunt setup card")
		return false

	return await _wait_for_marker(board)


func _assert_marker_cleared(board: Node, removal_name: String) -> bool:
	await process_frame
	if int(board.call("DebugGetAdventureTraceHuntMarkedTableCardIndex")) != -1 \
		or bool(board.call("DebugHasAdventureTraceHuntMarkerOverlay")) \
		or bool(board.call("DebugHasAdventureTraceHuntMarkerIntro")):
		_fail("Trace hunt marker did not clear after %s" % removal_name)
		return false
	return true


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_TRACE_HUNT_MARKER_SMOKE_FAIL %s" % message)
	quit(1)
