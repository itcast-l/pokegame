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
		or not board.has_method("DebugSetAdventureUpgradeHoverState") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing adventure upgrade debug hooks")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	var overlay := board.get_node_or_null("AdventureUpgradeSelectionOverlay")
	if overlay == null:
		_fail("Expected adventure upgrade selection overlay scene")
		return

	var header := overlay.get_node_or_null("Margin/Layout/Header") as Control
	if header == null:
		_fail("Adventure upgrade overlay is missing header container")
		return

	if header.visible:
		_fail("Adventure upgrade overlay header should be hidden when only descriptions are shown")
		return

	var visible_overlay_options := 0
	var card_frame_y_positions := []
	var card_frame_rotations := []
	var option_buttons := []
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

		if art.has_method("DebugGetCardDrawRect") == false:
			_fail("Adventure upgrade choice art should expose DebugGetCardDrawRect for aspect-ratio verification")
			return

		if art.has_method("DebugGetTitleText") == false or art.has_method("DebugGetDescriptionText") == false:
			_fail("Adventure upgrade choice art should expose debug text accessors")
			return

		if art.has_method("DebugGetResolvedDescriptionFontSize") == false:
			_fail("Adventure upgrade choice art should expose DebugGetResolvedDescriptionFontSize for typography verification")
			return

		var title_value := String(art.call("DebugGetTitleText"))
		if not title_value.is_empty():
			_fail("Adventure upgrade choice art should not keep a visible title payload")
			return

		var description_value := String(art.call("DebugGetDescriptionText"))
		if description_value.is_empty():
			_fail("Adventure upgrade choice art should keep a non-empty localized description")
			return

		if option_button.get_node_or_null("CardFrame/InfoOverlay") != null:
			_fail("Adventure upgrade overlay should not keep the old info overlay node")
			return

		var card_frame := option_button.get_node_or_null("CardFrame") as Control
		if card_frame == null:
			_fail("Adventure upgrade overlay is missing card frame")
			return

		if option_button.custom_minimum_size.x > 264.0 or option_button.custom_minimum_size.y > 402.0:
			_fail("Adventure upgrade option cards should stay below the tuned overlay size budget")
			return

		var card_draw_rect := art.call("DebugGetCardDrawRect") as Dictionary
		if card_draw_rect.is_empty():
			_fail("Adventure upgrade choice art should report a non-empty card draw rect")
			return

		var card_draw_width := float(card_draw_rect.get("width", 0.0))
		var card_draw_height := float(card_draw_rect.get("height", 0.0))
		if card_draw_width <= 0.0 or card_draw_height <= 0.0:
			_fail("Adventure upgrade choice art should report a valid card draw rect size")
			return

		if card_draw_width / card_draw_height > 0.62:
			_fail("Adventure upgrade card art should preserve a narrow card aspect ratio")
			return

		var description_band := art.call("DebugGetDescriptionBand") as Dictionary
		if description_band.is_empty():
			_fail("Adventure upgrade choice art should report a non-empty description band")
			return
		
		var card_draw_y := float(card_draw_rect.get("y", 0.0))
		var description_band_y := float(description_band.get("y", 0.0))
		var description_band_height := float(description_band.get("height", 0.0))
		var description_band_top_ratio := (description_band_y - card_draw_y) / card_draw_height
		if description_band_top_ratio > 0.72:
			_fail("Adventure upgrade description band should start higher on the card so long text can use the empty lower-middle area")
			return
		
		if description_band_height < card_draw_height * 0.18:
			_fail("Adventure upgrade description band should reserve at least 18% of the card height for long localized text")
			return
		
		var description_band_bottom_ratio := ((description_band_y + description_band_height) - card_draw_y) / card_draw_height
		if description_band_bottom_ratio > 0.90:
			_fail("Adventure upgrade description band should stay clear of the bottom frame ornament")
			return
		
		var description_font_size := int(art.call("DebugGetResolvedDescriptionFontSize"))
		if description_font_size < 11:
			_fail("Adventure upgrade description font should not collapse to a tiny size")
			return

		option_buttons.append(option_button)
		card_frame_y_positions.append(card_frame.position.y)
		card_frame_rotations.append(card_frame.rotation_degrees)

	var option_count := int(board.call("DebugGetAdventureUpgradeOptionCount"))
	if option_count < 1 or option_count > 3:
		_fail("Expected 1-3 upgrade options, got %d" % option_count)
		return

	if visible_overlay_options != option_count:
		_fail("Adventure upgrade overlay visible option count mismatch: %d != %d" % [visible_overlay_options, option_count])
		return

	if card_frame_y_positions.size() == 3 and DisplayServer.get_name().to_lower() != "headless":
		var left_y := float(card_frame_y_positions[0])
		var center_y := float(card_frame_y_positions[1])
		var right_y := float(card_frame_y_positions[2])
		if not (is_equal_approx(left_y, center_y) and is_equal_approx(center_y, right_y)):
			_fail("Expected adventure upgrade cards to stay parallel instead of pushing the middle card forward")
			return

		var left_rotation := float(card_frame_rotations[0])
		var center_rotation := float(card_frame_rotations[1])
		var right_rotation := float(card_frame_rotations[2])
		if not (absf(left_rotation) < 0.05 and absf(center_rotation) < 0.05 and absf(right_rotation) < 0.05):
			_fail("Expected adventure upgrade cards to stay flat and parallel")
			return

	if option_buttons.size() >= 3 and DisplayServer.get_name().to_lower() != "headless":
		var hover_button := option_buttons[2] as Button
		var hover_card_frame := hover_button.get_node_or_null("CardFrame") as Control
		if hover_card_frame == null:
			_fail("Adventure upgrade hover verification is missing the card frame")
			return

		var rest_y := float(hover_card_frame.position.y)
		var rest_scale := float(hover_card_frame.scale.x)
		if not bool(board.call("DebugSetAdventureUpgradeHoverState", 2, true)):
			_fail("Failed to force hover state for the third adventure upgrade card")
			return
		await process_frame

		var hover_step_y := float(hover_card_frame.position.y)
		var hover_step_scale := float(hover_card_frame.scale.x)
		if not (hover_step_y < rest_y):
			_fail("Adventure upgrade hover motion should lift the hovered card")
			return
		if not (hover_step_scale > rest_scale):
			_fail("Adventure upgrade hover motion should enlarge the hovered card")
			return
		if hover_step_scale >= 0.995:
			_fail("Adventure upgrade hover motion should ease toward the target instead of snapping in one frame")
			return

		for _hover_frame in range(18):
			await process_frame

		var settled_hover_y := float(hover_card_frame.position.y)
		var settled_hover_scale := float(hover_card_frame.scale.x)
		if not (settled_hover_y < hover_step_y):
			_fail("Adventure upgrade hover motion should continue moving after the first frame")
			return
		if settled_hover_scale <= hover_step_scale:
			_fail("Adventure upgrade hover scale should continue easing after the first frame")
			return

		if not bool(board.call("DebugSetAdventureUpgradeHoverState", 2, false)):
			_fail("Failed to clear hover state for the third adventure upgrade card")
			return
		await process_frame

		var exit_step_y := float(hover_card_frame.position.y)
		var exit_step_scale := float(hover_card_frame.scale.x)
		if not (exit_step_y > settled_hover_y):
			_fail("Adventure upgrade hover release should move the card back toward the row")
			return
		if not (exit_step_scale < settled_hover_scale):
			_fail("Adventure upgrade hover release should shrink the card back toward its resting scale")
			return

		for _exit_frame in range(18):
			await process_frame

		if absf(float(hover_card_frame.position.y) - rest_y) > 0.35:
			_fail("Adventure upgrade hover release should settle back onto the shared row")
			return
		if absf(float(hover_card_frame.scale.x) - rest_scale) > 0.02:
			_fail("Adventure upgrade hover release should settle back to the resting card scale")
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
	for _frame in range(12):
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
	for _frame in range(12):
		await process_frame
