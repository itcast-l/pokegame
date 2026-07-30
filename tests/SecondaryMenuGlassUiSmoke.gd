extends SceneTree

const GLASS_SHADER_PATH := "res://shaders/adventure_menu_glass.gdshader"
const VISUAL_PROFILE_META := "secondary_menu_visual_profile"
const UNIFIED_VISUAL_PROFILE := "secondary-menu-unified"
const SECONDARY_ACTION_ACCENT := Color(0.62, 1.0, 0.94, 0.84)
const PRIMARY_ACTION_ACCENT := Color(0.42, 0.92, 0.94, 0.96)
const DANGER_ACTION_ACCENT := Color(0.90, 0.52, 0.54, 0.84)
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
		"contents": ["PreviewPanel", "AvatarListPanel"],
		"visual_profile": "shared-default",
	},
	{
		"name": "single-player",
		"path": "res://scenes/main_menu_player_count_dialog.tscn",
		"panel": "PlayerCountDialogPanel",
		"contents": [],
		"title_anchor": "PlayerCountTitle",
		"visual_profile": UNIFIED_VISUAL_PROFILE,
		"secondary_frames": ["BackFrame"],
		"primary_frame": "StartGameFrame",
	},
	{
		"name": "settings",
		"path": "res://scenes/main_menu_settings_dialog.tscn",
		"panel": "SettingsDialogPanel",
		"contents": ["SettingsContentPanel"],
		"visual_profile": UNIFIED_VISUAL_PROFILE,
		"secondary_frames": ["ResetDefaultsFrame"],
		"primary_frame": "DoneFrame",
		"forbid_back": true,
	},
	{
		"name": "controls",
		"path": "res://scenes/main_menu_controls_dialog.tscn",
		"panel": "ControlsDialogPanel",
		"contents": ["ControlsContentPanel"],
		"visual_profile": UNIFIED_VISUAL_PROFILE,
		"secondary_frames": ["BackFrame", "ResetDefaultsFrame"],
		"primary_frame": "DoneFrame",
	},
	{
		"name": "exit",
		"path": "res://scenes/main_menu_exit_dialog.tscn",
		"panel": "ExitDialogPanel",
		"contents": ["PromptPanel"],
		"visual_profile": UNIFIED_VISUAL_PROFILE,
		"secondary_frames": ["CancelFrame"],
		"danger_frames": ["ConfirmFrame"],
		"forbid_back": true,
	},
	{
		"name": "pause",
		"path": "res://scenes/pause_menu.tscn",
		"panel": "PauseMenuPanel",
		"contents": [],
		"visual_profile": UNIFIED_VISUAL_PROFILE,
		"image_panel": true,
		"title_anchor": "PauseDivider",
		"pause_generated_asset_paths": {
			"panel": "res://art/ui/pause_menu/pause_panel.png",
			"idle": "res://art/ui/pause_menu/pause_button_idle.png",
			"focus": "res://art/ui/pause_menu/pause_button_focus.png",
			"pressed": "res://art/ui/pause_menu/pause_button_pressed.png",
			"divider": "res://art/ui/pause_menu/pause_divider.png",
		},
		"forbid_back": true,
		"pause_standard_vertical_layout": true,
		"forbidden_nodes": [
			"PauseMenuMessage",
			"ContinueIcon",
			"ReturnIcon",
			"RestartIcon",
			"QuitIcon",
			"QuitFrame",
			"QuitLabel",
			"QuitButton",
			"PauseQuitConfirmRoot",
			"RouteStemTop",
			"RouteBranchLeft",
			"RouteBranchRight",
			"RouteStemBottom",
			"CommandRuleTop",
			"CommandDividerLeft",
			"CommandDividerRight",
			"CommandRuleBottom",
			"ActionListRail",
			"ActionDividerOne",
			"ActionDividerTwo",
			"ActionDividerThree",
		],
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
	if bool(menu_spec.get("image_panel", false)):
		if not _assert_pause_generated_assets(menu, panel, menu_spec, context):
			return false
	else:
		var glass_surface := panel.get_node_or_null("GlassSurface") as ColorRect
		var glass_frame := panel.get_node_or_null("GlassFrame") as Panel
		if glass_surface == null or glass_frame == null \
				or not glass_surface.visible or not glass_frame.visible:
			_fail("%s is missing visible shared glass layers" % context)
			return false
		if not _rects_close(glass_surface.get_global_rect(), panel.get_global_rect()):
			_fail("%s main glass does not match panel bounds" % context)
			return false
		if not _assert_glass_material(glass_surface, context):
			return false
	if menu_spec.has("visual_profile"):
		if not menu.has_meta(VISUAL_PROFILE_META) \
				or str(menu.get_meta(VISUAL_PROFILE_META)) != str(menu_spec.visual_profile):
			_fail("%s does not expose visual profile %s" % [context, menu_spec.visual_profile])
			return false
	if menu_spec.has("secondary_frames"):
		for frame_name in menu_spec.secondary_frames:
			if not _assert_action_frame_accent(
					menu, frame_name, SECONDARY_ACTION_ACCENT, context, menu_spec, "secondary_action_frame_style"):
				return false
	if menu_spec.has("primary_frame") \
			and not _assert_action_frame_accent(
				menu, menu_spec.primary_frame, PRIMARY_ACTION_ACCENT, context, menu_spec, "primary_action_frame_style"):
		return false
	if menu_spec.has("danger_frames"):
		for frame_name in menu_spec.danger_frames:
			if not _assert_action_frame_accent(
					menu, frame_name, DANGER_ACTION_ACCENT, context, menu_spec, "danger_action_frame_style"):
				return false
	if menu_spec.has("forbidden_nodes"):
		for node_name in menu_spec.forbidden_nodes:
			if menu.find_child(node_name, true, false) != null:
				_fail("%s still contains removed node %s" % [context, node_name])
				return false
	if bool(menu_spec.get("pause_standard_vertical_layout", false)) \
			and not _assert_pause_standard_vertical_layout(menu, panel, context):
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

	var back_button := menu.find_child("BackButton", true, false) as Button
	if bool(menu_spec.get("forbid_back", false)):
		if back_button != null or menu.find_child("BackFrame", true, false) != null:
			_fail("%s still exposes the removed back action" % context)
			return false
	else:
		if back_button == null:
			_fail("%s is missing the unified title bar" % context)
			return false
		if back_button.size.x < 44.0 or back_button.size.y < 44.0:
			_fail("%s back target is below 44px: %s" % [context, back_button.size])
			return false
		if back_button.focus_mode == Control.FOCUS_NONE:
			_fail("%s back target is not keyboard or gamepad focusable" % context)
			return false
	var title_anchor_name := str(menu_spec.get("title_anchor", "TitleUnderline"))
	var title_anchor := menu.find_child(title_anchor_name, true, false) as Control
	if title_anchor == null:
		_fail("%s is missing the unified title anchor" % context)
		return false
	if absf(title_anchor.get_global_rect().get_center().x - panel.get_global_rect().get_center().x) > RECT_TOLERANCE:
		_fail("%s title is not geometrically centered" % context)
		return false

	return true


func _assert_pause_generated_assets(
		menu: Control,
		panel: Control,
		menu_spec: Dictionary,
		context: String) -> bool:
	var expected: Dictionary = menu_spec.pause_generated_asset_paths
	var artwork := menu.find_child("PauseMenuArtwork", true, false) as TextureRect
	var divider_names := ["PauseDivider", "PauseRowDividerOne", "PauseRowDividerTwo"]
	if artwork == null:
		_fail("%s is missing its generated panel artwork" % context)
		return false
	if not _texture_uses_path(artwork.texture, str(expected.panel)):
		_fail("%s does not use the generated pause panel texture" % context)
		return false
	for divider_name in divider_names:
		var divider := menu.find_child(divider_name, true, false) as TextureRect
		if divider == null or not _texture_uses_path(divider.texture, str(expected.divider)):
			_fail("%s divider %s does not use the generated divider texture" % [context, divider_name])
			return false
	if not _rects_close(artwork.get_global_rect(), panel.get_global_rect()):
		_fail("%s generated panel artwork does not match panel bounds" % context)
		return false

	for frame_name in ["ContinueFrame", "ReturnToMainMenuFrame", "RestartFrame"]:
		var frame := menu.find_child(frame_name, true, false) as Control
		if frame == null or int(frame.get("FrameStyle")) != 0:
			_fail("%s action frame %s is not using its generated plaque texture" % [context, frame_name])
			return false
		if not _texture_uses_path(frame.get("OceanPlaqueTexture") as Texture2D, str(expected.idle)) \
				or not _texture_uses_path(frame.get("FocusTexture") as Texture2D, str(expected.focus)) \
				or not _texture_uses_path(frame.get("PressedTexture") as Texture2D, str(expected.pressed)):
			_fail("%s action frame %s is missing one or more generated button states" % [context, frame_name])
			return false
		if bool(frame.get("ShowIdleTexture")):
			_fail("%s action frame %s still renders the rejected idle button box" % [context, frame_name])
			return false

	return true


func _texture_uses_path(texture: Texture2D, expected_path: String) -> bool:
	if texture == null:
		return false
	var source := texture
	if texture is AtlasTexture:
		source = (texture as AtlasTexture).atlas
	return source != null and source.resource_path == expected_path


func _assert_pause_standard_vertical_layout(menu: Control, panel: Control, context: String) -> bool:
	var continue_frame := menu.find_child("ContinueFrame", true, false) as Control
	var return_frame := menu.find_child("ReturnToMainMenuFrame", true, false) as Control
	var restart_frame := menu.find_child("RestartFrame", true, false) as Control
	if continue_frame == null or return_frame == null or restart_frame == null:
		_fail("%s is missing one or more pause menu rows" % context)
		return false

	var continue_rect := continue_frame.get_global_rect()
	var return_rect := return_frame.get_global_rect()
	var restart_rect := restart_frame.get_global_rect()
	var panel_rect := panel.get_global_rect()
	if panel_rect.size.x > 660.0 or panel_rect.size.x > panel_rect.size.y * 1.5:
		_fail("%s pause panel is still an oversized horizontal layout: %s" % [context, panel_rect])
		return false
	for frame_rect in [continue_rect, return_rect, restart_rect]:
		if not _rect_contains(panel_rect, frame_rect):
			_fail("%s pause row escapes the panel: %s" % [context, frame_rect])
			return false
		if frame_rect.size.x < 44.0 or frame_rect.size.y < 44.0:
			_fail("%s pause row is below the 44px target: %s" % [context, frame_rect])
			return false

	var panel_center_x := panel_rect.get_center().x
	var frames := [continue_frame, return_frame, restart_frame]
	var rects := [continue_rect, return_rect, restart_rect]
	var label_names := ["ContinueLabel", "ReturnToMainMenuLabel", "RestartLabel"]
	for frame_index in range(frames.size()):
		var frame: Control = frames[frame_index]
		var frame_rect: Rect2 = rects[frame_index]
		if absf(frame_rect.get_center().x - panel_center_x) > RECT_TOLERANCE:
			_fail("%s pause row %s is not centered" % [context, frame.name])
			return false
		if absf(frame_rect.size.x - continue_rect.size.x) > RECT_TOLERANCE:
			_fail("%s pause row %s does not share the standard row width" % [context, frame.name])
			return false
		if absf(frame.rotation) > 0.001:
			_fail("%s pause row %s is still rotated" % [context, frame.name])
			return false
		var label := menu.find_child(label_names[frame_index], true, false) as Label
		if label == null or label.horizontal_alignment != HORIZONTAL_ALIGNMENT_CENTER:
			_fail("%s pause row %s text is not center-aligned" % [context, frame.name])
			return false
		if absf(label.get_global_rect().get_center().x - frame_rect.get_center().x) > RECT_TOLERANCE:
			_fail("%s pause row %s label is not geometrically centered" % [context, frame.name])
			return false
	for row_index in range(rects.size() - 1):
		var current_rect: Rect2 = rects[row_index]
		var next_rect: Rect2 = rects[row_index + 1]
		if current_rect.end.y >= next_rect.position.y:
			_fail("%s pause rows overlap or are out of visual order" % context)
			return false

	return true


func _assert_action_frame_accent(
		menu: Control,
	frame_name: String,
	expected_accent: Color,
	context: String,
	menu_spec: Dictionary,
	style_key: String) -> bool:
	var frame := menu.find_child(frame_name, true, false) as Control
	var accent: Color = frame.get("AccentColor") if frame != null else Color.TRANSPARENT
	var expected_style := int(menu_spec.get(style_key, menu_spec.get("action_frame_style", 12)))
	if frame == null or int(frame.get("FrameStyle")) != expected_style \
			or not accent.is_equal_approx(expected_accent):
		_fail("%s action frame %s uses style=%s accent=%s instead of style=%s accent=%s" % [
			context,
			frame_name,
			int(frame.get("FrameStyle")) if frame != null else -1,
			accent,
			expected_style,
			expected_accent,
		])
		return false
	if bool(menu_spec.get("disable_plaque_glow_backdrop", false)) \
			and bool(frame.get("UsePlaqueGlowBackdrop")):
		_fail("%s action frame %s still uses the rectangular glow backdrop" % [context, frame_name])
		return false
	if expected_style == 11 \
			and bool(menu_spec.get("disable_tool_row_idle_backdrop", false)) \
			and bool(frame.get("UseToolRowIdleBackdrop")):
		_fail("%s command frame %s still uses an idle rectangular backdrop" % [context, frame_name])
		return false
	return true


func _assert_glass_material(surface: ColorRect, context: String) -> bool:
	var material := surface.material as ShaderMaterial
	if material == null or material.shader == null or material.shader.resource_path != GLASS_SHADER_PATH:
		_fail("%s does not use the adventure glass shader" % context)
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
