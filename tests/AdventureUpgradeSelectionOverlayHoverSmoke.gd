extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"

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

	if not board.has_method("DebugShowAdventureUpgradeSelectionOverlaySample"):
		_fail("Missing direct adventure upgrade overlay debug hook")
		return
	if not board.has_method("DebugSetAdventureUpgradeHoverState"):
		_fail("Missing direct adventure upgrade hover-state debug hook")
		return

	if not bool(board.call("DebugShowAdventureUpgradeSelectionOverlaySample")):
		_fail("Failed to show direct adventure upgrade overlay sample")
		return

	await process_frame
	await process_frame

	var overlay := board.get_node_or_null("AdventureUpgradeSelectionOverlay")
	if overlay == null:
		_fail("Expected direct adventure upgrade overlay scene")
		return

	var card_frame_y_positions := []
	var card_frame_rotations := []
	var option_buttons := []
	for option_index in range(1, 4):
		var option_button := overlay.get_node_or_null("Margin/Layout/CardsRow/Option%dButton" % option_index) as Button
		if option_button == null:
			_fail("Missing adventure upgrade option button %d" % option_index)
			return

		option_buttons.append(option_button)
		var card_frame := option_button.get_node_or_null("CardFrame") as Control
		if card_frame == null:
			_fail("Adventure upgrade overlay is missing card frame %d" % option_index)
			return

		card_frame_y_positions.append(card_frame.position.y)
		card_frame_rotations.append(card_frame.rotation_degrees)

	if card_frame_y_positions.size() == 3:
		var left_y := float(card_frame_y_positions[0])
		var center_y := float(card_frame_y_positions[1])
		var right_y := float(card_frame_y_positions[2])
		if not (is_equal_approx(left_y, center_y) and is_equal_approx(center_y, right_y)):
			_fail("Expected direct adventure upgrade cards to stay parallel instead of pushing the middle card forward")
			return

		var left_rotation := float(card_frame_rotations[0])
		var center_rotation := float(card_frame_rotations[1])
		var right_rotation := float(card_frame_rotations[2])
		if not (absf(left_rotation) < 0.05 and absf(center_rotation) < 0.05 and absf(right_rotation) < 0.05):
			_fail("Expected direct adventure upgrade cards to stay flat and parallel")
			return

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

	board.queue_free()
	for _frame in range(12):
		await process_frame

	print("ADVENTURE_UPGRADE_SELECTION_OVERLAY_HOVER_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _dispose_audio_before_quit() -> void:
	return


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_SELECTION_OVERLAY_HOVER_SMOKE_FAIL %s" % message)
	quit(1)
