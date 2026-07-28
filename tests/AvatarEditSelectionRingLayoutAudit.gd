extends SceneTree

const SCENE_PATH := "res://scenes/character_avatar_edit_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1280, 720)
const SETTLE_FRAMES := 6
const TOLERANCE := 0.5
const SELECTED_MODULATE := Color(1.0, 1.0, 0.78, 1.0)


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

	var scroll := node.get_node_or_null("%GridScroll") as ScrollContainer
	if scroll == null:
		_fail("Missing avatar grid scroll container")
		return

	var avatar_grid := node.get_node_or_null("%AvatarGrid") as GridContainer
	if avatar_grid == null:
		_fail("Missing avatar grid")
		return

	var visible_option_count := 0
	var scroll_rect := scroll.get_global_rect()
	for child in avatar_grid.get_children():
		if child is not Control:
			continue

		var option_root := child as Control
		if not option_root.visible:
			continue

		var button := option_root.get_node_or_null("SelectButton") as Button
		var panel := option_root.get_node_or_null("OptionPanel") as PanelContainer
		if button == null or panel == null:
			_fail("Avatar option is missing SelectButton or OptionPanel: %s" % option_root.get_path())
			return

		visible_option_count += 1
		button.emit_signal("pressed")
		await process_frame

		if not _color_is_close(panel.modulate, SELECTED_MODULATE):
			_fail("Selected avatar option did not apply expected highlight: %s modulate=%s" % [
				panel.get_path(),
				str(panel.modulate),
			])
			return

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

	print("AVATAR_EDIT_SELECTION_RING_LAYOUT_AUDIT_OK")
	await _dispose_audio_before_quit()
	quit(0)


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
