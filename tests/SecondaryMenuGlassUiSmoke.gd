extends SceneTree

const VISUAL_PROFILE_META := "secondary_menu_visual_profile"
const PAUSE_DERIVED_PROFILE := "pause-derived-core-menu"
const PAUSE_REFERENCE_PROFILE := "secondary-menu-unified"
const PANEL_TEXTURE_PATH := "res://art/ui/pause_menu/pause_panel.png"
const IDLE_TEXTURE_PATH := "res://art/ui/pause_menu/pause_button_idle.png"
const FOCUS_TEXTURE_PATH := "res://art/ui/pause_menu/pause_button_focus.png"
const PRESSED_TEXTURE_PATH := "res://art/ui/pause_menu/pause_button_pressed.png"
const DIVIDER_TEXTURE_PATH := "res://art/ui/pause_menu/pause_divider.png"
const BODY_TEXT := Color("f3f7f4")
const MINIMAL_ACTION_ACCENT := Color("22b9c0")
const MINIMAL_ROW_FRAME_STYLE := 11
const MINIMAL_ACTION_FRAME_STYLE := 12
const SAFE_PADDING := 24.0
const RECT_TOLERANCE := 2.5
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
		"design_size": Vector2(840, 592),
		"contents": ["PreviewPanel", "AvatarListPanel"],
		"minimal_actions": ["BackFrame", "ConfirmFrame"],
		"focus_control": "BackButton",
		"focus_frame": "BackFrame",
		"scrolls": ["GridScroll"],
		"body_control": "PlayerNameInput",
		"remove_center_ornaments": true,
	},
	{
		"name": "single-player",
		"path": "res://scenes/main_menu_player_count_dialog.tscn",
		"panel": "PlayerCountDialogPanel",
		"design_size": Vector2(840, 592),
		"contents": [],
		"minimal_actions": ["BackFrame", "StartGameFrame"],
		"minimal_rows": [
			"PlayersRowPanel", "DifficultyRowPanel",
			"ItemModeRowPanel", "WinningScoreRowPanel",
		],
		"ordered_rows": [
			"PlayersRowPanel", "DifficultyRowPanel",
			"ItemModeRowPanel", "WinningScoreRowPanel",
		],
		"selector_stack": [
			"PlayersRowPanel", "DifficultyRowPanel",
			"ItemModeRowPanel", "WinningScoreRowPanel",
		],
		"focus_control": "PlayerCountOption",
		"focus_frame": "PlayersRowPanel",
		"scrolls": ["ConfigurationScroll"],
		"body_control": "PlayerCountOption",
		"remove_center_ornaments": true,
	},
	{
		"name": "settings",
		"path": "res://scenes/main_menu_settings_dialog.tscn",
		"panel": "SettingsDialogPanel",
		"design_size": Vector2(840, 592),
		"contents": ["SettingsContentPanel"],
		"minimal_rows": [
			"MasterVolumeRowPanel", "MusicVolumeRowPanel", "MuteBgmRowPanel",
			"MuteSfxRowPanel",
		],
		"hidden_minimal_rows": [
			"WindowModeRowPanel", "ResolutionRowPanel", "LanguageRowPanel",
		],
		"minimal_actions": ["ResetDefaultsFrame", "DoneFrame"],
		"ordered_rows": [
			"MasterVolumeRowPanel", "MusicVolumeRowPanel", "MuteBgmRowPanel",
			"MuteSfxRowPanel",
		],
		"focus_control": "MasterVolumeSlider",
		"focus_frame": "MasterVolumeRowPanel",
		"scrolls": ["SettingsScroll"],
		"body_control": "MasterVolumeLabel",
		"settings_categories": true,
		"remove_center_ornaments": true,
		"forbid_back": true,
	},
	{
		"name": "controls",
		"path": "res://scenes/main_menu_controls_dialog.tscn",
		"panel": "ControlsDialogPanel",
		"design_size": Vector2(840, 592),
		"contents": ["ControlsContentPanel"],
		"minimal_actions": ["BackFrame", "ResetDefaultsFrame", "DoneFrame"],
		"minimal_rows": [
			"MoveUpRow", "MoveDownRow", "MoveLeftRow", "MoveRightRow",
			"ConfirmRow", "CancelRow", "PauseRow",
		],
		"ordered_rows": [
			"MoveUpRow", "MoveDownRow", "MoveLeftRow", "MoveRightRow",
			"ConfirmRow", "CancelRow", "PauseRow",
		],
		"aligned_binding_columns": true,
		"focus_control": "KeyboardButton",
		"focus_frame": "MoveUpRow",
		"scrolls": ["RowsScroll"],
		"body_control": "KeyboardButton",
		"remove_center_ornaments": true,
	},
	{
		"name": "exit",
		"path": "res://scenes/main_menu_exit_dialog.tscn",
		"panel": "ExitDialogPanel",
		"design_size": Vector2(620, 340),
		"contents": ["PromptPanel"],
		"minimal_actions": ["CancelFrame", "ConfirmFrame"],
		"focus_control": "ConfirmButton",
		"focus_frame": "ConfirmFrame",
		"body_control": "CancelButton",
		"unified_button_text_controls": ["CancelButton", "ConfirmButton"],
		"scrolls": [],
		"compact_confirmation": true,
		"forbid_back": true,
	},
	{
		"name": "pause-reference",
		"path": "res://scenes/pause_menu.tscn",
		"panel": "PauseMenuPanel",
		"reference_pause": true,
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
	_viewport.gui_embed_subwindows = true
	root.add_child(_viewport)

	var menu_filter := OS.get_environment("SECONDARY_MENU_GLASS_MENU_FILTER").strip_edges()
	for menu_spec in MENU_SPECS:
		if not menu_filter.is_empty() and menu_spec.name != menu_filter:
			continue
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
	await _wait_frames(4)

	for viewport_spec in VIEWPORT_SPECS:
		_viewport.size = viewport_spec.size
		await _wait_frames(4)
		if bool(menu_spec.get("reference_pause", false)):
			if not _audit_pause_reference(menu, menu_spec, viewport_spec):
				return false
			continue
		_release_focus()
		await _wait_frames(2)
		if not _audit_pause_derived_layout(menu, menu_spec, viewport_spec):
			return false
		if not await _capture_optional_screenshot(menu_spec.name, viewport_spec, "idle"):
			return false
		if not await _audit_focus_state(menu, menu_spec, viewport_spec):
			return false
		if not await _capture_optional_screenshot(menu_spec.name, viewport_spec, "focus"):
			return false

	menu.queue_free()
	await process_frame
	return true


func _audit_pause_derived_layout(
		menu: Control,
		menu_spec: Dictionary,
		viewport_spec: Dictionary) -> bool:
	var context := "%s %s" % [menu_spec.name, viewport_spec.name]
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport_spec.size))
	var panel := menu.find_child(menu_spec.panel, true, false) as Control
	var scrim := menu.find_child("Scrim", true, false) as ColorRect
	if panel == null or scrim == null:
		_fail("%s is missing its panel or readability veil" % context)
		return false
	if not menu.has_meta(VISUAL_PROFILE_META) \
			or str(menu.get_meta(VISUAL_PROFILE_META)) != PAUSE_DERIVED_PROFILE:
		_fail("%s does not expose %s" % [context, PAUSE_DERIVED_PROFILE])
		return false
	if not _rects_close(_get_canvas_rect(scrim), viewport_rect):
		_fail("%s readability veil does not cover the viewport" % context)
		return false

	var design_size: Vector2 = menu_spec.design_size
	var available := Vector2(viewport_spec.size) - Vector2.ONE * SAFE_PADDING * 2.0
	var expected_scale := minf(1.0, minf(available.x / design_size.x, available.y / design_size.y))
	if not panel.custom_minimum_size.is_equal_approx(design_size) \
			or not panel.has_meta("pause_derived_scale") \
			or absf(float(panel.get_meta("pause_derived_scale")) - expected_scale) > 0.002 \
			or absf(panel.scale.x - panel.scale.y) > 0.002:
		_fail("%s panel design size/scale is invalid: min=%s scale=%s expected=%s" % [
			context, panel.custom_minimum_size, panel.scale, expected_scale,
		])
		return false
	var panel_rect := _get_canvas_rect(panel)
	var expected_panel_size := design_size * expected_scale
	if panel_rect.size.distance_to(expected_panel_size) > RECT_TOLERANCE:
		_fail("%s panel screen size is %s instead of %s (layout=%s local_scale=%s root=%s canvas=%s)" % [
			context,
			panel_rect.size,
			expected_panel_size,
			panel.size,
			panel.scale,
			menu.size,
			menu.get_global_transform_with_canvas().get_scale(),
		])
		return false
	if not _rect_contains(viewport_rect, panel_rect) \
			or panel_rect.position.x < SAFE_PADDING - RECT_TOLERANCE \
			or panel_rect.position.y < SAFE_PADDING - RECT_TOLERANCE:
		_fail("%s panel escapes the safe viewport: %s" % [context, panel_rect])
		return false

	var artwork := panel.get_node_or_null("PauseDerivedPanelArtwork") as TextureRect
	if artwork == null or not artwork.visible \
			or not _texture_uses_path(artwork.texture, PANEL_TEXTURE_PATH) \
			or not _rects_close(_get_canvas_rect(artwork), panel_rect):
		_fail("%s is missing matching pause-derived panel artwork" % context)
		return false
	if bool(menu_spec.get("remove_center_ornaments", false)):
		var left_slice := artwork.get_node_or_null("PauseDerivedPanelLeft") as TextureRect
		var right_slice := artwork.get_node_or_null("PauseDerivedPanelRight") as TextureRect
		if not bool(panel.get_meta("pause_derived_center_ornaments_removed", false)) \
				or artwork.self_modulate.a > 0.01 \
				or left_slice == null or right_slice == null \
				or not _texture_uses_path(left_slice.texture, PANEL_TEXTURE_PATH) \
				or not _texture_uses_path(right_slice.texture, PANEL_TEXTURE_PATH):
			_fail("%s does not use the ornament-free panel composition" % context)
			return false
		var left_rect := _get_canvas_rect(left_slice)
		var right_rect := _get_canvas_rect(right_slice)
		if not _rect_contains(panel_rect, left_rect) or not _rect_contains(panel_rect, right_rect) \
				or absf(left_rect.end.x - right_rect.position.x) > RECT_TOLERANCE \
				or absf(left_rect.position.y - panel_rect.position.y) > RECT_TOLERANCE \
				or absf(right_rect.end.y - panel_rect.end.y) > RECT_TOLERANCE:
			_fail("%s ornament-free panel slices do not cover the panel" % context)
			return false
	if panel.get_node_or_null("GlassSurface") != null or panel.get_node_or_null("GlassFrame") != null:
		_fail("%s still installs the legacy glass panel layers" % context)
		return false

	for content_name in menu_spec.contents:
		var content := menu.find_child(content_name, true, false) as Control
		if content == null or not _rect_contains(panel_rect, _get_canvas_rect(content)):
			_fail("%s content section %s escapes the panel" % [context, content_name])
			return false
		if content.find_child("%sGlassSurface" % content_name, true, false) != null:
			_fail("%s content section %s still uses nested glass" % [context, content_name])
			return false

	if bool(menu_spec.get("compact_confirmation", false)) \
			and not _audit_compact_confirmation(menu, panel_rect, expected_scale, context):
		return false

	var title_underline := menu.find_child("TitleUnderline", true, false) as Control
	var title_divider := title_underline.get_node_or_null("PauseDerivedDivider") as TextureRect \
		if title_underline != null else null
	if title_divider == null or not _texture_uses_path(title_divider.texture, DIVIDER_TEXTURE_PATH):
		_fail("%s does not use the generated title divider" % context)
		return false

	for frame_name in menu_spec.get("frames", []):
		if not _assert_pause_derived_frame(menu, frame_name, context):
			return false
	for frame_name in menu_spec.get("compact_action_frames", []):
		if not _assert_pause_derived_compact_action_frame(menu, frame_name, context):
			return false
	for frame_name in menu_spec.get("primary_frames", []):
		if not _assert_pause_derived_primary_frame(menu, frame_name, context):
			return false
	for frame_name in menu_spec.get("selector_frames", []):
		if not _assert_selector_frame(menu, frame_name, context):
			return false
	for frame_name in menu_spec.get("minimal_rows", []):
		if not _assert_minimal_frame(
				menu, frame_name, MINIMAL_ROW_FRAME_STYLE, true, true, context):
			return false
	for frame_name in menu_spec.get("hidden_minimal_rows", []):
		if not _assert_minimal_frame(
				menu, frame_name, MINIMAL_ROW_FRAME_STYLE, true, false, context):
			return false
	for frame_name in menu_spec.get("minimal_actions", []):
		if not _assert_minimal_frame(
				menu, frame_name, MINIMAL_ACTION_FRAME_STYLE, false, true, context):
			return false
	if bool(menu_spec.get("settings_categories", false)) \
			and not _assert_settings_categories(menu, panel_rect, context):
		return false
	if menu_spec.has("ordered_rows") \
			and not _assert_rows_in_visual_order(menu, menu_spec.ordered_rows, context):
		return false
	if bool(menu_spec.get("aligned_binding_columns", false)) \
			and not _assert_binding_columns(menu, menu_spec.ordered_rows, context):
		return false
	if menu_spec.has("selector_stack") \
			and not _assert_selector_stack(menu, menu_spec.selector_stack, panel_rect, context):
		return false
	for scroll_name in menu_spec.scrolls:
		if not _assert_vertical_scroll(menu, scroll_name, panel_rect, context):
			return false

	var back_button := menu.find_child("BackButton", true, false) as Button
	if bool(menu_spec.get("forbid_back", false)):
		if back_button != null or menu.find_child("BackFrame", true, false) != null:
			_fail("%s still exposes a forbidden title-bar back action" % context)
			return false
	elif back_button == null or back_button.focus_mode == Control.FOCUS_NONE \
				or back_button.size.x < 44.0 or back_button.size.y < 44.0:
		_fail("%s back action is missing or below the 44px baseline" % context)
		return false

	var body_control := menu.find_child(menu_spec.body_control, true, false) as Control
	if body_control == null or not body_control.get_theme_color("font_color").is_equal_approx(BODY_TEXT):
		_fail("%s does not apply the pause-derived body text palette" % context)
		return false
	for control_name in menu_spec.get("unified_button_text_controls", []):
		var text_control := menu.find_child(control_name, true, false) as Control
		if text_control == null or not text_control.get_theme_color("font_color").is_equal_approx(BODY_TEXT):
			_fail("%s button text is not using the unified body color: %s" % [context, control_name])
			return false

	return true


