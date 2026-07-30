extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const GAME_BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const SINGLE_PLAYER_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button"
const SETTINGS_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SettingsFrame/Button"
const EXIT_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/ExitFrame/Button"
const AVATAR_BUTTON_PATH := "AvatarEntryLayer/MainMenuAvatarEntry/Button"
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
	_output_dir = OS.get_environment("SECONDARY_MENU_GLASS_SCREENSHOT_DIR").strip_edges()
	if _output_dir.is_empty():
		_fail("SECONDARY_MENU_GLASS_SCREENSHOT_DIR is required")
		return

	for viewport_spec in VIEWPORT_SPECS:
		if not await _capture_main_menu_states(viewport_spec):
			return
		if not await _capture_pause_states(viewport_spec):
			return

	print("SECONDARY_MENU_GLASS_VISUAL_CAPTURE_OK %s" % _output_dir)
	await _dispose_audio_before_quit()
	quit(0)


func _capture_main_menu_states(viewport_spec: Dictionary) -> bool:
	var viewport := _create_viewport(viewport_spec.size)
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	var menu := packed_scene.instantiate() as Control if packed_scene != null else null
	if menu == null:
		_fail("Failed to instantiate main menu")
		return false
	viewport.add_child(menu)
	await _wait_frames(4)

	var avatar_button := menu.get_node_or_null(AVATAR_BUTTON_PATH) as Button
	if avatar_button == null:
		_fail("Avatar entry button is missing")
		return false
	avatar_button.emit_signal("pressed")
	var avatar_root := await _wait_for_node(menu, "AvatarEditDialogLayer/CharacterAvatarEditScene") as Control
	if avatar_root == null or not await _wait_for_opaque(avatar_root):
		_fail("Avatar editor did not open")
		return false
	if not await _save_viewport(viewport, "avatar", viewport_spec.name):
		return false
	var avatar_back := avatar_root.find_child("BackButton", true, false) as Button
	avatar_back.emit_signal("pressed")
	if not await _wait_for_node_removed(menu, "AvatarEditDialogLayer") or not await _wait_for_menu_buttons_enabled(menu):
		_fail("Avatar editor did not close cleanly")
		return false

	var single_button := menu.get_node_or_null(SINGLE_PLAYER_BUTTON_PATH) as Button
	single_button.emit_signal("pressed")
	var single_root := menu.get_node_or_null("PlayerCountDialogLayer/PlayerCountDialogRoot") as Control
	if single_root == null or not await _wait_for_opaque(single_root):
		_fail("Single-player dialog did not open")
		return false
	var configuration_scroll := single_root.find_child("ConfigurationScroll", true, false) as ScrollContainer
	if configuration_scroll == null or configuration_scroll.scroll_vertical > 2:
		_fail("Single-player dialog did not open at the top of its compact list")
		return false
	await _wait_frames(4)
	var players_row := single_root.find_child("PlayersRowPanel", true, false) as Control
	if players_row == null or not _is_rect_inside(players_row.get_global_rect(), configuration_scroll.get_global_rect()):
		_fail("Single-player dialog clipped its first configuration row (scroll=%s, row=%s, viewport=%s)" % [
			configuration_scroll.scroll_vertical,
			players_row.get_global_rect() if players_row != null else Rect2(),
			configuration_scroll.get_global_rect(),
		])
		return false
	if not await _save_viewport(viewport, "single-player", viewport_spec.name):
		return false
	var player_count_option := single_root.find_child("PlayerCountOption", true, false) as OptionButton
	var difficulty_row := single_root.find_child("DifficultyRowPanel", true, false) as Control
	var difficulty_option := single_root.find_child("DifficultyOption", true, false) as OptionButton
	if player_count_option == null or difficulty_row == null or difficulty_option == null:
		_fail("Single-player interactive rows are missing")
		return false
	player_count_option.release_focus()
	await _wait_frames(10)
	var idle_image := viewport.get_texture().get_image()
	difficulty_option.grab_focus()
	if not await _wait_for_glow(difficulty_row, 0.35):
		_fail("Single-player row did not enter its highlighted focus state")
		return false
	var hover_image := viewport.get_texture().get_image()
	if not await _save_viewport(viewport, "single-player-row-hover", viewport_spec.name):
		return false
	if not _has_subtle_visible_change(idle_image, hover_image, difficulty_row.get_global_rect()):
		_fail("Single-player row highlight state did not visibly change")
		return false
	difficulty_option.show_popup()
	var difficulty_popup := difficulty_option.get_popup()
	if difficulty_popup == null or not await _wait_for_popup_visible(difficulty_popup):
		_fail("Single-player option menu did not open for visual capture")
		return false
	await _wait_frames(2)
	var difficulty_popup_rect := Rect2(Vector2(difficulty_popup.position), Vector2(difficulty_popup.size))
	var difficulty_option_rect := difficulty_option.get_global_rect()
	if not _is_rect_inside(
		difficulty_popup_rect,
		Rect2(Vector2.ZERO, Vector2(viewport.size))):
		_fail("Single-player configuration selector popup escaped the viewport")
		return false
	if absf(difficulty_popup_rect.position.x - difficulty_option_rect.position.x) > 3.0 \
			or absf(difficulty_popup_rect.size.x - difficulty_option_rect.size.x) > 3.0:
		_fail("Single-player popup does not align to the compact value selector (popup=%s, option=%s)" % [
			difficulty_popup_rect,
			difficulty_option_rect,
		])
		return false
	if not await _save_viewport(viewport, "single-player-row-popup", viewport_spec.name):
		return false
	difficulty_popup.hide()
	var winning_score_option := single_root.find_child("WinningScoreOption", true, false) as OptionButton
	var configuration_scroll_bar := configuration_scroll.get_v_scroll_bar()
	var configuration_has_overflow := configuration_scroll_bar.max_value > configuration_scroll_bar.page + 1.0
	winning_score_option.grab_focus()
	await process_frame
	await process_frame
	var configuration_viewport_rect := configuration_scroll.get_global_rect()
	var winning_score_rect := winning_score_option.get_global_rect()
	if configuration_has_overflow and configuration_scroll.scroll_vertical <= 2:
		_fail("Single-player focus did not scroll the compact list to its final option")
		return false
	if winning_score_rect.position.y < configuration_viewport_rect.position.y - 2.0 \
			or winning_score_rect.end.y > configuration_viewport_rect.end.y + 2.0:
		_fail("Single-player focus target was not fully visible after manual focus scrolling")
		return false
	var back_frame := single_root.find_child("BackFrame", true, false) as Control
	var back_button := single_root.find_child("BackButton", true, false) as Button
	if back_frame == null or back_button == null:
		_fail("Single-player back control is missing")
		return false
	winning_score_option.release_focus()
	back_button.grab_focus()
	if not await _wait_for_glow(back_frame, 0.35):
		_fail("Single-player back button did not enter its highlighted focus state")
		return false
	if not await _save_viewport(viewport, "single-player-back-hover", viewport_spec.name):
		return false
	back_button.emit_signal("pressed")
	if not await _wait_for_hidden(single_root) or not await _wait_for_menu_buttons_enabled(menu):
		_fail("Single-player dialog did not close cleanly")
		return false

	var settings_button := menu.get_node_or_null(SETTINGS_BUTTON_PATH) as Button
	settings_button.emit_signal("pressed")
	var settings_root := menu.get_node_or_null("SettingsDialogLayer/SettingsDialogRoot") as Control
	var controls_root := menu.get_node_or_null("SettingsDialogLayer/ControlsDialogRoot") as Control
	if settings_root == null or controls_root == null or not await _wait_for_opaque(settings_root):
		_fail("Settings dialog did not open")
		return false
	var settings_scroll := settings_root.find_child("SettingsScroll", true, false) as ScrollContainer
	if settings_scroll == null or settings_scroll.scroll_vertical > 2:
		var focus_owner := viewport.gui_get_focus_owner()
		_fail("Settings dialog did not open at the top of its compact grid (scroll=%s, focus=%s)" % [
			settings_scroll.scroll_vertical if settings_scroll != null else -1,
			focus_owner.name if focus_owner != null else "none",
		])
		return false
	if not await _save_viewport(viewport, "settings", viewport_spec.name):
		return false
	var controls_button := settings_root.find_child("ControlsButton", true, false) as Button
	var settings_scroll_bar := settings_scroll.get_v_scroll_bar()
	var settings_has_overflow := settings_scroll_bar.max_value > settings_scroll_bar.page + 1.0
	controls_button.grab_focus()
	await process_frame
	await process_frame
	var settings_viewport_rect := settings_scroll.get_global_rect()
	var controls_button_rect := controls_button.get_global_rect()
	if settings_has_overflow and settings_scroll.scroll_vertical <= 2:
		_fail("Settings focus did not scroll the compact grid to its final control")
		return false
	if controls_button_rect.position.y < settings_viewport_rect.position.y - 2.0 \
			or controls_button_rect.end.y > settings_viewport_rect.end.y + 2.0:
		_fail("Settings focus target was not fully visible after manual focus scrolling")
		return false
	controls_button.emit_signal("pressed")
	if not await _wait_for_opaque(controls_root):
		_fail("Controls dialog did not open")
		return false
	if not await _save_viewport(viewport, "controls", viewport_spec.name):
		return false
	(controls_root.find_child("BackButton", true, false) as Button).emit_signal("pressed")
	if not await _wait_for_hidden(controls_root):
		_fail("Controls dialog did not close cleanly")
		return false
	(settings_root.find_child("DoneButton", true, false) as Button).emit_signal("pressed")
	if not await _wait_for_hidden(settings_root) or not await _wait_for_menu_buttons_enabled(menu):
		_fail("Settings dialog did not close cleanly")
		return false

	var exit_button := menu.get_node_or_null(EXIT_BUTTON_PATH) as Button
	exit_button.emit_signal("pressed")
	var exit_root := menu.get_node_or_null("ExitDialogLayer/ExitDialogRoot") as Control
	if exit_root == null or not await _wait_for_opaque(exit_root):
		_fail("Exit dialog did not open")
		return false
	if not await _save_viewport(viewport, "exit", viewport_spec.name):
		return false
	(exit_root.find_child("CancelButton", true, false) as Button).emit_signal("pressed")
	await _wait_for_hidden(exit_root)

	menu.queue_free()
	viewport.queue_free()
	await process_frame
	return true


