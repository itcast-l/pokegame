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

	var scroll := node.get_node_or_null("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll") as ScrollContainer
	if scroll == null:
		_fail("Missing avatar grid scroll container")
		return

	var avatar_grid := node.get_node_or_null("DialogCenter/DialogPanel/DialogMargin/DialogLayout/GridScroll/GridRow/AvatarGrid") as GridContainer
	if avatar_grid == null:
		_fail("Missing avatar grid")
		return

	var visible_ring_count := 0
	var scroll_rect := scroll.get_global_rect()
	for child in avatar_grid.get_children():
		if child is not Control:
			continue

		var option_root := child as Control
		var ring := option_root.get_node_or_null("SelectionRing") as Control
		if ring == null or not ring.is_visible_in_tree():
			continue

		visible_ring_count += 1
		var shadow_size := _get_shadow_size(ring)
		var visual_rect := ring.get_global_rect().grow(shadow_size)
		if not _rect_is_inside(visual_rect, scroll_rect):
			_fail("Selection ring exceeds scroll viewport: ring=%s visual_rect=%s scroll_rect=%s shadow=%.1f" % [
				ring.get_path(),
				str(visual_rect),
				str(scroll_rect),
				shadow_size,
			])
			return

	if visible_ring_count == 0:
		_fail("No visible selection ring found")
		return

	print("AVATAR_EDIT_SELECTION_RING_LAYOUT_AUDIT_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _get_shadow_size(ring: Control) -> float:
	var stylebox := ring.get_theme_stylebox("panel")
	if stylebox is StyleBoxFlat:
		return float((stylebox as StyleBoxFlat).shadow_size)
	return 0.0


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
