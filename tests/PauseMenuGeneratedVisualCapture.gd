extends SceneTree

const GAME_BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SPECS := [
	{"name": "960x540", "size": Vector2i(960, 540)},
	{"name": "1280x720", "size": Vector2i(1280, 720)},
	{"name": "1920x1080", "size": Vector2i(1920, 1080)},
]

var _failed := false
var _output_dir := ""


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_output_dir = OS.get_environment("PAUSE_MENU_SCREENSHOT_DIR").strip_edges()
	if _output_dir.is_empty():
		_fail("PAUSE_MENU_SCREENSHOT_DIR is required")
		return
	var make_dir_error := DirAccess.make_dir_recursive_absolute(_output_dir)
	if make_dir_error != OK:
		_fail("Failed to create screenshot directory: %s" % error_string(make_dir_error))
		return

	for viewport_spec in VIEWPORT_SPECS:
		if not await _capture_viewport(viewport_spec):
			return

	print("PAUSE_MENU_GENERATED_VISUAL_CAPTURE_OK %s" % _output_dir)
	await _dispose_audio_before_quit()
	quit(0)


func _capture_viewport(viewport_spec: Dictionary) -> bool:
	var viewport := SubViewport.new()
	viewport.size = viewport_spec.size
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.gui_embed_subwindows = true
	root.add_child(viewport)

	var packed_scene := load(GAME_BOARD_SCENE_PATH) as PackedScene
	var board := packed_scene.instantiate() if packed_scene != null else null
	if board == null:
		_fail("Failed to instantiate game board")
		return false
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", false)
	board.set("network_session_id", "")
	viewport.add_child(board)
	await _wait_frames(4)
	_press_escape(viewport)

	var pause_layer := await _wait_for_node(board, "PauseMenuLayer")
	var pause_root := pause_layer.get_node_or_null("PauseMenuRoot") as Control if pause_layer != null else null
	if pause_root == null or not await _wait_for_visible(pause_root):
		_fail("Pause menu did not open at %s" % viewport_spec.name)
		return false
	await _wait_frames(2)
	var panel := pause_root.find_child("PauseMenuPanel", true, false) as Control
	var continue_button := pause_root.find_child("ContinueButton", true, false) as Button
	var return_button := pause_root.find_child("ReturnToMainMenuButton", true, false) as Button
	var restart_button := pause_root.find_child("RestartButton", true, false) as Button
	var continue_frame := pause_root.find_child("ContinueFrame", true, false) as Control
	var return_frame := pause_root.find_child("ReturnToMainMenuFrame", true, false) as Control
	var restart_frame := pause_root.find_child("RestartFrame", true, false) as Control
	if panel == null or continue_button == null or return_button == null or restart_button == null \
			or continue_frame == null or return_frame == null or restart_frame == null:
		_fail("Pause menu is missing its generated panel or action controls")
		return false
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport.size))
	var panel_rect := panel.get_global_rect()
	if not _rect_contains(viewport_rect, panel_rect, 2.0):
		_fail("Pause panel escapes %s: %s" % [viewport_spec.name, panel.get_global_rect()])
		return false
	if panel_rect.get_center().distance_to(viewport_rect.get_center()) > 2.0:
		_fail("Pause panel is not centered at %s: %s" % [viewport_spec.name, panel_rect])
		return false
	await _wait_frames(4)
	if float(continue_frame.call("DebugGetGlow")) > 0.08:
		_fail("Default pause focus is visible before pointer or navigation input at %s" % viewport_spec.name)
		return false

	_push_mouse_motion(viewport, return_button.get_global_rect().get_center())
	await _wait_frames(2)
	if not return_button.has_focus():
		_fail("Pointer hover did not transfer pause-menu focus at %s" % viewport_spec.name)
		return false
	if float(continue_frame.call("DebugGetGlow")) > 0.08:
		_fail("Pause menu kept two selected rows after pointer focus moved at %s" % viewport_spec.name)
		return false
	if not await _wait_for_glow(return_frame, 0.5):
		_fail("Pointer-focused pause row did not render its selection state at %s" % viewport_spec.name)
		return false

	_push_mouse_motion(viewport, Vector2.ZERO)
	continue_button.release_focus()
	return_button.release_focus()
	restart_button.release_focus()
	await _wait_frames(24)
	var idle_image := await _capture_image(viewport, "idle", viewport_spec.name)
	if idle_image == null:
		return false

	_push_action(viewport, "ui_down")
	await _wait_frames(2)
	restart_button.grab_focus()
	if not await _wait_for_glow(restart_frame, 0.9):
		_fail("Restart button did not render its focused state")
		return false
	var focus_image := await _capture_image(viewport, "focus", viewport_spec.name)
	if focus_image == null or not _has_visible_change(idle_image, focus_image, restart_frame.get_global_rect()):
		_fail("Generated focus texture did not visibly change at %s" % viewport_spec.name)
		return false

	var button_center := restart_button.get_global_rect().get_center()
	_push_mouse_button(viewport, button_center, true)
	await _wait_frames(3)
	var pressed_image := await _capture_image(viewport, "pressed", viewport_spec.name)
	if pressed_image == null or not _has_visible_change(focus_image, pressed_image, restart_frame.get_global_rect()):
		_fail("Generated pressed texture did not visibly change at %s" % viewport_spec.name)
		return false
	_push_mouse_button(viewport, button_center, false)
	paused = false

	board.queue_free()
	viewport.queue_free()
	await process_frame
	return true