func _capture_pause_states(viewport_spec: Dictionary) -> bool:
	var viewport := _create_viewport(viewport_spec.size)
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
	await _wait_frames(3)
	var board_image := viewport.get_texture().get_image()
	_press_escape(viewport)
	var pause_layer := await _wait_for_node(board, "PauseMenuLayer")
	var pause_root := pause_layer.get_node_or_null("PauseMenuRoot") as Control if pause_layer != null else null
	if pause_root == null or not await _wait_for_opaque(pause_root):
		_fail("Pause menu did not open")
		return false
	var pause_panel := pause_root.find_child("PauseMenuPanel", true, false) as Control
	await _wait_frames(4)
	if pause_panel == null or not _is_rect_inside(pause_panel.get_global_rect(), Rect2(Vector2.ZERO, Vector2(viewport.size))):
		_fail("Pause menu panel was not laid out inside the viewport (panel=%s, viewport=%s)" % [
			pause_panel.get_global_rect() if pause_panel != null else Rect2(),
			Rect2(Vector2.ZERO, Vector2(viewport.size)),
		])
		return false
	if not await _save_viewport(viewport, "pause", viewport_spec.name):
		return false
	var pause_image := viewport.get_texture().get_image()
	if not _has_visible_change(board_image, pause_image, pause_panel.get_global_rect()):
		_fail("Pause menu was visible in the scene tree but was not rendered into the viewport")
		return false

	var restart_button := pause_root.find_child("RestartButton", true, false) as Button
	var restart_frame := pause_root.find_child("RestartFrame", true, false) as Control
	if restart_button == null or restart_frame == null:
		_fail("Pause restart row is missing")
		return false
	restart_button.grab_focus()
	if not await _wait_for_glow(restart_frame, 0.35):
		_fail("Pause restart row did not enter its focused state")
		return false
	if not await _save_viewport(viewport, "pause-row-focus", viewport_spec.name):
		return false

	paused = false
	board.queue_free()
	viewport.queue_free()
	await process_frame
	return true


