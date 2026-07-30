extends SceneTree

const VIEWPORT_SPECS := [
	{"name": "960x540", "size": Vector2i(960, 540)},
	{"name": "1280x720", "size": Vector2i(1280, 720)},
	{"name": "1920x1080", "size": Vector2i(1920, 1080)},
]
const MENU_SPECS := [
	{
		"name": "single-player",
		"path": "res://scenes/main_menu_player_count_dialog.tscn",
		"panel": "PlayerCountDialogPanel",
	},
	{
		"name": "settings",
		"path": "res://scenes/main_menu_settings_dialog.tscn",
		"panel": "SettingsDialogPanel",
	},
	{
		"name": "exit",
		"path": "res://scenes/main_menu_exit_dialog.tscn",
		"panel": "ExitDialogPanel",
	},
	{
		"name": "adventure",
		"path": "res://scenes/adventure_avatar_menu_scene.tscn",
		"panel": "AdventureMenuPanel",
	},
	{
		"name": "skill-codex",
		"path": "res://scenes/main_menu_skill_codex_overlay.tscn",
		"panel": "TopBar",
		"open_method": "Open",
	},
	{
		"name": "multiplayer",
		"path": "res://scenes/multiplayer_lobby_scene.tscn",
		"panel": "SafeArea",
	},
]

var _failed := false
var _output_dir := ""
var _allow_clipping := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_output_dir = OS.get_environment("SECONDARY_MENU_INDIVIDUAL_SCREENSHOT_DIR").strip_edges()
	_allow_clipping = OS.get_environment("SECONDARY_MENU_INDIVIDUAL_ALLOW_CLIPPING") == "1"
	if _output_dir.is_empty():
		_fail("SECONDARY_MENU_INDIVIDUAL_SCREENSHOT_DIR is required")
		return
	var make_dir_error := DirAccess.make_dir_recursive_absolute(_output_dir)
	if make_dir_error != OK:
		_fail("Failed to create screenshot directory %s: %s" % [_output_dir, error_string(make_dir_error)])
		return

	for viewport_spec in VIEWPORT_SPECS:
		for menu_spec in MENU_SPECS:
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
	var menu := packed_scene.instantiate() if packed_scene != null else null
	if menu == null:
		_fail("Failed to instantiate %s" % menu_spec.path)
		return false
	viewport.add_child(menu)
	if menu_spec.has("open_method"):
		menu.call(menu_spec.open_method)
	elif menu is Control:
		menu.visible = true
		menu.modulate = Color.WHITE
		menu.scale = Vector2.ONE
	await _wait_frames(10)

	var panel := menu.find_child(menu_spec.panel, true, false) as Control
	if panel == null or not panel.is_visible_in_tree():
		_fail("%s is missing visible panel %s" % [menu_spec.name, menu_spec.panel])
		return false
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport.size))
	if not _allow_clipping and not _rect_contains(viewport_rect, panel.get_global_rect(), 3.0):
		_fail("%s %s panel escapes viewport: %s" % [
			menu_spec.name,
			viewport_spec.name,
			panel.get_global_rect(),
		])
		return false
	if not _validate_menu_layout(menu, menu_spec, viewport_spec):
		return false

	await RenderingServer.frame_post_draw
	var image := viewport.get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport.size:
		_fail("%s %s screenshot is empty or wrong-sized" % [menu_spec.name, viewport_spec.name])
		return false
	if not _has_rendered_pixels(image):
		_fail("%s %s screenshot contains no visible rendering" % [menu_spec.name, viewport_spec.name])
		return false

	var output_path := _output_dir.path_join("%s-%s.png" % [menu_spec.name, viewport_spec.name])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s: %s" % [output_path, error_string(save_error)])
		return false
	if menu_spec.name == "skill-codex" \
			and not await _capture_skill_codex_preview(menu, viewport_spec, viewport_rect):
		return false

	menu.queue_free()
	viewport.queue_free()
	await process_frame
	return true


