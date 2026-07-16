extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_FRAMES := 240
const REST_SCALE := 0.94
const ACTIVE_SCALE := 1.0
const ACTIVE_LIFT := 12.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return
	var board := packed_scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	await process_frame

	for method in [
		"DebugShowAdventureUpgradeSelectionOverlaySample",
		"DebugSetAdventureUpgradeHoverState",
		"DebugSetAdventureUpgradeFocusState",
		"DebugGetAdventureUpgradeOverlayMotionState",
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
	var baseline_perspective := {}
	for option_index in range(3):
		var state := _motion_state(board, option_index)
		if state.is_empty():
			_fail("Missing motion state for option %d" % option_index)
			return
		var pseudo := state["pseudo_3d"] as Dictionary
		if not _assert_perspective_state(pseudo):
			return
		if option_index == 0:
			baseline_perspective = _perspective_signature(pseudo)
		elif _perspective_signature(pseudo) != baseline_perspective:
			_fail("All three cards must use identical perspective parameters")
			return
		if absf((state["scale"] as Vector2).x - REST_SCALE) > 0.015:
			_fail("Card did not settle to rest scale")
			return
		if absf(float(state["current_elevation"])) > 0.02:
			_fail("Resting card retained elevation")
			return

	var rest_state := _motion_state(board, 1)
	var rest_position := rest_state["position"] as Vector2
	var rest_perspective := _perspective_signature(rest_state["pseudo_3d"] as Dictionary)

	# Mouse hover activation.
	if not bool(board.call("DebugSetAdventureUpgradeHoverState", 1, true)):
		_fail("Failed to activate hover")
		return
	await process_frame
	var hover_step := _motion_state(board, 1)
	if not ((hover_step["position"] as Vector2).y < rest_position.y) \
			or not ((hover_step["scale"] as Vector2).x > REST_SCALE) \
			or float(hover_step["current_elevation"]) <= 0.0 \
			or float(hover_step["current_elevation"]) >= 1.0:
		_fail("Hover activation did not ease position scale and elevation")
		return
	await _wait_frames(24)
	var hover_settled := _motion_state(board, 1)
	if absf((hover_settled["position"] as Vector2).y - (rest_position.y - ACTIVE_LIFT)) > 0.5:
		_fail("Hover card did not settle 12px above rest")
		return
	if absf((hover_settled["scale"] as Vector2).x - ACTIVE_SCALE) > 0.01:
		_fail("Hover card did not settle at scale 1.0")
		return
	if float(hover_settled["current_elevation"]) < 0.98:
		_fail("Hover card did not reach full elevation")
		return
	if int(hover_settled["frame_z_index"]) <= 0 or int(hover_settled["button_z_index"]) <= 0:
		_fail("Hover card did not rise above sibling Z indices")
		return
	var hover_pseudo := hover_settled["pseudo_3d"] as Dictionary
	if float(hover_pseudo["shadow_offset_y"]) < 15.5 \
			or float(hover_pseudo["glow_alpha"]) <= 0.5:
		_fail("Hover card shadow compensation or glow is incomplete")
		return
	if _perspective_signature(hover_pseudo) != rest_perspective:
		_fail("Hover changed fixed perspective parameters")
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
			or absf((focus_settled["scale"] as Vector2).x - ACTIVE_SCALE) > 0.01 \
			or float(focus_settled["current_elevation"]) < 0.98:
		_fail("Focus does not match hover activation")
		return
	var focus_pseudo := focus_settled["pseudo_3d"] as Dictionary
	if float(focus_pseudo["glow_alpha"]) <= 0.5 \
			or _perspective_signature(focus_pseudo) != rest_perspective:
		_fail("Focus glow or perspective does not match hover")
		return

	board.call("DebugSetAdventureUpgradeFocusState", 1, false)
	if not await _wait_for_restored(board, 1, rest_position):
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
		var pseudo := state["pseudo_3d"] as Dictionary
		if (state["position"] as Vector2).distance_to(rest_position) <= 0.5 \
				and absf((state["scale"] as Vector2).x - REST_SCALE) <= 0.01 \
				and absf(float(state["current_elevation"])) <= 0.02 \
				and absf(float(pseudo["shadow_offset_y"]) - 8.0) <= 0.5 \
				and float(pseudo["glow_alpha"]) <= 0.03:
			return true
	return _assert_restored(_motion_state(board, option_index), rest_position)


func _assert_perspective_state(pseudo: Dictionary) -> bool:
	if String(pseudo.get("shader_path", "")) != "res://shaders/adventure_upgrade_card_perspective.gdshader":
		_fail("Card surface is missing the perspective shader")
		return false
	if absf(float(pseudo["vertical_compression"]) - 0.96) > 0.001 \
			or absf(float(pseudo["top_width_ratio"]) - 0.97) > 0.001 \
			or absf(float(pseudo["bottom_width_ratio"]) - 1.0) > 0.001 \
			or absf(float(pseudo["plane_rotation_degrees"])) > 0.001:
		_fail("Perspective shader parameters do not match the fixed specification")
		return false
	if bool(pseudo["description_has_material"]):
		_fail("Description layer must not inherit perspective material")
		return false
	if int(pseudo["thickness_layer_count"]) != 3:
		_fail("Choice art must expose three thickness layers")
		return false
	if int(pseudo["surface_material_id"]) == 0 \
			or not bool(pseudo.get("glow_uses_outline_panel", false)):
		_fail("Card surface or outline glow is missing")
		return false
	if not bool(pseudo.get("surface_has_texture", false)) \
			or not bool(pseudo.get("suit_has_texture", false)) \
			or not bool(pseudo.get("suit_is_separate_layer", false)):
		_fail("Choice card must compose separate generated base and suit textures")
		return false
	if String(pseudo.get("card_texture_path", "")) != \
			"res://art/ui/adventure_upgrade_choice_table/run-001/base/processed/raw-sheet-clean.png":
		_fail("Choice card reused the skill-codex atlas instead of its generated table base")
		return false
	var suit_texture_path := String(pseudo.get("suit_texture_path", ""))
	if not suit_texture_path.begins_with(
			"res://art/ui/adventure_upgrade_choice_table/run-001/suits/processed/suit-"):
		_fail("Choice suit badge is not loaded from the generated independent suit set")
		return false
	if float(pseudo.get("suit_shadow_alpha", 0.0)) < 0.25:
		_fail("Choice suit badge is missing its independent contact-shadow layer")
		return false
	var suit_badge_rect := pseudo.get("suit_badge_rect", {}) as Dictionary
	if suit_badge_rect.is_empty() \
			or float(suit_badge_rect.get("width", 0.0)) < 120.0 \
			or float(suit_badge_rect.get("height", 0.0)) < 140.0:
		_fail("Choice suit badge is too small to reproduce the raised reference emblem")
		return false
	return true


func _perspective_signature(pseudo: Dictionary) -> Dictionary:
	return {
		"shader_path": pseudo["shader_path"],
		"vertical_compression": pseudo["vertical_compression"],
		"top_width_ratio": pseudo["top_width_ratio"],
		"bottom_width_ratio": pseudo["bottom_width_ratio"],
		"plane_rotation_degrees": pseudo["plane_rotation_degrees"],
	}


func _assert_restored(state: Dictionary, rest_position: Vector2) -> bool:
	if (state["position"] as Vector2).distance_to(rest_position) > 0.5 \
			or absf((state["scale"] as Vector2).x - REST_SCALE) > 0.01 \
			or absf(float(state["current_elevation"])) > 0.02:
		_fail("Card did not restore position scale and elevation")
		return false
	var pseudo := state["pseudo_3d"] as Dictionary
	if absf(float(pseudo["shadow_offset_y"]) - 8.0) > 0.5 or float(pseudo["glow_alpha"]) > 0.03:
		_fail("Card did not restore shadow and glow: shadow=%s glow=%s elevation=%s" % [pseudo["shadow_offset_y"], pseudo["glow_alpha"], state["current_elevation"]])
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
