extends SceneTree

const SCENE_PATH := "res://scenes/character_avatar_edit_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1280, 720)
const SETTLE_FRAMES := 6
const TOLERANCE := 0.5


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = VIEWPORT_SIZE
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var node := packed_scene.instantiate()
	if node == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	root.add_child(node)
	for _frame in range(SETTLE_FRAMES):
		await process_frame

	if not _audit_readable_typography(node):
		return

	var scroll := node.get_node_or_null("%GridScroll") as ScrollContainer
	if scroll == null:
		_fail("Missing avatar grid scroll container")
		return

	var avatar_grid := node.get_node_or_null("%AvatarGrid") as GridContainer
	if avatar_grid == null:
		_fail("Missing avatar grid")
		return

	var visible_option_count := 0
	var selected_option_count := 0
	var scroll_rect := scroll.get_global_rect()
	for child in avatar_grid.get_children():
		if child is not Control:
			continue

		var option_root := child as Control
		if not option_root.visible:
			continue

		var button := option_root.get_node_or_null("SelectButton") as Button
		var panel := option_root.get_node_or_null("OptionPanel") as PanelContainer
		var backdrop := option_root.get_node_or_null("OptionPanel/OptionMargin/OptionLayout/AvatarSlot/SlotBackdrop") as PanelContainer
		if button == null or panel == null or backdrop == null:
			_fail("Avatar option is missing its single-slot selection surface: %s" % option_root.get_path())
			return
		if not bool(option_root.get("SuppressVisuals")) \
				or not panel.get_theme_stylebox("panel") is StyleBoxEmpty:
			_fail("Avatar option still renders stacked outer frame visuals: %s" % option_root.get_path())
			return

		visible_option_count += 1
		button.emit_signal("pressed")
		await process_frame

		var slot_style := backdrop.get_theme_stylebox("panel") as StyleBoxFlat
		if not bool(option_root.get("Selected")) or slot_style == null \
				or slot_style.border_width_left < 2 \
				or not _color_is_close(panel.modulate, Color.WHITE):
			_fail("Selected avatar option did not apply the single-slot highlight: %s" % option_root.get_path())
			return
		selected_option_count += 1

		var visual_rect := panel.get_global_rect()
		if not _rect_is_inside(visual_rect, scroll_rect):
			_fail("Avatar option exceeds scroll viewport: panel=%s visual_rect=%s scroll_rect=%s" % [
				panel.get_path(),
				str(visual_rect),
				str(scroll_rect),
			])
			return

	if visible_option_count == 0:
		_fail("No visible avatar options found")
		return
	if selected_option_count != visible_option_count:
		_fail("Every avatar selection did not receive a stable single-slot highlight")
		return

	var current_selection_count := 0
	for child in avatar_grid.get_children():
		var option_root := child as Control
		if option_root == null or not option_root.visible:
			continue
		var backdrop := option_root.get_node_or_null(
			"OptionPanel/OptionMargin/OptionLayout/AvatarSlot/SlotBackdrop") as PanelContainer
		var slot_style := backdrop.get_theme_stylebox("panel") as StyleBoxFlat if backdrop != null else null
		if slot_style == null:
			_fail("Avatar option is missing its final slot style: %s" % option_root.get_path())
			return
		if bool(option_root.get("Selected")):
			current_selection_count += 1
			continue
		if slot_style.border_width_left != 1 \
				or slot_style.border_color.a < 0.20 \
				or slot_style.bg_color.a < 0.55:
			_fail("Unselected avatar option lacks a visible single-layer separator: %s" % option_root.get_path())
			return
	if current_selection_count != 1:
		_fail("Avatar grid must expose exactly one selected option")
		return

	print("AVATAR_EDIT_SELECTION_RING_LAYOUT_AUDIT_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _audit_readable_typography(node: Node) -> bool:
	var specs := [
		{"path": "%Title", "min_size": 30, "min_outline": 2},
		{"path": "%BackButton", "min_size": 18, "min_outline": 1},
		{"path": "%ConfirmButton", "min_size": 18, "min_outline": 1},
		{
			"path": "DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewKicker",
			"min_size": 16,
			"min_outline": 1,
		},
		{
			"path": "DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameHint",
			"min_size": 15,
			"min_outline": 1,
		},
	]

	for spec in specs:
		var control := node.get_node_or_null(spec.path) as Control
		if control == null:
			_fail("Missing avatar typography control: %s" % spec.path)
			return false
		if control.get_theme_font_size("font_size") < int(spec.min_size) \
				or control.get_theme_constant("outline_size") < int(spec.min_outline):
			_fail("Avatar typography is too light or small: %s" % control.get_path())
			return false
		var font := control.get_theme_font("font") as FontVariation
		if font == null or font.variation_embolden <= 0.0:
			_fail("Avatar typography is missing its readable font variation: %s" % control.get_path())
			return false

	var hint := node.get_node_or_null(
		"DialogCenter/DialogPanel/DialogMargin/DialogLayout/Body/PreviewPanel/PreviewMargin/PreviewLayout/PreviewControls/PlayerNameHint") as Label
	if hint == null or hint.get_theme_color("font_color").a < 0.95:
		_fail("Player name hint does not have sufficient text opacity")
		return false

	return true


func _rect_is_inside(inner: Rect2, outer: Rect2) -> bool:
	if inner.position.x < outer.position.x - TOLERANCE:
		return false
	if inner.position.y < outer.position.y - TOLERANCE:
		return false
	if inner.end.x > outer.end.x + TOLERANCE:
		return false
	if inner.end.y > outer.end.y + TOLERANCE:
		return false
	return true


func _color_is_close(a: Color, b: Color, epsilon: float = 0.02) -> bool:
	return abs(a.r - b.r) <= epsilon \
		and abs(a.g - b.g) <= epsilon \
		and abs(a.b - b.b) <= epsilon \
		and abs(a.a - b.a) <= epsilon


func _fail(message: String) -> void:
	push_error(message)
	print("AVATAR_EDIT_SELECTION_RING_LAYOUT_AUDIT_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
