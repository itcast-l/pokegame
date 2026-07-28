extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const MAP_TEXTURE_PATH := "res://art/maps/adventure_map_base_with_route_v1.png"
const GLASS_SHADER_PATH := "res://shaders/adventure_menu_glass.gdshader"
const MAIN_MENU_ADVENTURE_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/AdventureFrame/Button"
const VIEWPORT_SPECS := [
	{"name": "1280x720", "size": Vector2i(1280, 720)},
	{"name": "1920x1080", "size": Vector2i(1920, 1080)},
]
const RECT_TOLERANCE := 2.0

var _failed := false
var _viewport: SubViewport
var _menu: Control


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if not await _create_menu():
		return

	for spec in VIEWPORT_SPECS:
		if not await _audit_viewport(spec.size, spec.name):
			return
		if not await _capture_optional_screenshot("standalone", spec.size):
			return

	if not await _audit_main_menu_entry():
		return

	print("ADVENTURE_AVATAR_MENU_GLASS_LAYOUT_SMOKE_OK")
	if is_instance_valid(_menu):
		_menu.queue_free()
	_viewport.queue_free()
	await process_frame
	await _dispose_audio_before_quit()
	quit(0)


func _create_menu() -> bool:
	var packed_scene := load(MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load adventure avatar menu")
		return false

	_viewport = SubViewport.new()
	_viewport.size = VIEWPORT_SPECS[0].size
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(_viewport)

	_menu = packed_scene.instantiate() as Control
	if _menu == null:
		_fail("Failed to instantiate adventure avatar menu")
		return false
	_viewport.add_child(_menu)
	await process_frame
	await process_frame
	return true


func _audit_viewport(viewport_size: Vector2i, context: String) -> bool:
	_viewport.size = viewport_size
	await process_frame
	await process_frame

	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport_size))
	var background := _menu.get_node_or_null("StandaloneBackground") as Control
	var map_backdrop := _menu.get_node_or_null("StandaloneBackground/MapBackdrop") as TextureRect
	var readability_veil := _menu.get_node_or_null("StandaloneBackground/ReadabilityVeil") as ColorRect
	if background == null or map_backdrop == null or readability_veil == null:
		_fail("%s is missing the shared map backdrop layers" % context)
		return false
	if not background.visible or not _rects_close(background.get_global_rect(), viewport_rect):
		_fail("%s map backdrop does not cover the viewport: %s" % [context, background.get_global_rect()])
		return false
	if map_backdrop.texture == null or map_backdrop.texture.resource_path != MAP_TEXTURE_PATH:
		_fail("%s does not use the adventure map texture" % context)
		return false
	if not _rects_close(readability_veil.get_global_rect(), viewport_rect):
		_fail("%s readability veil does not cover the viewport" % context)
		return false

	var panel := _menu.get_node_or_null("SafeArea/MenuCenter/AdventureMenuPanel") as PanelContainer
	var glass_surface := _menu.get_node_or_null("%GlassSurface") as ColorRect
	var glass_frame := _menu.get_node_or_null("%GlassFrame") as Panel
	if panel == null or glass_surface == null or glass_frame == null:
		_fail("%s is missing the glass panel layers" % context)
		return false
	if not _rect_contains(viewport_rect, panel.get_global_rect()):
		_fail("%s glass panel escapes the viewport: %s" % [context, panel.get_global_rect()])
		return false
	if not _rects_close(glass_surface.get_global_rect(), panel.get_global_rect()):
		_fail("%s glass surface does not match the panel bounds" % context)
		return false
	if not _assert_glass_material(glass_surface, context):
		return false

	var body := _menu.get_node_or_null("SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body") as HBoxContainer
	var columns := [
		_menu.get_node_or_null("SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/AdventureListPanel") as PanelContainer,
		_menu.get_node_or_null("SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/LevelListPanel") as PanelContainer,
		_menu.get_node_or_null("SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/EnemyPreviewPanel") as PanelContainer,
	]
	var pane_glass_surfaces := [
		_menu.get_node_or_null("%AdventureListGlassSurface") as ColorRect,
		_menu.get_node_or_null("%LevelListGlassSurface") as ColorRect,
		_menu.get_node_or_null("%EnemyPreviewGlassSurface") as ColorRect,
	]
	if body == null or columns.any(func(column): return column == null) \
			or pane_glass_surfaces.any(func(surface): return surface == null):
		_fail("%s is missing one or more required content columns" % context)
		return false
	var shared_material := (pane_glass_surfaces[0] as ColorRect).material as ShaderMaterial
	var shared_style := (columns[0] as PanelContainer).get_theme_stylebox("panel")
	for index in range(columns.size()):
		var column := columns[index] as PanelContainer
		var pane_glass := pane_glass_surfaces[index] as ColorRect
		if column.size.x <= 0.0 or column.size.y <= 0.0 or not _rect_contains(body.get_global_rect(), column.get_global_rect()):
			_fail("%s column %d is outside the body: %s" % [context, index, column.get_global_rect()])
			return false
		if index > 0 and (columns[index - 1] as Control).get_global_rect().end.x > column.get_global_rect().position.x + RECT_TOLERANCE:
			_fail("%s columns overlap at index %d" % [context, index])
			return false
		var expected_glass_rect := column.get_global_rect().grow(-1.0)
		if not _rects_close(pane_glass.get_global_rect(), expected_glass_rect):
			_fail("%s content pane %d glass %s does not match its inner bounds %s" % [context, index, pane_glass.get_global_rect(), expected_glass_rect])
			return false
		if pane_glass.material != shared_material:
			_fail("%s content pane %d does not share the unified glass material" % [context, index])
			return false
		if column.get_theme_stylebox("panel") != shared_style:
			_fail("%s content pane %d does not share the unified frame style" % [context, index])
			return false
	if not _assert_content_pane_glass_material(shared_material, context):
		return false
	if not _assert_content_pane_style(shared_style, context):
		return false

	var start_frame := _menu.get_node_or_null("SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/EnemyPreviewPanel/EnemyPreviewMargin/EnemyPreviewLayout/StartWrap/StartFrame") as Control
	var start_button := _menu.get_node_or_null("%StartButton") as Button
	if start_frame == null or start_button == null or not start_frame.visible:
		_fail("%s is missing the primary adventure action" % context)
		return false
	if start_button.size.x < 44.0 or start_button.size.y < 44.0:
		_fail("%s primary action is below the minimum target size: %s" % [context, start_button.size])
		return false
	if int(start_frame.get("FrameStyle")) != 2:
		_fail("%s primary action does not use the adventure glass plaque" % context)
		return false

	return true