func _audit_compact_confirmation(
		menu: Control,
		panel_rect: Rect2,
		screen_scale: float,
		context: String) -> bool:
	var header := menu.find_child("Header", true, false) as Control
	var prompt := menu.find_child("PromptPanel", true, false) as PanelContainer
	var actions := menu.find_child("DialogActions", true, false) as Control
	if header == null or prompt == null or actions == null:
		_fail("%s is missing its compact confirmation sections" % context)
		return false

	var header_rect := _get_canvas_rect(header)
	var prompt_rect := _get_canvas_rect(prompt)
	var actions_rect := _get_canvas_rect(actions)
	var maximum_section_gap := 14.0 * screen_scale + RECT_TOLERANCE
	if prompt_rect.position.y - header_rect.end.y > maximum_section_gap \
			or actions_rect.position.y - prompt_rect.end.y > maximum_section_gap:
		_fail("%s confirmation sections are too widely separated" % context)
		return false
	if prompt_rect.size.y > 68.0 * screen_scale + RECT_TOLERANCE:
		_fail("%s warning row expanded into unused vertical space" % context)
		return false
	if not _rect_contains(panel_rect, header_rect) \
			or not _rect_contains(panel_rect, prompt_rect) \
			or not _rect_contains(panel_rect, actions_rect):
		_fail("%s compact confirmation content escapes the panel" % context)
		return false

	if not prompt.get_theme_stylebox("panel") is StyleBoxEmpty:
		_fail("%s explanatory text should not be enclosed by an inner panel" % context)
		return false
	return true


