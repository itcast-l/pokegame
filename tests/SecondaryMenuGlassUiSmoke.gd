extends SceneTree

const GLASS_SHADER_PATH := "res://shaders/adventure_menu_glass.gdshader"
const SAFE_PADDING := 24.0
const RECT_TOLERANCE := 2.0
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
		"design_size": Vector2(760, 600),
		"contents": ["PreviewPanel", "AvatarListPanel"],
		"frames": ["BackFrame", "ConfirmFrame"],
	},
	{
		"name": "single-player",
		"path": "res://scenes/main_menu_player_count_dialog.tscn",
		"panel": "PlayerCountDialogPanel",
		"design_size": Vector2(860, 560),
		"contents": [],
		"single_column_list": "ConfigurationRows",
		"title_anchor": "PlayerCountTitle",
		"frames": ["BackFrame", "PlayersRowPanel", "DifficultyRowPanel", "ItemModeRowPanel", "WinningScoreRowPanel", "StartGameFrame"],
		"frame_styles": {
			"BackFrame": 9,
			"PlayersRowPanel": 8,
			"DifficultyRowPanel": 8,
			"ItemModeRowPanel": 8,
			"WinningScoreRowPanel": 8,
			"StartGameFrame": 10,
		},
		"menu_row_style": 8,
		"menu_row_frames": ["PlayersRowPanel", "DifficultyRowPanel", "ItemModeRowPanel", "WinningScoreRowPanel"],
		"configuration_scroll": "ConfigurationScroll",
		"forbidden_prefix_icons": ["PlayersIcon", "DifficultyIcon", "ItemModeIcon", "WinningScoreIcon"],
	},
	{
		"name": "settings",
		"path": "res://scenes/main_menu_settings_dialog.tscn",
		"panel": "SettingsDialogPanel",
		"design_size": Vector2(620, 580),
		"contents": ["SettingsContentPanel"],
		"single_column_list": "SettingsList",
		"frames": ["BackFrame", "MasterVolumeRowPanel", "WindowModeRowPanel", "ControlsRowPanel", "ResetDefaultsFrame", "DoneFrame"],
	},
	{
		"name": "controls",
		"path": "res://scenes/main_menu_controls_dialog.tscn",
		"panel": "ControlsDialogPanel",
		"design_size": Vector2(720, 580),
		"contents": ["ControlsContentPanel"],
		"single_column_list": "Rows",
		"frames": ["BackFrame", "MoveUpRow", "CancelRow", "ResetDefaultsFrame", "DoneFrame"],
	},
	{
		"name": "exit",
		"path": "res://scenes/main_menu_exit_dialog.tscn",
		"panel": "ExitDialogPanel",
		"design_size": Vector2(560, 360),
		"contents": ["PromptPanel"],
		"frames": ["BackFrame", "CancelFrame", "ConfirmFrame"],
	},
	{
		"name": "pause",
		"path": "res://scenes/pause_menu.tscn",
		"panel": "PauseMenuPanel",
		"design_size": Vector2(600, 500),
		"contents": ["PauseActionsPanel"],
		"single_column_list": "Actions",
		"frames": ["BackFrame", "ContinueFrame", "ReturnToMainMenuFrame", "RestartFrame", "QuitFrame"],
	},
]

var _failed := false
var _viewport: SubViewport


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_viewport = SubViewport.new()
	_viewport.size = VIEWPORT_SPECS[0].size
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(_viewport)

	for menu_spec in MENU_SPECS:
		if not await _audit_menu(menu_spec):
			return

	print("SECONDARY_MENU_GLASS_UI_SMOKE_OK")
	_viewport.queue_free()
	await process_frame
	await _dispose_audio_before_quit()
	quit(0)


func _audit_menu(menu_spec: Dictionary) -> bool:
	var packed_scene := load(menu_spec.path) as PackedScene
	if packed_scene == null:
		_fail("Failed to load %s" % menu_spec.path)
		return false

	var menu := packed_scene.instantiate() as Control
	if menu == null:
		_fail("Failed to instantiate %s" % menu_spec.name)
		return false
	_viewport.add_child(menu)
	menu.visible = true
	menu.modulate = Color.WHITE
	await process_frame
	await process_frame

	for viewport_spec in VIEWPORT_SPECS:
		_viewport.size = viewport_spec.size
		await process_frame
		await process_frame
		if not _audit_current_layout(menu, menu_spec, viewport_spec):
			return false
		if not await _capture_optional_screenshot(menu_spec.name, viewport_spec):
			return false

	menu.queue_free()
	await process_frame
	return true


