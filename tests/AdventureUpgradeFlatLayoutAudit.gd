extends SceneTree

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SIZES := [Vector2i(1280, 720), Vector2i(960, 900), Vector2i(1680, 720)]
const TIMEOUT_SECONDS := 5.0
const PIXEL_DIFFERENCE_THRESHOLD := 0.08

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if DisplayServer.get_name().to_lower() == "headless":
		_fail("Flat-card layout audit requires a GUI renderer")
		return
	for viewport_size in VIEWPORT_SIZES:
		root.size = viewport_size
		await _wait_frames(3)
		var board := await _create_board()
		if _failed or board == null:
			return
		if not bool(board.call("DebugShowAdventureUpgradeSelectionOverlaySample")):
			_fail("Failed to show flat-card overlay sample")
			return
		if not await _wait_for_unlock(board):
			return

		var overlay := board.find_child("AdventureUpgradeSelectionOverlay", true, false) as Control
		if overlay == null:
			_fail("Missing adventure upgrade overlay")
			return
		if not _assert_layout(overlay, viewport_size):
			return

		Input.warp_mouse(Vector2.ZERO)
		await _wait_frames(2)
		for option_index in range(3):
			board.call("DebugSetAdventureUpgradeHoverState", option_index, false)
			board.call("DebugSetAdventureUpgradeFocusState", option_index, false)
		await _wait_frames(36)
		var rest_image := root.get_texture().get_image()
		if not _assert_nonblank_image(rest_image, viewport_size, "rest"):
			return

		if not bool(board.call("DebugSetAdventureUpgradeFocusState", 2, true)):
			_fail("Failed to focus the third flat card")
			return
		if not await _wait_for_activation(board, 2):
			return
		var active_state := board.call("DebugGetAdventureUpgradeOverlayMotionState", 2) as Dictionary
		var visual := active_state["card_visual"] as Dictionary
		if not bool(visual.get("surface_is_flat", false)) \
				or int(visual.get("thickness_layer_count", -1)) != 0 \
				or not String(visual.get("surface_shader_path", "")).is_empty() \
				or float(visual["glow_alpha"]) < 0.5 \
				or not bool(visual["surface_has_texture"]) \
				or not bool(visual.get("uses_complete_card_texture", false)) \
				or bool(visual.get("suit_is_separate_layer", true)):
			_fail("Active card is not using the required flat presentation")
			return
		if absf(float(active_state.get("rotation_degrees", 1.0))) > 0.01:
			_fail("Active card is not front-facing")
			return

		var active_image := root.get_texture().get_image()
		if not _assert_nonblank_image(active_image, viewport_size, "active"):
			return
		var third_button := overlay.get_node("Margin/Layout/CardsRow/Option3Button") as Control
		var pixel_region := _to_pixel_rect(third_button.get_global_rect().grow(20.0), overlay, active_image.get_size())
		var changed_pixel_count := _count_changed_pixels(rest_image, active_image, pixel_region)
		if not _save_optional_screenshot(active_image, viewport_size):
			return
		if changed_pixel_count < 500:
			_fail("Active third card changed only %d pixels at %s" % [changed_pixel_count, viewport_size])
			return

		board.queue_free()
		await _wait_frames(6)

	print("ADVENTURE_UPGRADE_FLAT_LAYOUT_AUDIT_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _create_board() -> Node:
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return null
	var board := packed_scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	await _wait_frames(2)
	return board


func _wait_for_unlock(board: Node) -> bool:
	var final_states: Array[Dictionary] = []
	var deadline_msec := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline_msec:
		await process_frame
		var ready := true
		final_states.clear()
		for option_index in range(3):
			var state := board.call("DebugGetAdventureUpgradeOverlayMotionState", option_index) as Dictionary
			final_states.append(state)
			if state.is_empty() \
					or not bool(state.get("reveal_completed", false)) \
					or not bool(state.get("interaction_unlocked", false)):
				ready = false
		if ready:
			return true
	_fail("Timed out waiting for flat-card overlay reveal states=%s" % [final_states])
	return false


func _wait_for_activation(board: Node, option_index: int) -> bool:
	var final_state := {}
	var deadline_msec := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline_msec:
		await process_frame
		final_state = board.call("DebugGetAdventureUpgradeOverlayMotionState", option_index) as Dictionary
		var visual := final_state.get("card_visual", {}) as Dictionary
		if float(visual.get("highlight", 0.0)) >= 0.95:
			return true
	_fail("Timed out waiting for flat-card activation state=%s" % [final_state])
	return false


func _assert_layout(overlay: Control, viewport_size: Vector2i) -> bool:
	var backdrop := overlay.get_node("TableBackdrop") as TextureRect
	var overlay_rect := overlay.get_global_rect()
	if backdrop.get_global_rect().position.distance_to(overlay_rect.position) > 1.0 \
			or backdrop.get_global_rect().size.distance_to(overlay_rect.size) > 2.0 \
			or backdrop.stretch_mode != TextureRect.STRETCH_KEEP_ASPECT_COVERED:
		_fail("Table backdrop does not fully cover %s rect=%s overlay=%s" % [viewport_size, backdrop.get_global_rect(), overlay_rect])
		return false

	var button_rects: Array[Rect2] = []
	for option_index in range(3):
		var button := overlay.get_node("Margin/Layout/CardsRow/Option%dButton" % (option_index + 1)) as Control
		var rect := button.get_global_rect()
		if rect.position.x < overlay_rect.position.x - 1.0 or rect.position.y < overlay_rect.position.y - 1.0 \
				or rect.end.x > overlay_rect.end.x + 1.0 or rect.end.y > overlay_rect.end.y + 1.0:
			_fail("Flat card leaves viewport at %s" % viewport_size)
			return false
		for existing in button_rects:
			if rect.intersects(existing):
				_fail("Flat cards overlap at %s" % viewport_size)
				return false
		button_rects.append(rect)

		var art := button.get_node("CardFrame/ChoiceArt") as Control
		var card_rect_data := art.call("DebugGetCardDrawRect") as Dictionary
		var description_data := art.call("DebugGetDescriptionBand") as Dictionary
		var card_rect := Rect2(
			Vector2(float(card_rect_data["x"]), float(card_rect_data["y"])),
			Vector2(float(card_rect_data["width"]), float(card_rect_data["height"])))
		var description_rect := Rect2(
			Vector2(float(description_data["x"]), float(description_data["y"])),
			Vector2(float(description_data["width"]), float(description_data["height"])))
		if not card_rect.encloses(description_rect) \
				or description_rect.position.x - card_rect.position.x < 8.0 \
				or card_rect.end.x - description_rect.end.x < 8.0 \
				or card_rect.end.y - description_rect.end.y < card_rect.size.y * 0.10:
			_fail("Description touches the card frame at %s" % viewport_size)
			return false
	return true


func _assert_nonblank_image(image: Image, viewport_size: Vector2i, state_name: String) -> bool:
	if image == null or image.is_empty() or image.get_size() != viewport_size:
		_fail("%s screenshot is empty or wrong-sized at %s" % [state_name, viewport_size])
		return false
	var used := image.get_used_rect()
	if used.size.x < viewport_size.x * 0.9 or used.size.y < viewport_size.y * 0.9:
		_fail("%s screenshot does not contain full-frame pixels at %s" % [state_name, viewport_size])
		return false
	return true


func _count_changed_pixels(before: Image, after: Image, region: Rect2) -> int:
	var bounds := Rect2i(Vector2i.ZERO, before.get_size())
	var sample := Rect2i(
		Vector2i(floori(region.position.x), floori(region.position.y)),
		Vector2i(ceili(region.size.x), ceili(region.size.y))).intersection(bounds)
	var changed := 0
	for y in range(sample.position.y, sample.end.y):
		for x in range(sample.position.x, sample.end.x):
			var a := before.get_pixel(x, y)
			var b := after.get_pixel(x, y)
			if absf(a.r - b.r) + absf(a.g - b.g) + absf(a.b - b.b) + absf(a.a - b.a) > PIXEL_DIFFERENCE_THRESHOLD:
				changed += 1
	return changed


func _to_pixel_rect(logical_rect: Rect2, overlay: Control, image_size: Vector2i) -> Rect2:
	var overlay_rect := overlay.get_global_rect()
	var scale := Vector2(image_size) / overlay_rect.size
	return Rect2((logical_rect.position - overlay_rect.position) * scale, logical_rect.size * scale)


func _save_optional_screenshot(image: Image, viewport_size: Vector2i) -> bool:
	var output_dir := OS.get_environment("FLAT_CARD_SCREENSHOT_DIR").strip_edges()
	if output_dir.is_empty():
		return true
	var output_path := output_dir.path_join("adventure-upgrade-flat-%dx%d.png" % [viewport_size.x, viewport_size.y])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save flat-card screenshot: %s" % error_string(save_error))
		return false
	return true


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await _wait_frames(2)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_FLAT_LAYOUT_AUDIT_FAIL %s" % message)
	quit(1)
