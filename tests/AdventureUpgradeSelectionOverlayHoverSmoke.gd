extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_FRAMES := 240
const REST_SCALE := 0.97
const ACTIVE_SCALE := 1.015
const ACTIVE_LIFT := 8.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return
	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "shrimp")
	root.add_child(board)
	await process_frame

	for method in [
		"DebugShowAdventureUpgradeSelectionOverlaySample",
		"DebugSetAdventureUpgradeHoverState",
		"DebugSetAdventureUpgradeFocusState",
		"DebugGetAdventureUpgradeOverlayMotionState",
		"DebugGetAdventureUpgradeOverlayEventMetrics",
		"DebugDisposeActiveSelectionOverlayForAwaitProbe",
	]:
		if not board.has_method(method):
			_fail("Missing overlay debug hook: %s" % method)
			return
	if not bool(board.call("DebugShowAdventureUpgradeSelectionOverlaySample")):
		_fail("Failed to show overlay sample")
		return

	# A hover arriving during reveal must not force completion or unlock interaction.
	if not bool(board.call("DebugSetAdventureUpgradeHoverState", 0, true)):
		_fail("Failed to inject pre-unlock hover")
		return
	await process_frame
	var early_state := _motion_state(board, 0)
	if bool(early_state.get("reveal_completed", true)) \
			or bool(early_state.get("interaction_unlocked", true)) \
			or not bool(early_state.get("button_disabled", false)):
		_fail("Pre-unlock hover skipped or unlocked the reveal")
		return
	board.call("DebugSetAdventureUpgradeHoverState", 0, false)

	if not await _wait_for_reveal_unlock(board):
		return

	# Release the default focus so every card can settle at the shared resting state.
	board.call("DebugSetAdventureUpgradeFocusState", 1, false)
	await _wait_frames(24)
	for option_index in range(3):
		var state := _motion_state(board, option_index)
		if state.is_empty():
			_fail("Missing motion state for option %d" % option_index)
			return
		var visual := state["card_visual"] as Dictionary
		if not _assert_flat_card_state(visual):
			return
		if absf((state["scale"] as Vector2).x - REST_SCALE) > 0.015:
			_fail("Card did not settle to rest scale")
			return
		if absf(float(state["rotation_degrees"])) > 0.01:
			_fail("Resting card is not front-facing")
			return

	var rest_state := _motion_state(board, 1)
	var rest_position := rest_state["position"] as Vector2

	# Mouse hover activation.
	if not bool(board.call("DebugSetAdventureUpgradeHoverState", 1, true)):
		_fail("Failed to activate hover")
		return
	await process_frame
	var hover_step := _motion_state(board, 1)
	if not ((hover_step["position"] as Vector2).y < rest_position.y) \
			or not ((hover_step["scale"] as Vector2).x > REST_SCALE):
		_fail("Hover activation did not ease position and scale")
		return
	await _wait_frames(24)
	var hover_settled := _motion_state(board, 1)
	if absf((hover_settled["position"] as Vector2).y - (rest_position.y - ACTIVE_LIFT)) > 0.5:
		_fail("Hover card did not settle 8px above rest")
		return
	if absf((hover_settled["scale"] as Vector2).x - ACTIVE_SCALE) > 0.01:
		_fail("Hover card did not settle at scale 1.015")
		return
	if int(hover_settled["frame_z_index"]) <= 0 or int(hover_settled["button_z_index"]) <= 0:
		_fail("Hover card did not rise above sibling Z indices")
		return
	var hover_visual := hover_settled["card_visual"] as Dictionary
	if absf(float(hover_visual["shadow_offset_y"]) - 6.0) > 0.5 \
			or float(hover_visual["glow_alpha"]) <= 0.5 \
			or not bool(hover_visual.get("surface_is_flat", false)):
		_fail("Hover card glow or flat presentation is incomplete")
		return

	board.call("DebugSetAdventureUpgradeHoverState", 1, false)
	if not await _wait_for_restored(board, 1, rest_position):
		return

	# Keyboard/gamepad focus must use the same complete activation state.
	if not bool(board.call("DebugSetAdventureUpgradeFocusState", 1, true)):
		_fail("Failed to activate focus")
		return
	await _wait_frames(24)
	var focus_settled := _motion_state(board, 1)
	if absf((focus_settled["position"] as Vector2).y - (rest_position.y - ACTIVE_LIFT)) > 0.5 \
				or absf((focus_settled["scale"] as Vector2).x - ACTIVE_SCALE) > 0.01:
		_fail("Focus does not match hover activation")
		return
	var focus_visual := focus_settled["card_visual"] as Dictionary
	if float(focus_visual["glow_alpha"]) <= 0.5 \
			or not bool(focus_visual.get("surface_is_flat", false)):
		_fail("Focus glow or flat presentation does not match hover")
		return

	board.call("DebugSetAdventureUpgradeFocusState", 1, false)
	if not await _wait_for_restored(board, 1, rest_position):
		return

	# One emitted mouse signal must invoke exactly one strongly typed callback.
	var overlay := board.find_child("AdventureUpgradeSelectionOverlay", true, false)
	var event_button := overlay.get_node("Margin/Layout/CardsRow/Option2Button") as Button
	var metrics_before := board.call("DebugGetAdventureUpgradeOverlayEventMetrics") as Dictionary
	event_button.emit_signal("mouse_entered")
	await process_frame
	var metrics_entered := board.call("DebugGetAdventureUpgradeOverlayEventMetrics") as Dictionary
	if int(metrics_entered["pointer_entered_callbacks"]) != int(metrics_before["pointer_entered_callbacks"]) + 1:
		_fail("One mouse-enter signal invoked more than one callback")
		return
	event_button.emit_signal("mouse_exited")
	await process_frame
	var metrics_exited := board.call("DebugGetAdventureUpgradeOverlayEventMetrics") as Dictionary
	if int(metrics_exited["pointer_exited_callbacks"]) != int(metrics_before["pointer_exited_callbacks"]) + 1:
		_fail("One mouse-exit signal invoked more than one callback")
		return

	# Disposal must detach callbacks before the overlay is queued for release.
	if not bool(board.call("DebugDisposeActiveSelectionOverlayForAwaitProbe")):
		_fail("Could not dispose the upgrade overlay")
		return
	var callbacks_before_release_emit := board.call("DebugGetAdventureUpgradeOverlayEventMetrics") as Dictionary
	event_button.emit_signal("mouse_entered")
	event_button.emit_signal("mouse_exited")
	var callbacks_after_release_emit := board.call("DebugGetAdventureUpgradeOverlayEventMetrics") as Dictionary
	if int(callbacks_after_release_emit["active_subscription_groups"]) != 0 \
			or int(callbacks_after_release_emit["pointer_entered_callbacks"]) != int(callbacks_before_release_emit["pointer_entered_callbacks"]) \
			or int(callbacks_after_release_emit["pointer_exited_callbacks"]) != int(callbacks_before_release_emit["pointer_exited_callbacks"]):
		_fail("Upgrade overlay retained event subscriptions after disposal")
		return

	board.queue_free()
	await _wait_frames(8)
	print("ADVENTURE_UPGRADE_SELECTION_OVERLAY_HOVER_SMOKE_OK")
	quit(0)


