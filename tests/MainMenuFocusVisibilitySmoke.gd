extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const SINGLE_PLAYER_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button"
const SINGLE_PLAYER_FRAME_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame"
const TIMEOUT_FRAMES := 120
const UNIFIED_BUTTON_TEXT := Color(243.0 / 255.0, 247.0 / 255.0, 244.0 / 255.0, 1.0)
const UNIFIED_DISABLED_BUTTON_TEXT := Color(158.0 / 255.0, 219.0 / 255.0, 213.0 / 255.0, 0.62)
const ENABLED_BUTTON_TEXT_COLOR_NAMES: Array[StringName] = [
	&"font_color",
	&"font_hover_color",
	&"font_focus_color",
	&"font_pressed_color",
	&"font_hover_pressed_color",
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	Input.warp_mouse(Vector2(0.0, 0.0))
	var menu := await _instantiate_menu()
	if menu == null:
		return
	if not await _audit_global_button_cursors(menu):
		return
	if not await _audit_pause_action_label_colors():
		return

	var single_player_button := menu.get_node_or_null(SINGLE_PLAYER_BUTTON_PATH) as Button
	var single_player_frame := menu.get_node_or_null(SINGLE_PLAYER_FRAME_PATH) as Control
	if single_player_button == null or single_player_frame == null:
		_fail("Main-menu single-player controls are missing")
		return
	if not single_player_button.has_focus():
		_fail("Single-player should retain the initial keyboard/gamepad focus target")
		return
	if bool(menu.call("DebugIsPrimaryMenuFocusHighlightVisible")):
		_fail("Main-menu focus highlight is visible before navigation input")
		return
	if not await _wait_for_glow(single_player_frame, 0.05, false):
		_fail("Single-player retained its selected glow in the initial pointer state")
		return

	var down_event := InputEventAction.new()
	down_event.action = "ui_down"
	down_event.pressed = true
	Input.parse_input_event(down_event)
	await process_frame
	down_event.pressed = false
	Input.parse_input_event(down_event)
	if not bool(menu.call("DebugIsPrimaryMenuFocusHighlightVisible")):
		_fail("Keyboard navigation did not reveal the main-menu focus highlight")
		return

	var focused_control := root.gui_get_focus_owner() as Control
	if focused_control == null or focused_control.get_parent() is not Control:
		_fail("Keyboard navigation did not retain a main-menu focus target")
		return
	var focused_frame := focused_control.get_parent() as Control
	if not await _wait_for_glow(focused_frame, 0.35, true):
		_fail("Focused main-menu row did not enter its keyboard highlight state")
		return

	Input.warp_mouse(Vector2(0.0, 0.0))
	var mouse_event := InputEventMouseMotion.new()
	mouse_event.position = Vector2(0.0, 0.0)
	mouse_event.global_position = Vector2(0.0, 0.0)
	Input.parse_input_event(mouse_event)
	await process_frame
	if bool(menu.call("DebugIsPrimaryMenuFocusHighlightVisible")):
		_fail("Pointer input did not hide the main-menu focus highlight")
		return
	if not await _wait_for_glow(focused_frame, 0.05, false):
		_fail("Focused main-menu row stayed selected after returning to pointer input")
		return

	print("MAIN_MENU_FOCUS_VISIBILITY_SMOKE_OK")
	if current_scene == menu:
		current_scene = null
	menu.queue_free()
	await _wait_frames(8)
	await _dispose_audio_before_quit()
	await _wait_frames(60)
	quit(0)


func _instantiate_menu() -> Node:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load main menu scene")
		return null

	var menu := packed_scene.instantiate()
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(4)
	return menu


func _audit_global_button_cursors(menu: Node) -> bool:
	var scene_button_count := _count_and_audit_button_cursors(menu)
	if scene_button_count < 10:
		_fail("Main menu cursor audit did not discover the expected button set")
		return false

	var probe_host := Control.new()
	probe_host.name = "DynamicButtonCursorProbes"
	var probes: Array[BaseButton] = [
		Button.new(),
		OptionButton.new(),
		CheckButton.new(),
		CheckBox.new(),
		MenuButton.new(),
		TextureButton.new(),
		LinkButton.new(),
	]
	for probe in probes:
		if probe is Button:
			(probe as Button).text = probe.get_class()
		elif probe is LinkButton:
			(probe as LinkButton).text = probe.get_class()
		probe_host.add_child(probe)
	root.add_child(probe_host)
	for probe in probes:
		for color_name in ENABLED_BUTTON_TEXT_COLOR_NAMES:
			probe.add_theme_color_override(color_name, Color(1.0, 0.75, 0.2, 1.0))
	await _wait_frames(2)

	var probe_count := _count_and_audit_button_cursors(probe_host)
	probe_host.queue_free()
	await process_frame
	if probe_count != probes.size():
		_fail("Dynamic cursor audit did not inspect every BaseButton subtype")
		return false
	return not _failed


func _count_and_audit_button_cursors(node: Node) -> int:
	var button_count := 0
	if node is BaseButton:
		button_count += 1
		var button := node as BaseButton
		var accepts_pointer := not button.disabled \
			and button.mouse_filter != Control.MOUSE_FILTER_IGNORE \
			and (button.button_mask & MOUSE_BUTTON_MASK_LEFT) != 0
		if accepts_pointer and button.mouse_default_cursor_shape != Control.CURSOR_POINTING_HAND:
			_fail("Button does not use the shared pointing-hand cursor: %s" % button.get_path())
			return button_count
		if not _audit_button_text_colors(button):
			return button_count
	for child in node.get_children():
		button_count += _count_and_audit_button_cursors(child)
		if _failed:
			return button_count
	return button_count


func _audit_button_text_colors(button: BaseButton) -> bool:
	var button_text := ""
	if button is Button:
		button_text = (button as Button).text
	elif button is LinkButton:
		button_text = (button as LinkButton).text
	if button_text.strip_edges().is_empty():
		return true

	for color_name in ENABLED_BUTTON_TEXT_COLOR_NAMES:
		var actual := button.get_theme_color(color_name)
		if not actual.is_equal_approx(UNIFIED_BUTTON_TEXT):
			_fail("Button text color is inconsistent for %s (%s): %s" % [button.get_path(), color_name, actual])
			return false
	var disabled_color := button.get_theme_color(&"font_disabled_color")
	if not disabled_color.is_equal_approx(UNIFIED_DISABLED_BUTTON_TEXT):
		_fail("Button disabled text color is inconsistent for %s: %s" % [button.get_path(), disabled_color])
		return false
	return true


func _audit_pause_action_label_colors() -> bool:
	var packed_pause := load("res://scenes/pause_menu.tscn") as PackedScene
	if packed_pause == null:
		_fail("Failed to load pause menu for button-label color audit")
		return false
	var pause_menu := packed_pause.instantiate()
	root.add_child(pause_menu)
	await _wait_frames(2)
	for label_name in [&"ContinueLabel", &"ReturnToMainMenuLabel", &"RestartLabel"]:
		var label := pause_menu.get_node_or_null(NodePath("%%%s" % label_name)) as Label
		if label == null:
			_fail("Pause action label is missing: %s" % label_name)
			return false
		if not label.get_theme_color(&"font_color").is_equal_approx(UNIFIED_BUTTON_TEXT):
			_fail("Pause action label color is inconsistent: %s" % label.get_path())
			return false
	pause_menu.queue_free()
	await process_frame
	return true


func _wait_for_glow(frame: Control, threshold: float, at_least: bool) -> bool:
	for _frame in range(TIMEOUT_FRAMES):
		var glow := float(frame.call("DebugGetGlow"))
		if (at_least and glow >= threshold) or (not at_least and glow <= threshold):
			return true
		await process_frame
	return false


func _wait_frames(count: int) -> void:
	for _frame in range(count):
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
	print("MAIN_MENU_FOCUS_VISIBILITY_SMOKE_FAIL %s" % message)
	quit(1)