func _assert_pause_derived_frame(menu: Control, frame_name: String, context: String) -> bool:
	var frame := menu.find_child(frame_name, true, false) as Control
	if frame == null or int(frame.get("FrameStyle")) != 0 \
			or bool(frame.get("ShowIdleTexture")) \
			or not bool(frame.get("UseNinePatchTexture")) \
			or float(frame.get("TexturePatchMarginHorizontal")) < 8.0 \
			or float(frame.get("TexturePatchMarginVertical")) < 8.0:
		_fail("%s frame %s does not use transparent-idle nine-patch state art" % [context, frame_name])
		return false
	if not _texture_uses_path(frame.get("OceanPlaqueTexture") as Texture2D, IDLE_TEXTURE_PATH) \
			or not _texture_uses_path(frame.get("FocusTexture") as Texture2D, FOCUS_TEXTURE_PATH) \
			or not _texture_uses_path(frame.get("PressedTexture") as Texture2D, PRESSED_TEXTURE_PATH):
		_fail("%s frame %s is missing a generated interaction state" % [context, frame_name])
		return false
	var divider := frame.find_child("PauseDerivedRowDivider", true, false) as TextureRect
	if divider == null or not _texture_uses_path(divider.texture, DIVIDER_TEXTURE_PATH):
		_fail("%s frame %s is missing the generated row divider" % [context, frame_name])
		return false
	var divider_screen_size := _get_canvas_rect(divider).size
	if divider_screen_size.y > 4.0 or divider_screen_size.x > _get_canvas_rect(frame).size.x:
		_fail("%s frame %s divider is stretched to %s" % [context, frame_name, divider_screen_size])
		return false
	var frame_screen_size := _get_canvas_rect(frame).size
	if frame_screen_size.x < 44.0 or frame_screen_size.y < 44.0:
		_fail("%s frame %s is below the 44px interaction baseline: %s" % [context, frame_name, frame_screen_size])
		return false
	return true