func _capture_image(viewport: SubViewport, state_name: String, viewport_name: String) -> Image:
	await _wait_frames(4)
	await RenderingServer.frame_post_draw
	var image := viewport.get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport.size:
		_fail("%s %s screenshot is empty or wrong-sized" % [state_name, viewport_name])
		return null
	var output_path := _output_dir.path_join("pause-%s-%s.png" % [state_name, viewport_name])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s: %s" % [output_path, error_string(save_error)])
		return null
	return image


func _has_visible_change(before: Image, after: Image, area: Rect2) -> bool:
	var bounds := Rect2i(area).intersection(Rect2i(Vector2i.ZERO, before.get_size()))
	var total_delta := 0.0
	var sample_count := 0
	for x in range(bounds.position.x, bounds.end.x, 8):
		for y in range(bounds.position.y, bounds.end.y, 6):
			var left := before.get_pixel(x, y)
			var right := after.get_pixel(x, y)
			total_delta += absf(left.r - right.r) + absf(left.g - right.g) + absf(left.b - right.b)
			sample_count += 1
	return sample_count > 0 and total_delta / sample_count > 0.025


func _press_escape(viewport: SubViewport) -> void:
	var press := InputEventKey.new()
	press.keycode = KEY_ESCAPE
	press.physical_keycode = KEY_ESCAPE
	press.pressed = true
	viewport.push_input(press, true)
	var release := press.duplicate() as InputEventKey
	release.pressed = false
	viewport.push_input(release, true)


func _push_mouse_button(viewport: SubViewport, position: Vector2, is_pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.global_position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = is_pressed
	viewport.push_input(event, true)


func _push_mouse_motion(viewport: SubViewport, position: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = position
	event.global_position = position
	viewport.push_input(event, true)


func _push_action(viewport: SubViewport, action: StringName) -> void:
	var press := InputEventAction.new()
	press.action = action
	press.pressed = true
	viewport.push_input(press, true)
	var release := press.duplicate() as InputEventAction
	release.pressed = false
	viewport.push_input(release, true)


func _wait_for_node(parent: Node, path: NodePath, max_frames := 180) -> Node:
	for _frame in max_frames:
		var node := parent.get_node_or_null(path)
		if node != null:
			return node
		await process_frame
	return parent.get_node_or_null(path)


func _wait_for_visible(control: Control, max_frames := 180) -> bool:
	for _frame in max_frames:
		if control.visible and control.modulate.a >= 0.98:
			return true
		await process_frame
	return control.visible and control.modulate.a >= 0.98


func _wait_for_glow(frame: Control, minimum_glow: float, max_frames := 120) -> bool:
	for _frame in max_frames:
		if float(frame.call("DebugGetGlow")) >= minimum_glow:
			return true
		await process_frame
	return float(frame.call("DebugGetGlow")) >= minimum_glow


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _rect_contains(outer: Rect2, inner: Rect2, tolerance: float) -> bool:
	return inner.position.x >= outer.position.x - tolerance \
		and inner.position.y >= outer.position.y - tolerance \
		and inner.end.x <= outer.end.x + tolerance \
		and inner.end.y <= outer.end.y + tolerance


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error("PAUSE_MENU_GENERATED_VISUAL_CAPTURE_FAIL %s" % message)
	quit(1)
