extends SceneTree

const LOBBY_SCENE_PATH := "res://scenes/multiplayer_lobby_scene.tscn"
const ROOM_ROW_SCENE_PATH := "res://scenes/multiplayer_lobby_room_row.tscn"
const PLAYER_ROW_SCENE_PATH := "res://scenes/multiplayer_lobby_player_row.tscn"
const GLASS_SHADER_PATH := "res://shaders/adventure_menu_glass.gdshader"
const SCRIM_SHADER_PATH := "res://shaders/menu_blur_scrim.gdshader"
const VIEWPORT_SPECS := [
	{"name": "960x540", "size": Vector2i(960, 540), "columns": 2},
	{"name": "1280x720", "size": Vector2i(1280, 720), "columns": 4},
	{"name": "1920x1080", "size": Vector2i(1920, 1080), "columns": 4},
]
const PANEL_NAMES := ["ModeRail", "RoomBrowser", "LobbyPanel", "FriendsPanel"]
const SECONDARY_BUTTON_SURFACE := Color(0.025, 0.15, 0.18, 0.72)
const PRIMARY_BUTTON_SURFACE := Color(0.03, 0.25, 0.28, 0.88)
const RECT_TOLERANCE := 2.0

var _failed := false
var _viewport: SubViewport
var _lobby: Control


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if not await _create_lobby():
		return
	for spec in VIEWPORT_SPECS:
		if not await _audit_viewport(spec):
			return
	if not await _audit_dynamic_rows():
		return

	print("MULTIPLAYER_LOBBY_GLASS_LAYOUT_SMOKE_OK")
	_lobby.queue_free()
	_viewport.queue_free()
	await process_frame
	await _dispose_audio_before_quit()
	quit(0)


