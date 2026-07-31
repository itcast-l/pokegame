extends SceneTree

const VISUAL_PROFILE_META := "secondary_menu_visual_profile"
const PAUSE_DERIVED_PROFILE := "pause-derived-core-menu"
const VIEWPORT_SPECS := [
	{"name": "960x540", "size": Vector2i(960, 540)},
	{"name": "1280x720", "size": Vector2i(1280, 720)},
	{"name": "1920x1080", "size": Vector2i(1920, 1080)},
]
const MENU_SPECS := [
	{
		"name": "avatar",
		"path": "res://scenes/character_avatar_edit_scene.tscn",
		"panel": "DialogPanel",
		"focus_control": "BackButton",
		"focus_frame": "BackFrame",
		"scrolls": ["GridScroll"],
	},
	{
		"name": "single-player",
		"path": "res://scenes/main_menu_player_count_dialog.tscn",
		"panel": "PlayerCountDialogPanel",
		"focus_control": "PlayerCountOption",
		"focus_frame": "PlayersRowPanel",
		"scrolls": ["ConfigurationScroll"],
	},
	{
		"name": "settings",
		"path": "res://scenes/main_menu_settings_dialog.tscn",
		"panel": "SettingsDialogPanel",
		"focus_control": "MasterVolumeSlider",
		"focus_frame": "MasterVolumeRowPanel",
		"scrolls": ["SettingsScroll"],
	},
	{
		"name": "controls",
		"path": "res://scenes/main_menu_controls_dialog.tscn",
		"panel": "ControlsDialogPanel",
		"focus_control": "KeyboardButton",
		"focus_frame": "MoveUpRow",
		"scrolls": ["RowsScroll"],
	},
	{
		"name": "exit",
		"path": "res://scenes/main_menu_exit_dialog.tscn",
		"panel": "ExitDialogPanel",
		"focus_control": "ConfirmButton",
		"focus_frame": "ConfirmFrame",
		"scrolls": [],
	},
]

var _failed := false
var _output_dir := ""


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_output_dir = OS.get_environment("SECONDARY_MENU_INDIVIDUAL_SCREENSHOT_DIR").strip_edges()
	if _output_dir.is_empty():
		_fail("SECONDARY_MENU_INDIVIDUAL_SCREENSHOT_DIR is required")
		return
	var make_dir_error := DirAccess.make_dir_recursive_absolute(_output_dir)
	if make_dir_error != OK:
		_fail("Failed to create screenshot directory %s: %s" % [_output_dir, error_string(make_dir_error)])
		return

	var only_menu := OS.get_environment("SECONDARY_MENU_INDIVIDUAL_ONLY").strip_edges()
	for viewport_spec in VIEWPORT_SPECS:
		for menu_spec in MENU_SPECS:
			if not only_menu.is_empty() and str(menu_spec.name) != only_menu:
				continue
			if not await _capture_menu(menu_spec, viewport_spec):
				return

	print("SECONDARY_MENU_INDIVIDUAL_VISUAL_CAPTURE_OK %s" % _output_dir)
	await _dispose_audio_before_quit()
	quit(0)


func _capture_menu(menu_spec: Dictionary, viewport_spec: Dictionary) -> bool:
	var viewport := SubViewport.new()
	viewport.size = viewport_spec.size
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.gui_embed_subwindows = true
	root.add_child(viewport)

	var packed_scene := load(menu_spec.path) as PackedScene
	var menu := packed_scene.instantiate() as Control if packed_scene != null else null
	if menu == null:
		_fail("Failed to instantiate %s" % menu_spec.path)
		return false
	viewport.add_child(menu)
	menu.visible = true
	menu.modulate = Color.WHITE
	await _wait_frames(8)
	if not _prepare_capture_state(menu, menu_spec.name):
		return false
	await _wait_frames(2)

	if not _validate_menu_layout(menu, menu_spec, viewport_spec):
		return false
	var focus_owner := viewport.gui_get_focus_owner()
	if focus_owner != null:
		focus_owner.release_focus()
	await _wait_frames(4)
	if not await _save_screenshot(viewport, menu_spec.name, viewport_spec.name, "idle"):
		return false

	var focus_control := menu.find_child(menu_spec.focus_control, true, false) as Control
	var focus_frame := menu.find_child(menu_spec.focus_frame, true, false) as Control
	if focus_control == null or focus_frame == null or focus_control.focus_mode == Control.FOCUS_NONE:
		_fail("%s %s is missing its focus capture target" % [menu_spec.name, viewport_spec.name])
		return false
	focus_control.grab_focus()
	await _wait_frames(10)
	if not focus_control.has_focus() or float(focus_frame.call("DebugGetGlow")) <= 0.08:
		_fail("%s %s did not render its focus state" % [menu_spec.name, viewport_spec.name])
		return false
	if not await _save_screenshot(viewport, menu_spec.name, viewport_spec.name, "focus"):
		return false

	menu.queue_free()
	viewport.queue_free()
	await process_frame
	return true