func _assert_pause_derived_primary_frame(menu: Control, frame_name: String, context: String) -> bool:
	var frame := menu.find_child(frame_name, true, false) as Control
	if frame == null or int(frame.get("FrameStyle")) != 0 \
			or not bool(frame.get("ShowIdleTexture")) \
			or not bool(frame.get("UseNinePatchTexture")) \
			or frame.find_child("PauseDerivedRowDivider", true, false) != null:
		_fail("%s primary frame %s is not visibly button-like at rest" % [context, frame_name])
		return false
	if not _texture_uses_path(frame.get("OceanPlaqueTexture") as Texture2D, IDLE_TEXTURE_PATH) \
			or not _texture_uses_path(frame.get("FocusTexture") as Texture2D, FOCUS_TEXTURE_PATH) \
			or not _texture_uses_path(frame.get("PressedTexture") as Texture2D, PRESSED_TEXTURE_PATH):
		_fail("%s primary frame %s is missing an interaction state" % [context, frame_name])
		return false
	var frame_screen_size := _get_canvas_rect(frame).size
	if frame_screen_size.x < 220.0 or frame_screen_size.y < 44.0:
		_fail("%s primary frame %s is too small to read as the main action: %s" % [
			context, frame_name, frame_screen_size,
		])
		return false
	return true