func _wait_for_reveal_unlock(board: Node) -> bool:
	for _frame in range(TIMEOUT_FRAMES):
		await process_frame
		var all_ready := true
		for option_index in range(3):
			var state := _motion_state(board, option_index)
			if state.is_empty() \
					or not bool(state.get("reveal_completed", false)) \
					or not bool(state.get("interaction_unlocked", false)) \
					or bool(state.get("button_disabled", true)):
				all_ready = false
				break
		if all_ready:
			return true
	_fail("Timed out waiting for reveal completion and interaction unlock")
	return false


func _motion_state(board: Node, option_index: int) -> Dictionary:
	return board.call("DebugGetAdventureUpgradeOverlayMotionState", option_index) as Dictionary


func _wait_for_restored(board: Node, option_index: int, rest_position: Vector2) -> bool:
	for _frame in range(TIMEOUT_FRAMES):
		await process_frame
		var state := _motion_state(board, option_index)
		if state.is_empty():
			continue
		var visual := state["card_visual"] as Dictionary
		if (state["position"] as Vector2).distance_to(rest_position) <= 0.5 \
				and absf((state["scale"] as Vector2).x - REST_SCALE) <= 0.01 \
				and absf(float(visual["shadow_offset_y"]) - 6.0) <= 0.5 \
				and float(visual["glow_alpha"]) <= 0.03:
			return true
	return _assert_restored(_motion_state(board, option_index), rest_position)



func _assert_flat_card_state(visual: Dictionary) -> bool:
	if not bool(visual.get("surface_is_flat", false)) \
			or int(visual.get("thickness_layer_count", -1)) != 0 \
			or not String(visual.get("surface_shader_path", "")).is_empty():
		_fail("Choice card must remain flat and free of perspective layers")
		return false
	if int(visual["surface_material_id"]) != 0 \
			or not bool(visual.get("glow_uses_outline_panel", false)):
		_fail("Complete card texture must be unmodified and retain its outline glow")
		return false
	if not bool(visual.get("surface_has_texture", false)) \
			or not bool(visual.get("uses_complete_card_texture", false)) \
			or bool(visual.get("suit_is_separate_layer", true)):
		_fail("Choice card must use one complete suit-and-rarity texture")
		return false
	if not String(visual.get("card_texture_path", "")).begins_with(
			"res://art/ui/adventure_upgrade_cards/"):
		_fail("Choice card did not use the promoted complete-card asset")
		return false
	return true


func _assert_restored(state: Dictionary, rest_position: Vector2) -> bool:
	if (state["position"] as Vector2).distance_to(rest_position) > 0.5 \
			or absf((state["scale"] as Vector2).x - REST_SCALE) > 0.01:
		_fail("Card did not restore position and scale")
		return false
	var visual := state["card_visual"] as Dictionary
	if absf(float(visual["shadow_offset_y"]) - 6.0) > 0.5 or float(visual["glow_alpha"]) > 0.03:
		_fail("Card did not restore shadow and glow: shadow=%s glow=%s" % [visual["shadow_offset_y"], visual["glow_alpha"]])
		return false
	return true


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_SELECTION_OVERLAY_HOVER_SMOKE_FAIL %s" % message)
	quit(1)