func _audit_current_layout(menu: Control, menu_spec: Dictionary, viewport_spec: Dictionary) -> bool:
	var context := "%s %s" % [menu_spec.name, viewport_spec.name]
	var viewport_size: Vector2i = viewport_spec.size
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport_size))
	var panel := menu.find_child(menu_spec.panel, true, false) as Control
	var scrim := menu.find_child("Scrim", true, false) as ColorRect
	if panel == null or scrim == null:
		_fail("%s is missing its panel or readability veil" % context)
		return false
	if not _rect_contains(viewport_rect, panel.get_global_rect()):
		_fail("%s panel escapes the safe viewport: %s" % [context, panel.get_global_rect()])
		return false
	if not _rects_close(scrim.get_global_rect(), viewport_rect):
		_fail("%s readability veil does not cover the viewport" % context)
		return false

	var design_size: Vector2 = menu_spec.design_size
	var expected_size := Vector2(
		minf(design_size.x, viewport_size.x - SAFE_PADDING * 2.0),
		minf(design_size.y, viewport_size.y - SAFE_PADDING * 2.0))
	if panel.size.distance_to(expected_size) > RECT_TOLERANCE:
		_fail("%s panel size is %s instead of %s" % [context, panel.size, expected_size])
		return false

	var glass_surface := panel.get_node_or_null("GlassSurface") as ColorRect
	var glass_frame := panel.get_node_or_null("GlassFrame") as Panel
	if menu_spec.has("custom_glass_surface"):
		if glass_surface != null or glass_frame != null:
			_fail("%s still installs the old adventure glass base" % context)
			return false
		var custom_surface := menu.find_child(menu_spec.custom_glass_surface, true, false) as ColorRect
		if custom_surface == null or not custom_surface.visible:
			_fail("%s is missing its open glass surface" % context)
			return false
		if not _rects_close(custom_surface.get_global_rect(), panel.get_global_rect()):
			_fail("%s open glass surface does not match panel bounds" % context)
			return false
		if not _assert_glass_material(custom_surface, context):
			return false
		var custom_frame := menu.find_child(menu_spec.custom_frame, true, false) as Panel
		if custom_frame == null or not custom_frame.visible \
				or not _rects_close(custom_frame.get_global_rect(), panel.get_global_rect()):
			_fail("%s is missing the first-version outer frame" % context)
			return false
		if menu.find_child("GeneratedSettingsPanelArt", true, false) != null \
				or menu.find_child("GeneratedSettingsPanelArtRight", true, false) != null:
			_fail("%s still contains the rejected decorative corner art" % context)
			return false
	else:
		if glass_surface == null or glass_frame == null:
			_fail("%s is missing shared main glass layers" % context)
			return false
		if not _rects_close(glass_surface.get_global_rect(), panel.get_global_rect()):
			_fail("%s main glass does not match panel bounds" % context)
			return false
		if not _assert_glass_material(glass_surface, context):
			return false
		if not glass_surface.visible or not glass_frame.visible:
			_fail("%s hides the shared adventure glass base" % context)
			return false

	if menu_spec.has("outer_glass_surface"):
		var outer_surface := menu.find_child(menu_spec.outer_glass_surface, true, false) as ColorRect
		var outer_frame := menu.find_child(menu_spec.outer_glass_frame, true, false) as Panel
		if outer_surface == null or outer_frame == null or not outer_surface.visible or not outer_frame.visible:
			_fail("%s is missing its adventure glass outer shell" % context)
			return false
		if not _assert_glass_material(outer_surface, context):
			return false
		if not _assert_matching_glass_base(glass_surface, glass_frame, outer_surface, outer_frame, context):
			return false
		var outer_rect := outer_surface.get_global_rect()
		var panel_rect := panel.get_global_rect()
		if not _rect_contains(outer_rect, panel_rect) \
				or outer_rect.size.x < panel_rect.size.x + 32.0 \
				or outer_rect.size.y < panel_rect.size.y + 32.0:
			_fail("%s adventure glass outer shell is not visibly wider than the panel" % context)
			return false

	for content_name in menu_spec.contents:
		var content_panel := menu.find_child(content_name, true, false) as PanelContainer
		if content_panel == null:
			_fail("%s is missing content panel %s" % [context, content_name])
			return false
		var content_surface := content_panel.get_node_or_null("%sGlassSurface" % content_name) as ColorRect
		if content_surface == null or not _assert_glass_material(content_surface, context):
			_fail("%s content panel %s is missing shared glass" % [context, content_name])
			return false
		if not _rect_contains(panel.get_global_rect(), content_panel.get_global_rect()):
			_fail("%s content panel %s escapes the main panel" % [context, content_name])
			return false

	if menu_spec.has("single_column_list"):
		var grid := menu.find_child(menu_spec.single_column_list, true, false) as GridContainer
		if grid == null or grid.columns != 1:
			_fail("%s is missing its single-column compact list" % context)
			return false
		if grid.get_child_count() < 2:
			_fail("%s single-column list has too few items" % context)
			return false
		var first := grid.get_child(0) as Control
		var second := grid.get_child(1) as Control
		if first == null or second == null:
			_fail("%s compact grid children are not controls" % context)
			return false
		var first_rect := first.get_global_rect()
		var second_rect := second.get_global_rect()
		if absf(first_rect.position.x - second_rect.position.x) > RECT_TOLERANCE \
			or second_rect.position.y < first_rect.position.y + first_rect.size.y - RECT_TOLERANCE:
			_fail("%s compact list does not stack its first two items vertically" % context)
			return false
		if first_rect.size.x < panel.size.x * 0.65:
			_fail("%s compact list item is unexpectedly narrow" % context)
			return false

	var back_button := menu.find_child("BackButton", true, false) as Button
	if back_button == null:
		_fail("%s is missing the unified title bar" % context)
		return false
	if back_button.size.x < 44.0 or back_button.size.y < 44.0:
		_fail("%s back target is below 44px: %s" % [context, back_button.size])
		return false
	var title_anchor_name := str(menu_spec.get("title_anchor", "TitleUnderline"))
	var title_anchor := menu.find_child(title_anchor_name, true, false) as Control
	if title_anchor == null:
		_fail("%s is missing the unified title anchor" % context)
		return false
	if absf(title_anchor.get_global_rect().get_center().x - panel.get_global_rect().get_center().x) > RECT_TOLERANCE:
		_fail("%s title is not geometrically centered" % context)
		return false

	if menu_spec.has("frames"):
		for frame_name in menu_spec.frames:
			var frame := menu.find_child(frame_name, true, false) as Control
			var expected_style := 2
			if menu_spec.has("frame_styles"):
				expected_style = int(menu_spec.frame_styles.get(frame_name, expected_style))
			if frame == null or int(frame.get("FrameStyle")) != expected_style:
				_fail("%s frame %s does not use style %d" % [context, frame_name, expected_style])
				return false

	if menu_spec.has("icon_frames"):
		for frame_name in menu_spec.icon_frames:
			var frame := menu.find_child(frame_name, true, false) as Control
			if not _assert_first_version_frame(frame, context, frame_name, 6):
				return false

	if menu_spec.has("primary_frames"):
		for frame_name in menu_spec.primary_frames:
			var frame := menu.find_child(frame_name, true, false) as Control
			if not _assert_primary_frame(frame, context, frame_name):
				return false

	if menu_spec.has("menu_row_frames"):
		var expected_row_style := int(menu_spec.get("menu_row_style", 2))
		var configuration_scroll := menu.find_child(str(menu_spec.get("configuration_scroll", "")), true, false) as ScrollContainer
		if configuration_scroll == null:
			_fail("%s is missing its configuration scroll container" % context)
			return false
		var visible_configuration_rect := configuration_scroll.get_global_rect()
		for row_name in menu_spec.menu_row_frames:
			var row := menu.find_child(row_name, true, false) as Control
			if row == null:
				_fail("%s row %s is missing" % [context, row_name])
				return false
			var options := row.find_children("*", "OptionButton", true, false)
			var option := options[0] as OptionButton if not options.is_empty() else null
			if option == null:
				_fail("%s row %s is missing its compact value selector" % [context, row_name])
				return false
			var row_rect := row.get_global_rect()
			var option_rect := option.get_global_rect()
			if not _rect_contains(row_rect, option_rect) \
					or option_rect.position.x < row_rect.position.x + row_rect.size.x * 0.50 \
					or option_rect.size.x > row_rect.size.x * 0.50 \
					or option_rect.size.y < 44.0:
				_fail("%s row %s value selector is not confined to the right segment: %s" % [context, row_name, option_rect])
				return false
			if int(row.get("FrameStyle")) != expected_row_style:
				_fail("%s row %s does not use style %d" % [context, row_name, expected_row_style])
				return false
			if not _rect_contains(visible_configuration_rect, row.get_global_rect()):
				_fail("%s row %s is clipped or requires scrolling (row=%s, viewport=%s)" % [
					context,
					row_name,
					row.get_global_rect(),
					visible_configuration_rect,
				])
				return false
		if configuration_scroll.get_v_scroll_bar().visible:
			_fail("%s configuration rows unexpectedly require a vertical scrollbar" % context)
			return false

	if menu_spec.has("forbidden_prefix_icons"):
		for icon_name in menu_spec.forbidden_prefix_icons:
			if menu.find_child(icon_name, true, false) != null:
				_fail("%s still contains removed prefix icon %s" % [context, icon_name])
				return false

	return true