func _assert_pause_derived_compact_action_frame(menu: Control, frame_name: String, context: String) -> bool:
	var frame := menu.find_child(frame_name, true, false) as Control
	if frame == null or int(frame.get("FrameStyle")) != 0 \
			or not bool(frame.get("ShowIdleTexture")) \
			or not bool(frame.get("UseNinePatchTexture")) \
			or frame.find_child("PauseDerivedRowDivider", true, false) != null:
		_fail("%s compact action %s is not visibly button-like at rest" % [context, frame_name])
		return false
	if not _texture_uses_path(frame.get("OceanPlaqueTexture") as Texture2D, IDLE_TEXTURE_PATH) \
			or not _texture_uses_path(frame.get("FocusTexture") as Texture2D, FOCUS_TEXTURE_PATH) \
			or not _texture_uses_path(frame.get("PressedTexture") as Texture2D, PRESSED_TEXTURE_PATH):
		_fail("%s compact action %s is missing an interaction state" % [context, frame_name])
		return false
	var frame_screen_size := _get_canvas_rect(frame).size
	if frame_screen_size.x < 44.0 or frame_screen_size.y < 44.0:
		_fail("%s compact action %s is below the 44px baseline: %s" % [
			context, frame_name, frame_screen_size,
		])
		return false
	return true


func _assert_selector_frame(menu: Control, frame_name: String, context: String) -> bool:
	var frame := menu.find_child(frame_name, true, false) as Control
	if frame == null or not bool(frame.get("SuppressVisuals")) \
			or bool(frame.get("ShowIdleTexture")) \
			or frame.find_child("PauseDerivedRowDivider", true, false) != null:
		_fail("%s selector frame %s still renders a full-row treatment" % [context, frame_name])
		return false
	var frame_screen_size := _get_canvas_rect(frame).size
	if frame_screen_size.x < 44.0 or frame_screen_size.y < 44.0:
		_fail("%s selector frame %s is below the 44px baseline" % [context, frame_name])
		return false
	return true


func _assert_minimal_frame(
		menu: Control,
		frame_name: String,
		expected_style: int,
		expect_suppressed_visuals: bool,
		require_visible_size: bool,
		context: String) -> bool:
	var frame := menu.find_child(frame_name, true, false) as Control
	if frame == null or int(frame.get("FrameStyle")) != expected_style \
			or bool(frame.get("SuppressVisuals")) != expect_suppressed_visuals \
			or bool(frame.get("ShowIdleTexture")) \
			or bool(frame.get("UseNinePatchTexture")) \
			or bool(frame.get("UseToolRowIdleBackdrop")):
		_fail("%s frame %s does not use the minimal settings treatment" % [context, frame_name])
		return false
	if frame.get("OceanPlaqueTexture") != null or frame.get("FocusTexture") != null \
			or frame.get("PressedTexture") != null \
			or frame.find_child("PauseDerivedRowDivider", true, false) != null:
		_fail("%s frame %s still uses textured state art or a row divider" % [context, frame_name])
		return false
	if expected_style == MINIMAL_ACTION_FRAME_STYLE:
		var accent: Color = frame.get("AccentColor")
		if not accent.is_equal_approx(MINIMAL_ACTION_ACCENT):
			_fail("%s frame %s does not share the settings hover accent" % [context, frame_name])
			return false
	var frame_screen_size := _get_canvas_rect(frame).size
	if require_visible_size and (frame_screen_size.x < 44.0 or frame_screen_size.y < 44.0):
		_fail("%s frame %s is below the 44px interaction baseline: %s" % [context, frame_name, frame_screen_size])
		return false
	return true