func _create_viewport(viewport_size: Vector2i) -> SubViewport:
	var viewport := SubViewport.new()
	viewport.size = viewport_size
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.gui_embed_subwindows = true
	root.add_child(viewport)
	return viewport


func _press_escape(viewport: SubViewport) -> void:
	var press := InputEventKey.new()
	press.keycode = KEY_ESCAPE
	press.physical_keycode = KEY_ESCAPE
	press.pressed = true
	press.echo = false
	viewport.push_input(press, true)
	var release := InputEventKey.new()
	release.keycode = KEY_ESCAPE
	release.physical_keycode = KEY_ESCAPE
	release.pressed = false
	viewport.push_input(release, true)


func _wait_for_node(parent: Node, path: NodePath, max_frames := 180) -> Node:
	var deadline := Time.get_ticks_msec() + max_frames * 20
	while Time.get_ticks_msec() < deadline:
		var node := parent.get_node_or_null(path)
		if node != null:
			return node
		await process_frame
	return parent.get_node_or_null(path)


func _wait_for_node_removed(parent: Node, path: NodePath, max_frames := 180) -> bool:
	var deadline := Time.get_ticks_msec() + max_frames * 20
	while Time.get_ticks_msec() < deadline:
		if parent.get_node_or_null(path) == null:
			return true
		await process_frame
	return parent.get_node_or_null(path) == null