func _validate_menu_layout(menu: Node, menu_spec: Dictionary, viewport_spec: Dictionary) -> bool:
	if menu_spec.name == "skill-codex":
		var root_control := menu.get_node_or_null("Root") as Control
		var scroll := menu.get_node_or_null("Root/SafeMargin/Layout/CardScroll") as ScrollContainer
		var grid := menu.get_node_or_null("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid") as GridContainer
		if root_control == null or scroll == null or grid == null \
				or scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED \
				or grid.size.x > scroll.size.x + 2.0:
			_fail("skill-codex %s has invalid screen-space or horizontal layout" % viewport_spec.name)
			return false
		return true
	if menu_spec.name != "multiplayer":
		return true

	var body_scroll := menu.get_node_or_null("SafeArea/MainLayout/BodyScroll") as ScrollContainer
	var body := menu.get_node_or_null("SafeArea/MainLayout/BodyScroll/Body") as GridContainer
	var expected_columns := 2 if viewport_spec.size == Vector2i(960, 540) else 4
	if body_scroll == null or body == null or body.columns != expected_columns \
			or body_scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED \
			or body_scroll.get_h_scroll_bar().visible \
			or body.size.x > body_scroll.size.x + 2.0:
		_fail("multiplayer %s has invalid responsive grid or horizontal scrolling" % viewport_spec.name)
		return false
	var panels: Array[Control] = []
	for panel_name in ["ModeRail", "RoomBrowser", "LobbyPanel", "FriendsPanel"]:
		var child_panel := body.get_node_or_null(panel_name) as Control
		if child_panel == null or not _rect_contains(body.get_global_rect(), child_panel.get_global_rect(), 2.0):
			_fail("multiplayer %s panel %s escapes its grid" % [viewport_spec.name, panel_name])
			return false
		panels.append(child_panel)
	for left_index in range(panels.size()):
		for right_index in range(left_index + 1, panels.size()):
			if panels[left_index].get_global_rect().intersects(panels[right_index].get_global_rect(), true):
				_fail("multiplayer %s panels overlap" % viewport_spec.name)
				return false
	return true


func _capture_skill_codex_preview(
		menu: Node,
		viewport_spec: Dictionary,
		viewport_rect: Rect2) -> bool:
	if not bool(menu.call("DebugActivateVisibleCard", 0)):
		_fail("skill-codex %s failed to open an unlocked preview" % viewport_spec.name)
		return false
	await _wait_frames(4)
	var preview_layer := menu.get_node_or_null("Root/PreviewLayer") as Control
	var preview_surface := menu.get_node_or_null(
		"Root/PreviewLayer/PreviewCenter/PreviewGlassSurface") as ColorRect
	var preview_card := menu.get_node_or_null(
		"Root/PreviewLayer/PreviewCenter/PreviewCard") as Control
	if preview_layer == null or not preview_layer.visible \
			or preview_surface == null or preview_card == null \
			or not _rect_contains(viewport_rect, preview_surface.get_global_rect(), 2.0) \
			or not _rect_contains(preview_surface.get_global_rect(), preview_card.get_global_rect(), 2.0):
		_fail("skill-codex %s preview escapes its focus glass or viewport" % viewport_spec.name)
		return false
	await RenderingServer.frame_post_draw
	var image := preview_layer.get_viewport().get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport_spec.size:
		_fail("skill-codex %s preview screenshot is empty or wrong-sized" % viewport_spec.name)
		return false
	var output_path := _output_dir.path_join("skill-codex-preview-%s.png" % viewport_spec.name)
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s: %s" % [output_path, error_string(save_error)])
		return false
	menu.call("HandleCancel")
	await process_frame
	return true


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _rect_contains(outer: Rect2, inner: Rect2, tolerance: float) -> bool:
	return inner.position.x >= outer.position.x - tolerance \
		and inner.position.y >= outer.position.y - tolerance \
		and inner.end.x <= outer.end.x + tolerance \
		and inner.end.y <= outer.end.y + tolerance


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