func _prepare_capture_state(menu: Control, menu_name: String) -> bool:
	if menu_name == "settings":
		var master_slider := menu.find_child("MasterVolumeSlider", true, false) as HSlider
		var music_slider := menu.find_child("MusicVolumeSlider", true, false) as HSlider
		var master_value := menu.find_child("MasterVolumeValue", true, false) as Label
		var music_value := menu.find_child("MusicVolumeValue", true, false) as Label
		if master_slider == null or music_slider == null or master_value == null or music_value == null:
			_fail("Settings capture is missing volume controls")
			return false
		master_slider.value = 82.0
		music_slider.value = 76.0
		master_value.text = "82%"
		music_value.text = "76%"
	elif menu_name == "controls":
		var back_button := menu.find_child("BackButton", true, false) as Button
		if back_button == null:
			_fail("Controls capture is missing its back button")
			return false
		if back_button.text.is_empty():
			back_button.text = TranslationServer.translate("ADVENTURE_BACK")
	return true


func _validate_menu_layout(
		menu: Control,
		menu_spec: Dictionary,
		viewport_spec: Dictionary) -> bool:
	var context := "%s %s" % [menu_spec.name, viewport_spec.name]
	var panel := menu.find_child(menu_spec.panel, true, false) as Control
	if panel == null or not panel.is_visible_in_tree():
		_fail("%s is missing visible panel %s" % [context, menu_spec.panel])
		return false
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport_spec.size))
	if not _rect_contains(viewport_rect, _get_canvas_rect(panel), 3.0):
		_fail("%s panel escapes viewport: %s" % [context, _get_canvas_rect(panel)])
		return false
	if not menu.has_meta(VISUAL_PROFILE_META) \
			or str(menu.get_meta(VISUAL_PROFILE_META)) != PAUSE_DERIVED_PROFILE:
		_fail("%s does not use the pause-derived profile" % context)
		return false
	var artwork := panel.get_node_or_null("PauseDerivedPanelArtwork") as TextureRect
	if artwork == null or not _rects_close(_get_canvas_rect(artwork), _get_canvas_rect(panel), 2.0):
		_fail("%s panel artwork does not match its panel" % context)
		return false
	var removes_center_ornaments := bool(
		panel.get_meta("pause_derived_center_ornaments_removed", false))
	if menu_spec.name in ["avatar", "single-player", "settings", "controls"]:
		var left_slice := artwork.get_node_or_null("PauseDerivedPanelLeft") as TextureRect
		var right_slice := artwork.get_node_or_null("PauseDerivedPanelRight") as TextureRect
		if not removes_center_ornaments or artwork.self_modulate.a > 0.01 \
				or left_slice == null or right_slice == null:
			_fail("%s does not use the ornament-free panel composition" % context)
			return false
		var left_rect := _get_canvas_rect(left_slice)
		var right_rect := _get_canvas_rect(right_slice)
		if not _rect_contains(_get_canvas_rect(panel), left_rect, 2.0) \
				or not _rect_contains(_get_canvas_rect(panel), right_rect, 2.0) \
				or absf(left_rect.end.x - right_rect.position.x) > 2.0:
			_fail("%s ornament-free panel slices do not cover the panel" % context)
			return false
	elif removes_center_ornaments:
		_fail("%s unexpectedly removes the shared panel ornaments" % context)
		return false
	for scroll_name in menu_spec.scrolls:
		var scroll := menu.find_child(scroll_name, true, false) as ScrollContainer
		if scroll == null or scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED \
				or scroll.get_h_scroll_bar().visible \
				or not _rect_contains(_get_canvas_rect(panel), _get_canvas_rect(scroll), 2.0):
			_fail("%s has invalid scroll layout in %s" % [context, scroll_name])
			return false
	return true


func _save_screenshot(
		viewport: SubViewport,
		menu_name: String,
		viewport_name: String,
		state: String) -> bool:
	await RenderingServer.frame_post_draw
	var image := viewport.get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport.size:
		_fail("%s %s %s screenshot is empty or wrong-sized" % [menu_name, viewport_name, state])
		return false
	if not _has_rendered_pixels(image):
		_fail("%s %s %s screenshot contains no visible rendering" % [menu_name, viewport_name, state])
		return false
	var output_path := _output_dir.path_join(
		"%s-%s-%s.png" % [menu_name, viewport_name, state])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s: %s" % [output_path, error_string(save_error)])
		return false
	return true


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _get_canvas_rect(control: Control) -> Rect2:
	var canvas_transform := control.get_global_transform_with_canvas()
	var top_left := canvas_transform * Vector2.ZERO
	var bottom_right := canvas_transform * control.size
	return Rect2(top_left, bottom_right - top_left)


func _rect_contains(outer: Rect2, inner: Rect2, tolerance: float) -> bool:
	return inner.position.x >= outer.position.x - tolerance \
		and inner.position.y >= outer.position.y - tolerance \
		and inner.end.x <= outer.end.x + tolerance \
		and inner.end.y <= outer.end.y + tolerance


func _rects_close(left: Rect2, right: Rect2, tolerance: float) -> bool:
	return left.position.distance_to(right.position) <= tolerance \
		and left.size.distance_to(right.size) <= tolerance


func _has_rendered_pixels(image: Image) -> bool:
	var image_size := image.get_size()
	for column in 12:
		for row in 8:
			var x := (image_size.x - 1) * column / 11
			var y := (image_size.y - 1) * row / 7
			var color := image.get_pixel(x, y)
			if color.a > 0.05 and color.r + color.g + color.b > 0.04:
				return true
	return false


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
	push_error("SECONDARY_MENU_INDIVIDUAL_VISUAL_CAPTURE_FAIL %s" % message)
	quit(1)
