extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const SINGLE_PLAYER_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button"
const PLAYER_COUNT_ROOT_PATH := "PlayerCountDialogLayer/PlayerCountDialogRoot"
const VIEWPORT_SIZE := Vector2i(1280, 720)

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if DisplayServer.get_name().to_lower() == "headless":
		_fail("Pointer interaction smoke requires a GUI window")
		return

	root.size = VIEWPORT_SIZE
	await _wait_frames(4)
	var menu := await _instantiate_menu()
	if menu == null:
		return

	var single_player_button := menu.get_node_or_null(SINGLE_PLAYER_BUTTON_PATH) as Button
	if single_player_button == null:
		_fail("Single-player main-menu button is missing")
		return
	single_player_button.emit_signal("pressed")

	var dialog_root := menu.get_node_or_null(PLAYER_COUNT_ROOT_PATH) as Control
	if dialog_root == null or not await _wait_for_opaque(dialog_root):
		_fail("Single-player dialog did not open")
		return
	await _wait_frames(12)

	var difficulty_row := dialog_root.find_child("DifficultyRowPanel", true, false) as Control
	var difficulty_option := dialog_root.find_child("DifficultyOption", true, false) as OptionButton
	var difficulty_label := dialog_root.find_child("DifficultyLabel", true, false) as Label
	if difficulty_row == null or difficulty_option == null or difficulty_label == null:
		_fail("Difficulty row controls are missing")
		return
	var difficulty_row_rect := difficulty_row.get_global_rect()
	var difficulty_option_rect := difficulty_option.get_global_rect()
	if not difficulty_row_rect.encloses(difficulty_option_rect) \
			or difficulty_option_rect.position.x < difficulty_row_rect.position.x + difficulty_row_rect.size.x * 0.50 \
			or difficulty_option_rect.size.y < 44.0:
		_fail("Difficulty selector is not confined to the right-side value segment")
		return

	var label_click_position := difficulty_label.get_global_rect().get_center()
	if difficulty_option_rect.has_point(label_click_position):
		_fail("Difficulty label test point overlaps the compact dropdown")
		return
	_move_pointer(label_click_position)
	await _wait_frames(4)
	if difficulty_option.is_hovered():
		_fail("Left-side difficulty label unexpectedly hovers the dropdown")
		return

	_click_pointer(label_click_position)
	var difficulty_popup := difficulty_option.get_popup()
	await _wait_frames(4)
	if difficulty_popup == null or difficulty_popup.visible:
		_fail("Clicking the left-side difficulty label unexpectedly opened the dropdown")
		return

	var value_click_position := difficulty_option_rect.get_center()
	_move_pointer(value_click_position)
	if not await _wait_for_glow(difficulty_row, 0.35):
		_fail("Moving over the compact difficulty selector did not highlight its row")
		return
	_click_pointer(value_click_position)
	if not await _wait_for_popup_visible(difficulty_popup):
		_fail("Clicking the compact difficulty selector did not open the dropdown")
		return
	await _wait_frames(2)
	for icon_name in ["checked", "radio_checked", "unchecked", "radio_unchecked"]:
		var popup_icon := difficulty_popup.get_theme_icon(icon_name, "PopupMenu")
		if popup_icon != null and (popup_icon.get_width() > 1 or popup_icon.get_height() > 1):
			_fail("Difficulty popup unexpectedly shows a prefix icon: %s" % icon_name)
			return
	var difficulty_popup_rect := Rect2(Vector2(difficulty_popup.position), Vector2(difficulty_popup.size))
	if absf(difficulty_popup_rect.position.x - difficulty_option_rect.position.x) > 3.0 \
			or absf(difficulty_popup_rect.size.x - difficulty_option_rect.size.x) > 3.0:
		_fail("Difficulty popup is not aligned to the right-side value segment (popup=%s, option=%s)" % [
			difficulty_popup_rect,
			difficulty_option_rect,
		])
		return
	difficulty_popup.hide()
	await _wait_frames(3)

	var back_frame := dialog_root.find_child("BackFrame", true, false) as Control
	var back_button := dialog_root.find_child("BackButton", true, false) as Button
	if back_frame == null or back_button == null:
		_fail("Single-player back control is missing")
		return
	var back_position := back_button.get_global_rect().get_center()
	_move_pointer(back_position)
	if not await _wait_for_glow(back_frame, 0.35):
		_fail("Moving over the back button did not show its hover style")
		return
	if not back_button.is_hovered():
		_fail("Back button did not receive pointer hover")
		return

	_click_pointer(back_position)
	if not await _wait_for_hidden(dialog_root):
		_fail("Clicking the back button did not close the single-player dialog")
		return
	if not await _wait_for_menu_buttons_enabled(menu):
		_fail("Main-menu buttons did not unlock after closing the dialog")
		return

	print("MAIN_MENU_PLAYER_COUNT_POINTER_INTERACTION_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _instantiate_menu() -> Node:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load the main-menu scene")
		return null
	var menu := packed_scene.instantiate()
	if menu == null:
		_fail("Failed to instantiate the main-menu scene")
		return null
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(4)
	return menu


func _move_pointer(position: Vector2) -> void:
	var previous_position := root.get_mouse_position()
	Input.warp_mouse(position)
	var motion := InputEventMouseMotion.new()
	motion.position = position
	motion.global_position = position
	motion.relative = position - previous_position
	Input.parse_input_event(motion)


func _click_pointer(position: Vector2) -> void:
	var press := InputEventMouseButton.new()
	press.position = position
	press.global_position = position
	press.button_index = MOUSE_BUTTON_LEFT
	press.button_mask = MOUSE_BUTTON_MASK_LEFT
	press.pressed = true
	Input.parse_input_event(press)

	var release := InputEventMouseButton.new()
	release.position = position
	release.global_position = position
	release.button_index = MOUSE_BUTTON_LEFT
	release.button_mask = 0
	release.pressed = false
	Input.parse_input_event(release)


func _wait_for_glow(frame: Control, minimum_glow: float, max_frames := 120) -> bool:
	for _frame in max_frames:
		if float(frame.call("DebugGetGlow")) >= minimum_glow:
			return true
		await process_frame
	return float(frame.call("DebugGetGlow")) >= minimum_glow


func _wait_for_popup_visible(popup: PopupMenu, max_frames := 120) -> bool:
	for _frame in max_frames:
		if popup.visible:
			return true
		await process_frame
	return popup.visible


func _wait_for_opaque(control: Control, max_frames := 180) -> bool:
	for _frame in max_frames:
		if control.visible and control.modulate.a >= 0.98:
			return true
		await process_frame
	return control.visible and control.modulate.a >= 0.98


func _wait_for_hidden(control: Control, max_frames := 180) -> bool:
	for _frame in max_frames:
		if not control.visible:
			return true
		await process_frame
	return not control.visible


func _wait_for_menu_buttons_enabled(menu: Node, max_frames := 240) -> bool:
	for _frame in max_frames:
		if not bool(menu.call("DebugAreMainMenuButtonsDisabled")):
			return true
		await process_frame
	return not bool(menu.call("DebugAreMainMenuButtonsDisabled"))


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _rects_close(left: Rect2, right: Rect2, tolerance := 2.0) -> bool:
	return left.position.distance_to(right.position) <= tolerance \
		and left.size.distance_to(right.size) <= tolerance


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
	push_error("MAIN_MENU_PLAYER_COUNT_POINTER_INTERACTION_SMOKE_FAIL %s" % message)
	quit(1)
