extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0

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
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")

	root.add_child(board)
	await process_frame

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugGetAdventureUpgradeOptionCount") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing adventure upgrade debug hooks")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	var overlay := board.get_node_or_null("AdventureUpgradeSelectionOverlay")
	if overlay == null:
		_fail("Expected adventure upgrade selection overlay scene")
		return

	var visible_overlay_options := 0
	var card_frame_y_positions := []
	var card_frame_rotations := []
	for option_index in range(1, 4):
		var option_button := overlay.get_node_or_null("Margin/Layout/CardsRow/Option%dButton" % option_index) as Button
		if option_button == null:
			_fail("Missing adventure upgrade option button %d" % option_index)
			return

		if not option_button.visible:
			continue

		visible_overlay_options += 1
		if String(option_button.text) != "":
			_fail("Expected adventure upgrade card button text to stay empty")
			return

		if option_button.get_node_or_null("DescriptionScrim") != null:
			_fail("Adventure upgrade overlay should not use a separate bottom description scrim")
			return

		var art := option_button.get_node_or_null("CardFrame/ChoiceArt") as Control
		if art == null:
			_fail("Adventure upgrade overlay is missing integrated choice art control")
			return

		if not art.has_method("ConfigureChoice"):
			_fail("Adventure upgrade choice art should expose ConfigureChoice")
			return

		if art.has_method("SetHighlighted") == false:
			_fail("Adventure upgrade choice art should expose SetHighlighted")
			return

		if art.has_method("DebugGetDescriptionBand") == false:
			_fail("Adventure upgrade choice art should expose DebugGetDescriptionBand for layout verification")
			return

		if option_button.get_node_or_null("CardFrame/InfoOverlay") != null:
			_fail("Adventure upgrade overlay should not keep the old info overlay node")
			return

		var card_frame := option_button.get_node_or_null("CardFrame") as Control
		if card_frame == null:
			_fail("Adventure upgrade overlay is missing card frame")
			return

		card_frame_y_positions.append(card_frame.position.y)
		card_frame_rotations.append(card_frame.rotation_degrees)

	var option_count := int(board.call("DebugGetAdventureUpgradeOptionCount"))
	if option_count < 1 or option_count > 3:
		_fail("Expected 1-3 upgrade options, got %d" % option_count)
		return

	if visible_overlay_options != option_count:
		_fail("Adventure upgrade overlay visible option count mismatch: %d != %d" % [visible_overlay_options, option_count])
		return

	if card_frame_y_positions.size() == 3:
		var left_y := float(card_frame_y_positions[0])
		var center_y := float(card_frame_y_positions[1])
		var right_y := float(card_frame_y_positions[2])
		if not (center_y < left_y and center_y < right_y):
			_fail("Expected middle upgrade card to sit slightly forward of the side cards")
			return

		var left_rotation := float(card_frame_rotations[0])
		var center_rotation := float(card_frame_rotations[1])
		var right_rotation := float(card_frame_rotations[2])
		if not (left_rotation < -0.2 and absf(center_rotation) < 0.2 and right_rotation > 0.2):
			_fail("Expected adventure upgrade cards to fan slightly instead of sitting perfectly flat")
			return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 0:
		_fail("Expected no acquired upgrades before selection")
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to select the first adventure upgrade option")
		return

	if not await _wait_for_actionable_state(board):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Expected exactly one acquired upgrade after run-start selection")
		return

	board.queue_free()
	await process_frame
	print("ADVENTURE_UPGRADE_RUN_START_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for upgrade selection")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

	_fail("Timed out waiting for run-start upgrade selection")
	return false


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for actionable state")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			continue

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "AwaitingMainPlayerAction":
			return true

	_fail("Timed out waiting for actionable state after upgrade selection")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_RUN_START_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