func _create_lobby() -> bool:
	var packed_scene := load(LOBBY_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load multiplayer lobby scene")
		return false
	_viewport = SubViewport.new()
	_viewport.size = VIEWPORT_SPECS[0].size
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(_viewport)
	_lobby = packed_scene.instantiate() as Control
	if _lobby == null:
		_fail("Failed to instantiate multiplayer lobby scene")
		return false
	_viewport.add_child(_lobby)
	await _wait_frames(4)
	return true


func _audit_viewport(spec: Dictionary) -> bool:
	var viewport_size: Vector2i = spec.size
	var context := str(spec.name)
	_viewport.size = viewport_size
	await _wait_frames(4)

	if not _lobby.has_meta("secondary_menu_visual_profile") \
			or str(_lobby.get_meta("secondary_menu_visual_profile")) != "secondary-menu-unified-multiplayer-readable":
		_fail("%s is missing the readable multiplayer visual profile" % context)
		return false
	var viewport_rect := Rect2(Vector2.ZERO, Vector2(viewport_size))
	var veil := _lobby.get_node_or_null("ReadabilityVeil") as ColorRect
	var veil_material := veil.material as ShaderMaterial if veil != null else null
	var veil_style_valid := false
	if DisplayServer.get_name().to_lower() == "headless":
		veil_style_valid = veil != null and veil_material == null and veil.color.a >= 0.50
	else:
		veil_style_valid = veil_material != null and veil_material.shader != null \
				and veil_material.shader.resource_path == SCRIM_SHADER_PATH \
				and (veil_material.get_shader_parameter("tint_color") as Color).a >= 0.50
	if veil == null or not _rects_close(veil.get_global_rect(), viewport_rect) or not veil_style_valid:
		_fail("%s readability veil is missing or does not cover the viewport" % context)
		return false

	var body_scroll := _lobby.get_node_or_null("SafeArea/MainLayout/BodyScroll") as ScrollContainer
	var body := _lobby.get_node_or_null("SafeArea/MainLayout/BodyScroll/Body") as GridContainer
	if body_scroll == null or body == null or body.columns != int(spec.columns):
		_fail("%s lobby body does not use the expected %d-column grid" % [context, spec.columns])
		return false
	if body_scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED \
			or body_scroll.scroll_horizontal != 0 \
			or body_scroll.get_h_scroll_bar().visible:
		_fail("%s lobby body still allows horizontal scrolling" % context)
		return false
	if body.size.x > body_scroll.size.x + RECT_TOLERANCE:
		_fail("%s lobby body is wider than its scroll viewport: %s > %s" % [context, body.size.x, body_scroll.size.x])
		return false
	if int(spec.columns) == 2 and not body_scroll.get_v_scroll_bar().visible:
		_fail("%s compact lobby does not expose its structured vertical scroll" % context)
		return false
	if int(spec.columns) == 4 and (body_scroll.vertical_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED \
			or body_scroll.scroll_vertical != 0 or body_scroll.get_v_scroll_bar().visible):
		_fail("%s desktop lobby still exposes outer vertical scrolling: mode=%d offset=%d bar=%s body=%s viewport=%s" % [
			context,
			body_scroll.vertical_scroll_mode,
			body_scroll.scroll_vertical,
			body_scroll.get_v_scroll_bar().visible,
			body.size,
			body_scroll.size,
		])
		return false
	if int(spec.columns) == 4 and body.size.y > body_scroll.size.y + RECT_TOLERANCE:
		_fail("%s desktop lobby is taller than its viewport: %s > %s" % [context, body.size.y, body_scroll.size.y])
		return false
	var steam_host := body.get_node_or_null("FriendsPanel/PanelMargin/FriendsContent/SteamHostButton") as Button
	var steam_refresh := body.get_node_or_null("FriendsPanel/PanelMargin/FriendsContent/SteamRefreshButton") as Button
	if steam_host == null or steam_refresh == null \
			or body.get_node_or_null("ModeRail/PanelMargin/ModeRailContent/SteamHostButton") != null \
			or body.get_node_or_null("ModeRail/PanelMargin/ModeRailContent/SteamRefreshButton") != null:
		_fail("%s Steam actions are not grouped inside the Steam panel" % context)
		return false

	var panels: Array[PanelContainer] = []
	for panel_name in PANEL_NAMES:
		var panel := body.get_node_or_null(panel_name) as PanelContainer
		var surface := panel.get_node_or_null("GlassSurface") as ColorRect if panel != null else null
		var frame := panel.get_node_or_null("GlassFrame") as Panel if panel != null else null
		var material := surface.material as ShaderMaterial if surface != null else null
		var surface_opacity := float(material.get_shader_parameter("surface_opacity")) if material != null else 0.0
		var surface_tint := material.get_shader_parameter("tint_color") as Color if material != null else Color.TRANSPARENT
		if panel == null or surface == null or frame == null \
				or material == null or material.shader == null \
				or material.shader.resource_path != GLASS_SHADER_PATH \
				or surface_opacity < 0.935 \
				or surface_tint.a < 0.775:
			_fail("%s panel %s does not use an opaque readable glass surface: opacity=%s tint=%s" % [
				context,
				panel_name,
				surface_opacity,
				surface_tint,
			])
			return false
		if not _rect_contains(body.get_global_rect(), panel.get_global_rect()):
			_fail("%s panel %s escapes the grid" % [context, panel_name])
			return false
		panels.append(panel)

	for left_index in range(panels.size()):
		for right_index in range(left_index + 1, panels.size()):
			if panels[left_index].get_global_rect().intersects(panels[right_index].get_global_rect(), true):
				_fail("%s panels %s and %s overlap" % [context, PANEL_NAMES[left_index], PANEL_NAMES[right_index]])
				return false
	if int(spec.columns) == 2:
		if absf(panels[0].global_position.y - panels[1].global_position.y) > RECT_TOLERANCE \
				or panels[2].global_position.y < panels[0].get_global_rect().end.y - RECT_TOLERANCE \
				or absf(panels[2].global_position.y - panels[3].global_position.y) > RECT_TOLERANCE:
			_fail("%s compact lobby is not arranged as a 2x2 workflow" % context)
			return false
	else:
		for panel in panels:
			if absf(panel.global_position.y - panels[0].global_position.y) > RECT_TOLERANCE:
				_fail("%s desktop lobby panels are not aligned in one row" % context)
				return false

	for control_path in [
		"SafeArea/MainLayout/Header/BackFrame/BackButton",
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/RoomNameInput",
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/RoomPasswordInput",
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/RoomRulesGrid/PlayerCountOption",
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/RoomRulesGrid/ItemModeOption",
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/RoomRulesGrid/WinningScoreOption",
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/LanHostButton",
		"SafeArea/MainLayout/BodyScroll/Body/FriendsPanel/PanelMargin/FriendsContent/SteamHostButton",
		"SafeArea/MainLayout/BodyScroll/Body/FriendsPanel/PanelMargin/FriendsContent/SteamRefreshButton",
		"SafeArea/MainLayout/BodyScroll/Body/LobbyPanel/PanelMargin/LobbyContent/ReadyButton",
		"SafeArea/MainLayout/BodyScroll/Body/LobbyPanel/PanelMargin/LobbyContent/StartButton",
		"SafeArea/MainLayout/BodyScroll/Body/LobbyPanel/PanelMargin/LobbyContent/LeaveSessionButton",
	]:
		var control := _lobby.get_node_or_null(control_path) as Control
		if control == null or control.size.y < 44.0:
			_fail("%s control %s is below the 44px interaction baseline" % [context, control_path])
			return false
		if control is BaseButton and (control as BaseButton).focus_mode == Control.FOCUS_NONE:
			_fail("%s control %s is not keyboard or gamepad focusable" % [context, control_path])
			return false
	if not _audit_room_setting_labels(context):
		return false
	for button_path in [
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/LanHostButton",
		"SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/LanSearchButton",
		"SafeArea/MainLayout/BodyScroll/Body/FriendsPanel/PanelMargin/FriendsContent/SteamHostButton",
		"SafeArea/MainLayout/BodyScroll/Body/FriendsPanel/PanelMargin/FriendsContent/SteamRefreshButton",
	]:
		if not _assert_button_surface(button_path, SECONDARY_BUTTON_SURFACE, context):
			return false
	if not _audit_header_navigation(context):
		return false
	for button_path in [
		"SafeArea/MainLayout/BodyScroll/Body/LobbyPanel/PanelMargin/LobbyContent/ReadyButton",
		"SafeArea/MainLayout/BodyScroll/Body/LobbyPanel/PanelMargin/LobbyContent/StartButton",
	]:
		if not _assert_button_surface(button_path, PRIMARY_BUTTON_SURFACE, context):
			return false
		var button := _lobby.get_node_or_null(button_path) as Button
		if button == null or button.get_theme_color("font_disabled_color").a < 0.90:
			_fail("%s disabled action %s is too faint to read: color=%s" % [
				context,
				button_path,
				button.get("theme_override_colors/font_disabled_color") if button != null else Color.TRANSPARENT,
			])
			return false
	if not _audit_readable_typography(context):
		return false
	return true


func _audit_room_setting_labels(context: String) -> bool:
	var field_specs := [
		{
			"label": "PlayerCountLabel",
			"option": "PlayerCountOption",
			"key": "PLAYER_COUNT_PLAYERS",
		},
		{
			"label": "ItemModeLabel",
			"option": "ItemModeOption",
			"key": "PLAYER_COUNT_ITEM_MODE",
		},
		{
			"label": "WinningScoreLabel",
			"option": "WinningScoreOption",
			"key": "PLAYER_COUNT_WINNING_SCORE",
		},
	]
	var grid_path := "SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/RoomRulesGrid"
	var grid := _lobby.get_node_or_null(grid_path) as GridContainer
	if grid == null or grid.columns != 2:
		_fail("%s room settings are not arranged as a two-column labeled grid" % context)
		return false
	for spec in field_specs:
		var label := grid.get_node_or_null(spec.label) as Label
		var option := grid.get_node_or_null(spec.option) as OptionButton
		var translated_label := TranslationServer.translate(spec.key)
		if label == null or option == null or label.text.is_empty() \
				or label.text == spec.key or label.text != translated_label:
			_fail("%s room setting %s is missing its localized field label" % [context, spec.option])
			return false
		var label_rect := label.get_global_rect()
		var option_rect := option.get_global_rect()
		var columns_overlap := option_rect.end.x > label_rect.position.x + RECT_TOLERANCE \
				if grid.is_layout_rtl() else label_rect.end.x > option_rect.position.x + RECT_TOLERANCE
		if columns_overlap or absf(label_rect.get_center().y - option_rect.get_center().y) > RECT_TOLERANCE \
				or not _rect_contains(grid.get_global_rect(), label_rect) \
				or not _rect_contains(grid.get_global_rect(), option_rect):
			_fail("%s room setting %s does not align its label with its option" % [context, spec.option])
			return false
	return true


func _assert_button_surface(button_path: String, expected: Color, context: String) -> bool:
	var button := _lobby.get_node_or_null(button_path) as Button
	var style := button.get_theme_stylebox("normal") as StyleBoxFlat if button != null else null
	if style == null or not style.bg_color.is_equal_approx(expected):
		_fail("%s button %s does not use the unified surface" % [context, button_path])
		return false
	return true


func _audit_header_navigation(context: String) -> bool:
	var header := _lobby.get_node_or_null("SafeArea/MainLayout/Header") as HBoxContainer
	var back_frame := _lobby.get_node_or_null("SafeArea/MainLayout/Header/BackFrame") as Control
	var back_button := _lobby.get_node_or_null("SafeArea/MainLayout/Header/BackFrame/BackButton") as Button
	var title := _lobby.get_node_or_null("SafeArea/MainLayout/Header/Title") as Label
	var header_status := _lobby.get_node_or_null("SafeArea/MainLayout/Header/HeaderStatus") as Control
	if header == null or back_frame == null or back_button == null or title == null or header_status == null \
			or back_frame.global_position.x > header.global_position.x + RECT_TOLERANCE \
			or not is_equal_approx(back_frame.size.x, header_status.size.x) \
			or int(back_frame.get("FrameStyle")) != 13 \
			or title.horizontal_alignment != HORIZONTAL_ALIGNMENT_CENTER \
			or not back_button.flat \
			or not back_button.get_theme_stylebox("normal") is StyleBoxEmpty \
			or not _rects_close(back_button.get_global_rect(), back_frame.get_global_rect()):
		_fail("%s header does not use the left-aligned framed back navigation pattern" % context)
		return false
	return true


func _audit_readable_typography(context: String) -> bool:
	var specs := [
		{"path": "SafeArea/MainLayout/Header/Title", "min_size": 28, "min_outline": 2},
		{"path": "SafeArea/MainLayout/Header/BackFrame/BackButton", "min_size": 18, "min_outline": 1},
		{
			"path": "SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/ModeTitle",
			"min_size": 20,
			"min_outline": 1,
		},
		{
			"path": "SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/RoomNameInput",
			"min_size": 16,
			"min_outline": 1,
		},
		{
			"path": "SafeArea/MainLayout/BodyScroll/Body/ModeRail/PanelMargin/ModeRailContent/LanHostInfoLabel",
			"min_size": 15,
			"min_outline": 1,
		},
		{
			"path": "SafeArea/MainLayout/BodyScroll/Body/FriendsPanel/PanelMargin/FriendsContent/SteamNoteLabel",
			"min_size": 15,
			"min_outline": 1,
		},
	]
	for spec in specs:
		var control := _lobby.get_node_or_null(spec.path) as Control
		var font := control.get_theme_font("font") as FontVariation if control != null else null
		if control == null or control.get_theme_font_size("font_size") < int(spec.min_size) \
				or control.get_theme_constant("outline_size") < int(spec.min_outline) \
				or font == null or font.variation_embolden <= 0.0:
			_fail("%s typography is too light or small: %s" % [context, spec.path])
			return false
	return true


func _audit_dynamic_rows() -> bool:
	var room_scene := load(ROOM_ROW_SCENE_PATH) as PackedScene
	var player_scene := load(PLAYER_ROW_SCENE_PATH) as PackedScene
	var room_row := room_scene.instantiate() as PanelContainer if room_scene != null else null
	var player_row := player_scene.instantiate() as PanelContainer if player_scene != null else null
	if room_row == null or player_row == null:
		_fail("Failed to instantiate multiplayer dynamic rows")
		return false
	_viewport.add_child(room_row)
	_viewport.add_child(player_row)
	await process_frame

	room_row.call("Configure", {
		"room_name": "Layout Audit Room",
		"host_name": "Host",
		"address": "127.0.0.1:4011",
		"transport": "Lan",
		"status": "Lobby",
		"player_count": 2,
		"winning_score": 100,
		"item_mode": true,
		"has_password": true,
	})
	var room_status := room_row.get_node("RowLayout/StatusLabel") as Label
	var join_button := room_row.get_node("RowLayout/JoinButton") as Button
	var room_style := room_row.get_theme_stylebox("panel") as StyleBoxFlat
	if not room_status.visible or room_status.text.is_empty() or join_button.disabled \
			or not join_button.tooltip_text.is_empty() \
			or room_row.tooltip_text.is_empty() \
			or room_style == null or room_style.bg_color.a < 0.935:
		_fail("Room row does not combine visible status text, border state, and join affordance")
		return false
	for control_path in ["RowLayout/TextStack/RoomNameLabel", "RowLayout/TextStack/DetailsLabel", "RowLayout/StatusLabel", "RowLayout/JoinButton"]:
		if not _assert_row_typography(room_row, control_path, 14):
			return false

	player_row.call("Configure", {
		"seat_index": 0,
		"display_name": "Layout Audit Player",
		"is_host": true,
		"is_local": true,
		"is_ready": true,
		"is_connected": true,
	})
	var role_label := player_row.get_node("RowLayout/RoleLabel") as Label
	var ready_label := player_row.get_node("RowLayout/ReadyLabel") as Label
	var ready_style := player_row.get_theme_stylebox("panel") as StyleBoxFlat
	if role_label.text.is_empty() or ready_label.text.is_empty() or ready_style == null \
			or ready_style.bg_color.a < 0.915:
		_fail("Player row does not expose host and ready status with a border state")
		return false
	for control_path in ["RowLayout/SeatLabel", "RowLayout/PlayerNameLabel", "RowLayout/RoleLabel", "RowLayout/ReadyLabel"]:
		if not _assert_row_typography(player_row, control_path, 15):
			return false
	var ready_border := ready_style.border_color
	player_row.call("Configure", {
		"seat_index": 0,
		"display_name": "Layout Audit Player",
		"is_host": true,
		"is_local": true,
		"is_ready": false,
		"is_connected": false,
	})
	var offline_style := player_row.get_theme_stylebox("panel") as StyleBoxFlat
	if ready_label.text.is_empty() or offline_style == null \
			or offline_style.border_color.is_equal_approx(ready_border):
		_fail("Player offline state is not expressed by both text and a changed border")
		return false

	room_row.queue_free()
	player_row.queue_free()
	await process_frame
	return true


func _assert_row_typography(row: Control, control_path: String, minimum_size: int) -> bool:
	var control := row.get_node_or_null(control_path) as Control
	var font := control.get_theme_font("font") as FontVariation if control != null else null
	if control == null or control.get_theme_font_size("font_size") < minimum_size \
			or control.get_theme_constant("outline_size") < 1 \
			or font == null or font.variation_embolden <= 0.0:
		_fail("Dynamic row typography is too light or small: %s" % control_path)
		return false
	return true


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
	push_error("MULTIPLAYER_LOBBY_GLASS_LAYOUT_SMOKE_FAIL %s" % message)
	quit(1)