func _assert_settings_categories(menu: Control, panel_rect: Rect2, context: String) -> bool:
	var audio_button := menu.find_child("AudioCategoryButton", true, false) as Button
	var display_button := menu.find_child("DisplayCategoryButton", true, false) as Button
	var language_button := menu.find_child("LanguageCategoryButton", true, false) as Button
	var controls_button := menu.find_child("ControlsCategoryButton", true, false) as Button
	var category_panel := menu.find_child("CategoryPanel", true, false) as Control
	var content_panel := menu.find_child("SettingsContentPanel", true, false) as Control
	var content_divider := menu.find_child("ContentDivider", true, false) as ColorRect
	var legacy_controls_button := menu.find_child("ControlsButton", true, false) as Button
	var legacy_controls_row := menu.find_child("ControlsRowPanel", true, false) as Control
	var buttons := [audio_button, display_button, language_button, controls_button]
	if buttons.any(func(button): return button == null) or category_panel == null \
			or content_panel == null or content_divider == null:
		_fail("%s is missing its categorized settings shell" % context)
		return false
	if controls_button.toggle_mode or legacy_controls_button != null or legacy_controls_row != null:
		_fail("%s still requires an intermediate controls edit step" % context)
		return false
	for button in buttons:
		if button.focus_mode == Control.FOCUS_NONE or _get_canvas_rect(button).size.y < 44.0:
			_fail("%s category button %s is not a valid focus target" % [context, button.name])
			return false
	var category_rect := _get_canvas_rect(category_panel)
	var content_rect := _get_canvas_rect(content_panel)
	if not _rect_contains(panel_rect, category_rect) or not _rect_contains(panel_rect, content_rect) \
			or category_rect.intersects(content_rect, true) \
			or _get_canvas_rect(content_divider).size.x > 2.5:
		_fail("%s categorized settings columns overlap or escape the panel" % context)
		return false
	var reset_frame := menu.find_child("ResetDefaultsFrame", true, false) as Control
	var done_frame := menu.find_child("DoneFrame", true, false) as Control
	if reset_frame == null or done_frame == null:
		_fail("%s is missing its settings action group" % context)
		return false
	var reset_rect := _get_canvas_rect(reset_frame)
	var done_rect := _get_canvas_rect(done_frame)
	var action_gap := reset_rect.position.x - done_rect.end.x
	if action_gap < -RECT_TOLERANCE or action_gap > 24.0 \
			or absf(reset_rect.position.y - done_rect.position.y) > RECT_TOLERANCE:
		_fail("%s settings actions are not grouped together: gap=%s" % [context, action_gap])
		return false
	var reset_accent: Color = reset_frame.get("AccentColor")
	var done_accent: Color = done_frame.get("AccentColor")
	if not reset_accent.is_equal_approx(done_accent):
		_fail("%s settings actions do not share the same accent color" % context)
		return false
	if not _assert_settings_category_state(menu, "audio", context):
		return false
	var settings_scroll := menu.find_child("SettingsScroll", true, false) as ScrollContainer
	if settings_scroll == null or settings_scroll.get_v_scroll_bar().visible:
		_fail("%s audio category should fit without vertical scrolling" % context)
		return false
	display_button.emit_signal("pressed")
	if not _assert_settings_category_state(menu, "display", context):
		return false
	language_button.emit_signal("pressed")
	if not _assert_settings_category_state(menu, "language", context):
		return false
	controls_button.emit_signal("pressed")
	if not _assert_settings_category_state(menu, "language", context):
		return false
	audio_button.emit_signal("pressed")
	return _assert_settings_category_state(menu, "audio", context)