func _audit_main_menu_entry() -> bool:
	_menu.queue_free()
	await process_frame
	_viewport.size = VIEWPORT_SPECS[0].size

	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load main menu for embedded entry audit")
		return false
	var main_menu := packed_scene.instantiate() as Control
	if main_menu == null:
		_fail("Failed to instantiate main menu for embedded entry audit")
		return false
	_viewport.add_child(main_menu)
	await process_frame
	await process_frame

	var adventure_button := main_menu.get_node_or_null(MAIN_MENU_ADVENTURE_BUTTON_PATH) as Button
	if adventure_button == null:
		_fail("Main menu is missing the adventure entry button")
		return false
	adventure_button.emit_signal("pressed")

	var deadline := Time.get_ticks_msec() + 4000
	var transition_root := main_menu.find_child("AdventureTransitionRoot", true, false) as Control
	while (transition_root == null or not transition_root.visible or transition_root.modulate.a < 0.98) \
			and Time.get_ticks_msec() < deadline:
		await process_frame
		transition_root = main_menu.find_child("AdventureTransitionRoot", true, false) as Control
	if transition_root == null or not transition_root.visible or transition_root.modulate.a < 0.98:
		_fail("Main-menu adventure transition did not become visible")
		return false

	var embedded_menu := transition_root.find_child("AdventureAvatarMenuScene", true, false) as Control
	if embedded_menu == null:
		_fail("Main-menu transition is missing the embedded adventure menu")
		return false
	var embedded_background := embedded_menu.get_node_or_null("StandaloneBackground") as Control
	var embedded_glass := embedded_menu.get_node_or_null("%GlassSurface") as ColorRect
	if embedded_background == null or not embedded_background.visible:
		_fail("Embedded adventure menu hid the shared map backdrop")
		return false
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(VIEWPORT_SPECS[0].size))
	if not _rects_close(embedded_background.get_global_rect(), viewport_rect):
		_fail("Embedded adventure map does not cover the viewport: %s" % embedded_background.get_global_rect())
		return false
	if embedded_glass == null or not _assert_glass_material(embedded_glass, "main-menu entry"):
		return false

	if not await _capture_optional_screenshot("main-menu-entry", VIEWPORT_SPECS[0].size):
		return false
	main_menu.queue_free()
	await process_frame
	return true


