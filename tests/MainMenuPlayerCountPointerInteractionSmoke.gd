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
	var split_rows := {
		"PlayerCountOption": ["PlayersRowPanel", "PlayersLabel"],
		"DifficultyOption": ["DifficultyRowPanel", "DifficultyLabel"],
		"ItemModeOption": ["ItemModeRowPanel", "ItemModeLabel"],
		"WinningScoreOption": ["WinningScoreRowPanel", "WinningScoreLabel"],
	}
	for option_name in split_rows:
		var option := dialog_root.find_child(option_name, true, false) as OptionButton
		var row_data: Array = split_rows[option_name]
		var row := dialog_root.find_child(row_data[0], true, false) as Control
		var label := dialog_root.find_child(row_data[1], true, false) as Label
		var accent := row.get_node_or_null("LabelAccent") as ColorRect if row != null else null
		if option == null or row == null or label == null or accent == null:
			_fail("Split configuration row is incomplete: %s" % option_name)
			return
		if accent.visible:
			_fail("Configuration field label still uses a competing decorative accent: %s" % option_name)
			return
		if option.flat or not bool(row.get("SuppressVisuals")):
			_fail("Configuration selector still merges its interaction state into the full row: %s" % option_name)
			return
		var arrow_icon := option.get_theme_icon("arrow")
		if arrow_icon == null or arrow_icon.get_width() > 1 or arrow_icon.get_height() > 1:
			_fail("Configuration option still exposes a duplicate native arrow: %s" % option_name)
			return
		var option_style := option.get_theme_stylebox("normal") as StyleBoxFlat
		var focus_style := option.get_theme_stylebox("focus") as StyleBoxFlat
		if option_style == null or focus_style == null \
				or option_style.bg_color.a < 0.8 \
				or option_style.border_width_left < 1 \
				or focus_style.border_color.is_equal_approx(option_style.border_color) \
				or option_style.get_content_margin(SIDE_RIGHT) < 44.0 \
				or option.alignment != HORIZONTAL_ALIGNMENT_LEFT:
			_fail("Configuration option is missing its independent selector surface: %s" % option_name)
			return
		var label_rect := label.get_global_rect()
		var option_rect := option.get_global_rect()
		var row_rect := row.get_global_rect()
		if not row_rect.encloses(label_rect) or not row_rect.encloses(option_rect) \
				or label_rect.end.y > option_rect.position.y - 8.0 \
				or option_rect.size.x < row_rect.size.x - 20.0 \
				or option_rect.size.y < 44.0 \
				or label.get_theme_font_size("font_size") > 16 \
				or option.get_theme_font_size("font_size") < 20:
			_fail("Configuration cell does not keep a clear title-over-selector hierarchy: %s row=%s label=%s option=%s" % [
				option_name,
				row_rect,
				label_rect,
				option_rect,
			])
			return
	var difficulty_row_rect := difficulty_row.get_global_rect()
	var difficulty_option_rect := difficulty_option.get_global_rect()
	if not difficulty_row_rect.encloses(difficulty_option_rect) \
			or difficulty_option_rect.size.x < difficulty_row_rect.size.x * 0.88 \
			or difficulty_option_rect.size.y < 44.0:
		_fail("Difficulty selector does not fill its stack row")
		return
	var players_row_rect := (dialog_root.find_child("PlayersRowPanel", true, false) as Control).get_global_rect()
	var item_mode_row_rect := (dialog_root.find_child("ItemModeRowPanel", true, false) as Control).get_global_rect()
	var winning_score_row_rect := (dialog_root.find_child("WinningScoreRowPanel", true, false) as Control).get_global_rect()
	var panel_rect := (dialog_root.find_child("PlayerCountDialogPanel", true, false) as Control).get_global_rect()
	var stack_rects := [players_row_rect, difficulty_row_rect, item_mode_row_rect, winning_score_row_rect]
	for row_index in stack_rects.size():
		var row_rect: Rect2 = stack_rects[row_index]
		if absf(row_rect.get_center().x - panel_rect.get_center().x) > 3.0 \
				or absf(row_rect.size.x - players_row_rect.size.x) > 3.0 \
				or row_rect.size.x < panel_rect.size.x * 0.55 \
				or row_rect.size.x > panel_rect.size.x * 0.75:
			_fail("Configuration controls do not form a centered single-column stack: row=%s first=%s panel=%s" % [
				row_rect,
				players_row_rect,
				panel_rect,
			])
			return
		if row_index > 0:
			var previous_rect: Rect2 = stack_rects[row_index - 1]
			if row_rect.position.y - previous_rect.end.y < 4.0:
				_fail("Configuration stack rows overlap or are out of order")
				return

	var label_click_position := difficulty_label.get_global_rect().get_center()
	if difficulty_option_rect.has_point(label_click_position):
		_fail("Difficulty title overlaps its selector")
		return
	_move_pointer(label_click_position)
	await _wait_frames(4)
	if difficulty_option.is_hovered():
		_fail("Difficulty title unexpectedly hovers the selector")
		return

	_click_pointer(label_click_position)
	var difficulty_popup := difficulty_option.get_popup()
	await _wait_frames(4)
	if difficulty_popup == null or difficulty_popup.visible:
		_fail("Clicking the difficulty title unexpectedly opened the dropdown")
		return

	var value_click_position := difficulty_option_rect.get_center()
	_move_pointer(value_click_position)
	if not await _wait_for_glow(difficulty_row, 0.35):
		_fail("Moving over the difficulty selector did not activate its focus state")
		return
	_click_pointer(value_click_position)
	if not await _wait_for_popup_visible(difficulty_popup):
		_fail("Clicking the difficulty selector did not open the dropdown")
		return
	await _wait_frames(2)
	for icon_name in ["checked", "radio_checked", "unchecked", "radio_unchecked"]:
		var popup_icon := difficulty_popup.get_theme_icon(icon_name, "PopupMenu")
		if popup_icon != null and (popup_icon.get_width() > 1 or popup_icon.get_height() > 1):
			_fail("Difficulty popup unexpectedly shows a prefix icon: %s" % icon_name)
			return
	var popup_panel_style := difficulty_popup.get_theme_stylebox("panel", "PopupMenu") as StyleBoxFlat
	if popup_panel_style == null or popup_panel_style.shadow_size != 0:
		_fail("Difficulty popup reserves horizontal shadow space instead of matching the selector width")
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
	if not back_button.tooltip_text.is_empty():
		_fail("Back button repeats its visible label in a hover tooltip")
		return

	_click_pointer(back_position)
	if not await _wait_for_hidden(dialog_root):
		_fail("Clicking the back button did not close the single-player dialog")
		return
	if not await _wait_for_menu_buttons_enabled(menu):
		_fail("Main-menu buttons did not unlock after closing the dialog")
		return

	print("MAIN_MENU_PLAYER_COUNT_POINTER_INTERACTION_SMOKE_OK")
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