func _assert_binding_columns(menu: Control, row_names: Array, context: String) -> bool:
	var keyboard_header := menu.find_child("KeyboardHeader", true, false) as Label
	var gamepad_header := menu.find_child("GamepadHeader", true, false) as Label
	if keyboard_header == null or gamepad_header == null:
		_fail("%s is missing its binding column headers" % context)
		return false
	var keyboard_header_rect := _get_canvas_rect(keyboard_header)
	var gamepad_header_rect := _get_canvas_rect(gamepad_header)
	for row_name in row_names:
		var row := menu.find_child(row_name, true, false) as Control
		var keyboard_button := row.find_child("KeyboardButton", true, false) as Button \
			if row != null else null
		var gamepad_button := row.find_child("GamepadButton", true, false) as Button \
			if row != null else null
		if keyboard_button == null or gamepad_button == null:
			_fail("%s row %s is missing a binding button" % [context, row_name])
			return false
		var keyboard_rect := _get_canvas_rect(keyboard_button)
		var gamepad_rect := _get_canvas_rect(gamepad_button)
		var row_rect := _get_canvas_rect(row)
		var keyboard_delta := absf(keyboard_header_rect.get_center().x - keyboard_rect.get_center().x)
		var gamepad_delta := absf(gamepad_header_rect.get_center().x - gamepad_rect.get_center().x)
		if keyboard_delta > RECT_TOLERANCE or gamepad_delta > RECT_TOLERANCE:
			_fail("%s row %s binding columns are misaligned: keyboard=%s gamepad=%s headers=%s/%s buttons=%s/%s" % [
				context, row_name, keyboard_delta, gamepad_delta,
				keyboard_header_rect, gamepad_header_rect, keyboard_rect, gamepad_rect,
			])
			return false
		var trailing_gap := row_rect.end.x - gamepad_rect.end.x
		if absf(keyboard_rect.size.x - gamepad_rect.size.x) > RECT_TOLERANCE \
				or trailing_gap > row_rect.size.x * 0.05:
			_fail("%s row %s does not use the available width: keyboard=%s gamepad=%s trailing=%s" % [
				context, row_name, keyboard_rect.size.x, gamepad_rect.size.x, trailing_gap,
			])
			return false
	return true


func _assert_settings_category_state(menu: Control, expected: String, context: String) -> bool:
	var button_names := {
		"audio": "AudioCategoryButton",
		"display": "DisplayCategoryButton",
		"language": "LanguageCategoryButton",
		"controls_entry": "ControlsCategoryButton",
	}
	for category in button_names:
		var button := menu.find_child(button_names[category], true, false) as Button
		if button == null or button.button_pressed != (category == expected):
			_fail("%s category selection state is invalid for %s" % [context, expected])
			return false
	var expected_visibility := {
		"AudioVolumeSectionLabel": expected == "audio",
		"AudioMuteSectionLabel": expected == "audio",
		"MasterVolumeRowPanel": expected == "audio",
		"MusicVolumeRowPanel": expected == "audio",
		"MuteBgmRowPanel": expected == "audio",
		"MuteSfxRowPanel": expected == "audio",
		"WindowModeRowPanel": expected == "display",
		"ResolutionRowPanel": expected == "display",
		"LanguageRowPanel": expected == "language",
	}
	for node_name in expected_visibility:
		var control := menu.find_child(node_name, true, false) as Control
		if control == null or control.visible != bool(expected_visibility[node_name]):
			_fail("%s category %s has invalid visibility for %s" % [context, expected, node_name])
			return false
	return true


func _assert_rows_in_visual_order(menu: Control, row_names: Array, context: String) -> bool:
	var previous_rect := Rect2()
	for row_index in row_names.size():
		var row := menu.find_child(row_names[row_index], true, false) as Control
		if row == null:
			_fail("%s is missing row %s" % [context, row_names[row_index]])
			return false
		var row_rect := _get_canvas_rect(row)
		if row_index > 0 and previous_rect.end.y > row_rect.position.y + RECT_TOLERANCE:
			_fail("%s rows overlap or are out of visual order near %s" % [context, row.name])
			return false
		previous_rect = row_rect
	return true


func _assert_selector_stack(
		menu: Control,
		row_names: Array,
		panel_rect: Rect2,
		context: String) -> bool:
	var grid := menu.find_child("ConfigurationRows", true, false) as GridContainer
	if grid == null or grid.columns != 1 or row_names.size() != 4:
		_fail("%s does not use the four-item selector stack" % context)
		return false
	var first_rect := Rect2()
	var previous_rect := Rect2()
	for row_index in row_names.size():
		var row := menu.find_child(row_names[row_index], true, false) as Control
		if row == null:
			_fail("%s is missing selector stack row %s" % [context, row_names[row_index]])
			return false
		var row_rect := _get_canvas_rect(row)
		if row_index == 0:
			first_rect = row_rect
		if absf(row_rect.get_center().x - panel_rect.get_center().x) > RECT_TOLERANCE \
				or absf(row_rect.size.x - first_rect.size.x) > RECT_TOLERANCE \
				or row_rect.size.x < panel_rect.size.x * 0.55 \
				or row_rect.size.x > panel_rect.size.x * 0.75:
			_fail("%s selector stack row %s is not centered or consistently sized" % [context, row_index])
			return false
		if row_index > 0 and row_rect.position.y - previous_rect.end.y < 4.0:
			_fail("%s selector stack rows overlap" % context)
			return false
		previous_rect = row_rect
	return true