func _assert_first_version_frame(frame: Control, context: String, frame_name: String, expected_style: int) -> bool:
	if frame == null \
			or frame.get_script() == null \
			or frame.get_script().resource_path != "res://scripts/OceanMenuButtonFrame.cs" \
			or int(frame.get("FrameStyle")) != expected_style:
		_fail("%s frame %s does not use the first-version glass style" % [context, frame_name])
		return false
	if frame.get("OceanPlaqueTexture") != null or frame.get("SelectionGlowTexture") != null:
		_fail("%s frame %s unexpectedly uses a generated texture" % [context, frame_name])
		return false
	return true


func _assert_primary_frame(frame: Control, context: String, frame_name: String) -> bool:
	if frame == null \
			or frame.get_script() == null \
			or frame.get_script().resource_path != "res://scripts/OceanMenuButtonFrame.cs" \
			or int(frame.get("FrameStyle")) != 7:
		_fail("%s frame %s does not use the frosted primary style" % [context, frame_name])
		return false
	if frame.get("OceanPlaqueTexture") != null or frame.get("SelectionGlowTexture") != null:
		_fail("%s frame %s unexpectedly uses generated button art" % [context, frame_name])
		return false
	return true


func _assert_texture_path(texture: Texture2D, expected_path: String, context: String) -> bool:
	if texture == null:
		_fail("%s has no generated texture" % context)
		return false
	var source := texture
	if texture is AtlasTexture:
		source = (texture as AtlasTexture).atlas
	if source == null or source.resource_path != expected_path:
		_fail("%s uses %s instead of %s" % [context, source.resource_path if source != null else "<null>", expected_path])
		return false
	return true


