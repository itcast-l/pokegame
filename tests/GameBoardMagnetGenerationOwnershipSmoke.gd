extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 25.0

var _failed := false

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var board := await _create_board()
	if board == null:
		return
	if not await _run_replacement_probe(board):
		return
	if not await _run_no_replacement_probe(board):
		return
	if _failed:
		return
	board.queue_free()
	await process_frame
	print("GAME_BOARD_MAGNET_GENERATION_OWNERSHIP_SMOKE_OK")
	quit()

func _run_replacement_probe(board: Node) -> bool:
	var first := {}
	if not bool(board.call("DebugConfigureMagnetGenerationOwnershipProbe")) \
		or not bool(board.call("DebugStartMagnetGenerationOwnershipProbe", first)) \
		or not await _wait_for_owned_selection(board, first) \
		or not bool(board.call("DebugBeginMagnetGenerationOwnershipAnimation")) \
		or not await _wait_for_true(board, "DebugIsMagnetGenerationOwnershipAnimationActive"):
		_fail("Could not start first magnet ownership probe")
		return false
	if not bool(board.call("DebugRestartMatchForMagnetGenerationOwnershipProbe")):
		_fail("Could not restart match during magnet animation")
		return false
	if not bool(board.call("DebugConfigureMagnetGenerationOwnershipProbe")):
		_fail("Could not configure replacement magnet ownership probe")
		return false
	var replacement := {}
	if not bool(board.call("DebugStartMagnetGenerationOwnershipProbe", replacement)) \
		or not await _wait_for_owned_selection(board, replacement):
		_fail("Could not start replacement magnet ownership probe")
		return false
	if not await _wait_for_result(first):
		return false
	if not await _wait_for_animation_nodes_gone(board):
		return false
	# The old continuation must not clear or attach into the replacement selection.
	if bool(replacement.get("completed", true)) \
		or not bool(board.call("DebugHasCurrentOwnedMagnetSelection")) \
		or String(board.call("DebugGetPendingItemSelectionModeName")) != "AttachmentRewardHandCard" \
		or not bool(board.call("DebugIsProcessingCardPlayForOwnershipProbe")) \
		or int(board.call("DebugCountMainPlayerHandCardsWithAttachment", "magnet")) != 0:
		_fail("Old magnet continuation mutated replacement selection")
		return false
	if not bool(board.call("DebugCancelMagnetGenerationOwnershipProbe")) \
		or not await _wait_for_result(replacement) \
		or String(replacement.get("error", "")) != "":
		_fail("Replacement magnet probe did not cancel cleanly: %s" % str(replacement))
		return false
	return true

func _run_no_replacement_probe(board: Node) -> bool:
	var first := {}
	if not bool(board.call("DebugConfigureMagnetGenerationOwnershipProbe")) \
		or not bool(board.call("DebugStartMagnetGenerationOwnershipProbe", first)) \
		or not await _wait_for_owned_selection(board, first) \
		or not bool(board.call("DebugBeginMagnetGenerationOwnershipAnimation")) \
		or not await _wait_for_true(board, "DebugIsMagnetGenerationOwnershipAnimationActive") \
		or not bool(board.call("DebugRestartMatchForMagnetGenerationOwnershipProbe")) \
		or not await _wait_for_result(first):
		_fail("Could not complete no-replacement magnet ownership probe")
		return false
	if not await _wait_for_animation_nodes_gone(board):
		return false
	if String(board.call("DebugGetPendingItemSelectionModeName")) != "None" \
		or bool(board.call("DebugIsProcessingCardPlayForOwnershipProbe")) \
		or int(board.call("DebugCountMainPlayerHandCardsWithAttachment", "magnet")) != 0 \
		or String(first.get("error", "")) != "":
		_fail("Old magnet continuation left stale state after restart: %s" % str(first))
		return false
	return true

func _create_board() -> Node:
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
	root.add_child(board)
	await process_frame
	for method in ["DebugConfigureMagnetGenerationOwnershipProbe", "DebugStartMagnetGenerationOwnershipProbe", "DebugBeginMagnetGenerationOwnershipAnimation", "DebugRestartMatchForMagnetGenerationOwnershipProbe", "DebugHasCurrentOwnedMagnetSelection", "DebugGetMagnetAttachmentAnimationNodeCount", "DebugCancelMagnetGenerationOwnershipProbe"]:
		if not board.has_method(method):
			_fail("Missing magnet ownership hook: %s" % method)
			return null
	return board

func _wait_for_owned_selection(board: Node, result: Dictionary) -> bool:
	var owned := await _wait_for_true(board, "DebugHasCurrentOwnedMagnetSelection", false)
	if not owned:
		_fail("Timed out waiting for owned magnet selection: %s" % str(result))
	return owned

func _wait_for_true(board: Node, method: String, report_failure := true) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(board.call(method)):
			return true
	if report_failure:
		_fail("Timed out waiting for %s" % method)
	return false

func _wait_for_result(result: Dictionary) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for magnet probe result")
	return false

func _wait_for_animation_nodes_gone(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if int(board.call("DebugGetMagnetAttachmentAnimationNodeCount")) == 0:
			return true
	_fail("Timed out waiting for old magnet animation nodes to leave the tree")
	return false

func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("GAME_BOARD_MAGNET_GENERATION_OWNERSHIP_SMOKE_FAIL %s" % message)
	quit(1)