func _assert_vertical_scroll(
		menu: Control,
		scroll_name: String,
		panel_rect: Rect2,
		context: String) -> bool:
	var scroll := menu.find_child(scroll_name, true, false) as ScrollContainer
	if scroll == null or scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED \
			or scroll.get_h_scroll_bar().visible \
			or not _rect_contains(panel_rect, _get_canvas_rect(scroll)):
		_fail("%s scroll %s has invalid bounds or horizontal scrolling" % [context, scroll_name])
		return false
	return true


func _audit_focus_state(menu: Control, menu_spec: Dictionary, viewport_spec: Dictionary) -> bool:
	var context := "%s %s" % [menu_spec.name, viewport_spec.name]
	var focus_control := menu.find_child(menu_spec.focus_control, true, false) as Control
	var focus_frame := menu.find_child(menu_spec.focus_frame, true, false) as Control
	if focus_control == null or focus_frame == null or focus_control.focus_mode == Control.FOCUS_NONE:
		_fail("%s is missing its focus audit target" % context)
		return false
	focus_control.grab_focus()
	await _wait_frames(8)
	if not focus_control.has_focus() or float(focus_frame.call("DebugGetGlow")) <= 0.08:
		_fail("%s focus does not activate the pause-derived frame" % context)
		return false
	return true


func _audit_pause_reference(menu: Control, menu_spec: Dictionary, viewport_spec: Dictionary) -> bool:
	var context := "%s %s" % [menu_spec.name, viewport_spec.name]
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport_spec.size))
	var panel := menu.find_child(menu_spec.panel, true, false) as Control
	var artwork := menu.find_child("PauseMenuArtwork", true, false) as TextureRect
	if panel == null or artwork == null or not _rect_contains(viewport_rect, _get_canvas_rect(panel)) \
			or not _texture_uses_path(artwork.texture, PANEL_TEXTURE_PATH) \
			or not menu.has_meta(VISUAL_PROFILE_META) \
			or str(menu.get_meta(VISUAL_PROFILE_META)) != PAUSE_REFERENCE_PROFILE:
		_fail("%s no longer matches the approved pause-menu reference" % context)
		return false
	for frame_name in ["ContinueFrame", "ReturnToMainMenuFrame", "RestartFrame"]:
		var frame := menu.find_child(frame_name, true, false) as Control
		if frame == null or bool(frame.get("ShowIdleTexture")) \
				or not _texture_uses_path(frame.get("FocusTexture") as Texture2D, FOCUS_TEXTURE_PATH) \
				or not _texture_uses_path(frame.get("PressedTexture") as Texture2D, PRESSED_TEXTURE_PATH):
			_fail("%s reference frame %s changed unexpectedly" % [context, frame_name])
			return false
	return true


func _capture_optional_screenshot(menu_name: String, viewport_spec: Dictionary, state: String) -> bool:
	var output_dir := OS.get_environment("SECONDARY_MENU_GLASS_SCREENSHOT_DIR").strip_edges()
	if output_dir.is_empty():
		return true
	await RenderingServer.frame_post_draw
	var image := _viewport.get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport_spec.size \
			or not _has_rendered_pixels(image):
		_fail("%s %s screenshot is empty at %s" % [menu_name, state, viewport_spec.name])
		return false
	var output_path := output_dir.path_join(
		"secondary-menu-%s-%s-%s.png" % [menu_name, viewport_spec.name, state])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s: %s" % [output_path, error_string(save_error)])
		return false
	return true


func _texture_uses_path(texture: Texture2D, expected_path: String) -> bool:
	if texture == null:
		return false
	var source := texture
	if texture is AtlasTexture:
		source = (texture as AtlasTexture).atlas
	return source != null and source.resource_path == expected_path


func _release_focus() -> void:
	var focus_owner := _viewport.gui_get_focus_owner()
	if focus_owner != null:
		focus_owner.release_focus()


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _rect_contains(outer: Rect2, inner: Rect2) -> bool:
	return inner.position.x >= outer.position.x - RECT_TOLERANCE \
		and inner.position.y >= outer.position.y - RECT_TOLERANCE \
		and inner.end.x <= outer.end.x + RECT_TOLERANCE \
		and inner.end.y <= outer.end.y + RECT_TOLERANCE


func _rects_close(actual: Rect2, expected: Rect2) -> bool:
	return actual.position.distance_to(expected.position) <= RECT_TOLERANCE \
		and actual.size.distance_to(expected.size) <= RECT_TOLERANCE


func _get_canvas_rect(control: Control) -> Rect2:
	var canvas_transform := control.get_global_transform_with_canvas()
	var top_left := canvas_transform * Vector2.ZERO
	var bottom_right := canvas_transform * control.size
	return Rect2(top_left, bottom_right - top_left)


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
	push_error("SECONDARY_MENU_GLASS_UI_SMOKE_FAIL %s" % message)
	quit(1)