func _assert_glass_material(glass_surface: ColorRect, context: String) -> bool:
	var material := glass_surface.material as ShaderMaterial
	if material == null or material.shader == null or material.shader.resource_path != GLASS_SHADER_PATH:
		_fail("%s glass surface does not use the dedicated glass shader" % context)
		return false
	return true


func _assert_content_pane_glass_material(material: ShaderMaterial, context: String) -> bool:
	if material == null or material.shader == null or material.shader.resource_path != GLASS_SHADER_PATH:
		_fail("%s content panes do not use the dedicated glass shader" % context)
		return false
	var surface_opacity := float(material.get_shader_parameter("surface_opacity"))
	var tint_color: Color = material.get_shader_parameter("tint_color")
	var blur_lod := float(material.get_shader_parameter("blur_lod"))
	if surface_opacity < 0.48 or surface_opacity > 0.58 \
			or tint_color.a < 0.24 or tint_color.a > 0.34 \
			or blur_lod < 1.8 or blur_lod > 2.4:
		_fail("%s content pane glass is outside the translucent design range: surface=%s tint=%s blur=%s" % [context, surface_opacity, tint_color.a, blur_lod])
		return false
	return true


func _assert_content_pane_style(style_box: StyleBox, context: String) -> bool:
	var flat_style := style_box as StyleBoxFlat
	if flat_style == null:
		_fail("%s content panes do not use the unified flat glass frame" % context)
		return false
	if flat_style.bg_color.a > 0.1:
		_fail("%s content pane frame fill is too opaque: %s" % [context, flat_style.bg_color.a])
		return false
	for side in [SIDE_LEFT, SIDE_TOP, SIDE_RIGHT, SIDE_BOTTOM]:
		if flat_style.get_border_width(side) != 1:
			_fail("%s content pane frame does not use a 1px border" % context)
			return false
	for corner in [CORNER_TOP_LEFT, CORNER_TOP_RIGHT, CORNER_BOTTOM_RIGHT, CORNER_BOTTOM_LEFT]:
		if flat_style.get_corner_radius(corner) != 8:
			_fail("%s content pane frame does not use an 8px radius" % context)
			return false
	var border_color := flat_style.border_color
	if border_color.a < 0.3 or border_color.g <= border_color.r or border_color.b <= border_color.r:
		_fail("%s content pane frame is missing the restrained cyan border" % context)
		return false
	return true


func _capture_optional_screenshot(state_name: String, viewport_size: Vector2i) -> bool:
	var output_dir := OS.get_environment("ADVENTURE_AVATAR_MENU_GLASS_SCREENSHOT_DIR").strip_edges()
	if output_dir.is_empty():
		return true
	for _frame in 3:
		await process_frame
	var image := _viewport.get_texture().get_image()
	if image == null or image.is_empty() or image.get_size() != viewport_size:
		_fail("%s screenshot is empty or wrong-sized at %s" % [state_name, viewport_size])
		return false
	var output_path := output_dir.path_join(
		"adventure-avatar-menu-%s-%dx%d.png" % [state_name, viewport_size.x, viewport_size.y])
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s screenshot: %s" % [state_name, error_string(save_error)])
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
	push_error("ADVENTURE_AVATAR_MENU_GLASS_LAYOUT_SMOKE_FAIL %s" % message)
	quit(1)