func _assert_glass_material(surface: ColorRect, context: String) -> bool:
	var material := surface.material as ShaderMaterial
	if material == null or material.shader == null or material.shader.resource_path != GLASS_SHADER_PATH:
		_fail("%s does not use the adventure glass shader" % context)
		return false
	return true


func _assert_matching_glass_base(
		main_surface: ColorRect,
		main_frame: Panel,
		outer_surface: ColorRect,
		outer_frame: Panel,
		context: String) -> bool:
	var main_material := main_surface.material as ShaderMaterial
	var outer_material := outer_surface.material as ShaderMaterial
	for parameter in ["corner_radius", "blur_lod", "tint_color", "edge_light_color", "surface_opacity"]:
		if main_material.get_shader_parameter(parameter) != outer_material.get_shader_parameter(parameter):
			_fail("%s outer shell does not reuse the main adventure glass parameter %s" % [context, parameter])
			return false

	var main_style := main_frame.get_theme_stylebox("panel") as StyleBoxFlat
	var outer_style := outer_frame.get_theme_stylebox("panel") as StyleBoxFlat
	if main_style == null or outer_style == null \
			or main_style.border_color != outer_style.border_color \
			or main_style.border_width_top != outer_style.border_width_top \
			or main_style.shadow_color != outer_style.shadow_color \
			or main_style.shadow_size != outer_style.shadow_size \
			or main_style.shadow_offset != outer_style.shadow_offset:
		_fail("%s outer shell does not reuse the main adventure glass frame style" % context)
		return false
	return true


func _capture_optional_screenshot(menu_name: String, viewport_spec: Dictionary) -> bool:
	var output_dir := OS.get_environment("SECONDARY_MENU_GLASS_SCREENSHOT_DIR").strip_edges()
	if output_dir.is_empty():
		return true
	for _frame in 3:
		await process_frame
	var image := _viewport.get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport_spec.size:
		_fail("%s screenshot is empty or wrong-sized at %s" % [menu_name, viewport_spec.name])
		return false
	var output_path := output_dir.path_join(
		"secondary-menu-%s-%s.png" % [menu_name, viewport_spec.name])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s: %s" % [output_path, error_string(save_error)])
		return false
	return true


func _rect_contains(outer: Rect2, inner: Rect2) -> bool:
	return inner.position.x >= outer.position.x - RECT_TOLERANCE \
		and inner.position.y >= outer.position.y - RECT_TOLERANCE \
		and inner.end.x <= outer.end.x + RECT_TOLERANCE \
		and inner.end.y <= outer.end.y + RECT_TOLERANCE


func _rects_close(actual: Rect2, expected: Rect2) -> bool:
	return actual.position.distance_to(expected.position) <= RECT_TOLERANCE \
		and actual.size.distance_to(expected.size) <= RECT_TOLERANCE


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
	push_error("SECONDARY_MENU_GLASS_UI_SMOKE_FAIL %s" % message)
	quit(1)