func _wait_for_opaque(control: Control, max_frames := 180) -> bool:
	var deadline := Time.get_ticks_msec() + max_frames * 20
	while Time.get_ticks_msec() < deadline:
		if control.visible and control.modulate.a >= 0.98:
			return true
		await process_frame
	return control.visible and control.modulate.a >= 0.98


func _wait_for_popup_visible(popup: PopupMenu, max_frames := 180) -> bool:
	var deadline := Time.get_ticks_msec() + max_frames * 20
	while Time.get_ticks_msec() < deadline:
		if popup.visible:
			return true
		await process_frame
	return popup.visible


func _wait_for_hidden(control: Control, max_frames := 180) -> bool:
	var deadline := Time.get_ticks_msec() + max_frames * 20
	while Time.get_ticks_msec() < deadline:
		if not control.visible:
			return true
		await process_frame
	return not control.visible


func _wait_for_menu_buttons_enabled(menu: Node, max_frames := 240) -> bool:
	var deadline := Time.get_ticks_msec() + max_frames * 20
	while Time.get_ticks_msec() < deadline:
		if not bool(menu.call("DebugAreMainMenuButtonsDisabled")):
			return true
		await process_frame
	return not bool(menu.call("DebugAreMainMenuButtonsDisabled"))


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _wait_for_glow(frame: Control, minimum_glow: float, max_frames := 120) -> bool:
	for _frame in max_frames:
		if float(frame.call("DebugGetGlow")) >= minimum_glow:
			return true
		await process_frame
	return float(frame.call("DebugGetGlow")) >= minimum_glow


func _save_viewport(viewport: SubViewport, state_name: String, viewport_name: String) -> bool:
	await _wait_frames(4)
	await RenderingServer.frame_post_draw
	var image := viewport.get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport.size:
		_fail("%s %s screenshot is empty or wrong-sized" % [state_name, viewport_name])
		return false
	var output_path := _output_dir.path_join("%s-%s.png" % [state_name, viewport_name])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s: %s" % [output_path, error_string(save_error)])
		return false
	return true


func _is_rect_inside(inner_rect: Rect2, outer_rect: Rect2, tolerance := 2.0) -> bool:
	return inner_rect.position.x >= outer_rect.position.x - tolerance \
		and inner_rect.position.y >= outer_rect.position.y - tolerance \
		and inner_rect.end.x <= outer_rect.end.x + tolerance \
		and inner_rect.end.y <= outer_rect.end.y + tolerance


func _has_visible_change(before: Image, after: Image, area: Rect2) -> bool:
	if before == null or after == null or before.get_size() != after.get_size():
		return false
	var sample_area := Rect2i(area.grow(-12.0)).intersection(Rect2i(Vector2i.ZERO, before.get_size()))
	if sample_area.size.x < 8 or sample_area.size.y < 8:
		return false
	var changed_samples := 0
	var total_samples := 0
	for column in 10:
		for row in 8:
			var x := sample_area.position.x + (sample_area.size.x - 1) * column / 9
			var y := sample_area.position.y + (sample_area.size.y - 1) * row / 7
			var before_color := before.get_pixel(x, y)
			var after_color := after.get_pixel(x, y)
			var delta := absf(before_color.r - after_color.r) + absf(before_color.g - after_color.g) + absf(before_color.b - after_color.b)
			if delta > 0.18:
				changed_samples += 1
			total_samples += 1
	return changed_samples >= total_samples / 3


func _has_subtle_visible_change(before: Image, after: Image, area: Rect2) -> bool:
	if before == null or after == null or before.get_size() != after.get_size():
		return false
	var sample_area := Rect2i(area.grow(-2.0)).intersection(Rect2i(Vector2i.ZERO, before.get_size()))
	if sample_area.size.x < 8 or sample_area.size.y < 8:
		return false
	var changed_samples := 0
	var total_samples := 0
	for column in 16:
		for row in 10:
			var x := sample_area.position.x + (sample_area.size.x - 1) * column / 15
			var y := sample_area.position.y + (sample_area.size.y - 1) * row / 9
			var before_color := before.get_pixel(x, y)
			var after_color := after.get_pixel(x, y)
			var delta := absf(before_color.r - after_color.r) + absf(before_color.g - after_color.g) + absf(before_color.b - after_color.b)
			if delta > 0.05:
				changed_samples += 1
			total_samples += 1
	return changed_samples >= total_samples / 8


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
	push_error("SECONDARY_MENU_GLASS_VISUAL_CAPTURE_FAIL %s" % message)
	quit(1)
