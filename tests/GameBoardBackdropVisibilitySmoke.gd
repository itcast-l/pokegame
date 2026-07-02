extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TALL_VIEWPORT_SIZE := Vector2i(960, 900)

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var viewport := SubViewport.new()
	viewport.size = TALL_VIEWPORT_SIZE
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	board.set("player_count", 3)
	board.set("item_mode", false)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", false)
	viewport.add_child(board)
	await process_frame
	await process_frame
	viewport.size = TALL_VIEWPORT_SIZE + Vector2i(120, 0)
	await process_frame
	viewport.size = TALL_VIEWPORT_SIZE
	await process_frame
	await process_frame

	var backdrop := board.get_node_or_null("UltraWideBackdrop") as TextureRect
	var vertical_backdrop := board.get_node_or_null("VerticalBackdrop") as TextureRect
	var board_art := board.get_node_or_null("BoardArt") as TextureRect
	if backdrop == null or vertical_backdrop == null or board_art == null:
		_fail("Scene missing UltraWideBackdrop, VerticalBackdrop, or BoardArt")
		return

	var control_rect := board_art.get_global_rect()
	var visible_rect := _get_visible_rect(board_art)
	if visible_rect.size.y >= control_rect.size.y - 1.5:
		_fail("Expected vertical letterboxing in tall viewport control=%s visible=%s" % [
			str(control_rect),
			str(visible_rect),
		])
		return

	if backdrop.visible:
		_fail("UltraWideBackdrop should be hidden when BoardArt leaves top/bottom gutters control=%s visible=%s" % [
			str(control_rect),
			str(visible_rect),
		])
		return

	if not vertical_backdrop.visible:
		_fail("VerticalBackdrop should be visible when BoardArt leaves top/bottom gutters control=%s visible=%s" % [
			str(control_rect),
			str(visible_rect),
		])
		return

	print("GAME_BOARD_BACKDROP_VISIBILITY_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _get_visible_rect(board_art: TextureRect) -> Rect2:
	var control_rect := board_art.get_global_rect()
	var texture := board_art.texture
	if texture == null:
		return control_rect

	var texture_size := texture.get_size()
	if texture_size.x <= 0.0 or texture_size.y <= 0.0:
		return control_rect

	var control_size := control_rect.size
	if control_size.x <= 0.0 or control_size.y <= 0.0:
		return control_rect

	var texture_ratio := texture_size.x / texture_size.y
	var control_ratio := control_size.x / control_size.y
	var visible_size := control_size
	if control_ratio > texture_ratio:
		visible_size = Vector2(control_size.y * texture_ratio, control_size.y)
	else:
		visible_size = Vector2(control_size.x, control_size.x / texture_ratio)

	return Rect2(control_rect.position + (control_size - visible_size) * 0.5, visible_size)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("GAME_BOARD_BACKDROP_VISIBILITY_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
