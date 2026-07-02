extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _run_item_overlay_phase()
	if _failed:
		return

	await _run_adventure_overlay_phase()
	if _failed:
		return

	print("SELECTION_OVERLAY_CONTROLLER_ISOLATION_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _run_item_overlay_phase() -> void:
	var board := await _instantiate_board(false)
	if board == null:
		return

	if not board.has_method("DebugStartPendingTargetHandSelectionForSeat") \
		or not board.has_method("DebugGetPendingItemSelectionModeName"):
		_fail("Missing item overlay debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if not bool(board.call("DebugStartPendingTargetHandSelectionForSeat", 0)):
		_fail("Failed to start local pending target-hand selection")
		return

	await process_frame

	var item_overlay := board.get_node_or_null("ItemSelectionOverlay")
	if item_overlay == null:
		_fail("Expected item selection overlay to appear")
		return

	if board.get_node_or_null("AdventureUpgradeSelectionOverlay") != null:
		_fail("Adventure upgrade overlay should not appear during item selection")
		return

	if not _press_first_item_option(item_overlay):
		_fail("Failed to press first item selection option")
		return

	if not await _wait_for_pending_item_selection_mode(board, "TargetHandCard"):
		return

	item_overlay = board.get_node_or_null("ItemSelectionOverlay")
	if item_overlay == null:
		_fail("Expected item selection overlay to remain visible for target-hand selection")
		return

	if board.get_node_or_null("AdventureUpgradeSelectionOverlay") != null:
		_fail("Adventure upgrade overlay should not appear during target-hand selection")
		return

	if not _press_first_item_option(item_overlay):
		_fail("Failed to press first target-hand selection option")
		return

	if not await _wait_for_pending_item_selection_mode(board, "None"):
		return

	board.queue_free()
	for _frame in range(12):
		await process_frame


func _run_adventure_overlay_phase() -> void:
	var board := await _instantiate_board(true)
	if board == null:
		return

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing adventure overlay debug hooks")
		return

	if not await _wait_for_adventure_upgrade_selection(board):
		return

	var adventure_overlay := board.get_node_or_null("AdventureUpgradeSelectionOverlay")
	if adventure_overlay == null:
		_fail("Expected adventure upgrade selection overlay to appear")
		return

	if board.get_node_or_null("ItemSelectionOverlay") != null:
		_fail("Item selection overlay should not appear during adventure upgrade selection")
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to resolve adventure upgrade selection")
		return

	if not await _wait_for_actionable_state(board):
		return

	board.queue_free()
	for _frame in range(12):
		await process_frame


func _instantiate_board(adventure_mode_enabled: bool) -> Node:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return null

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return null

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", adventure_mode_enabled)
	if adventure_mode_enabled:
		board.set("adventure_enemy_avatar_id", "shrimp")

	root.add_child(board)
	await process_frame
	return board


func _press_first_item_option(item_overlay: Node) -> bool:
	var option_button := item_overlay.get_node_or_null("%Option1Button") as Button
	if option_button == null or option_button.disabled or not option_button.visible:
		return false

	option_button.emit_signal("pressed")
	return true


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


func _wait_for_pending_item_selection_mode(board: Node, expected_mode: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for pending item selection mode %s" % expected_mode)
			return false

		if String(board.call("DebugGetPendingItemSelectionModeName")) == expected_mode:
			return true

	_fail("Timed out waiting for pending item selection mode %s" % expected_mode)
	return false


func _wait_for_adventure_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for adventure upgrade selection")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

	_fail("Timed out waiting for adventure upgrade selection")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("SELECTION_OVERLAY_CONTROLLER_ISOLATION_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(12):
		await process_frame
