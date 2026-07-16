extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const RECT_TOLERANCE := 1.5

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(960, 900)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return
	var board := packed_scene.instantiate() as Control
	board.set("player_count", 4)
	board.set("item_mode", false)
	board.set("solo_mode", true)
	board.set("adventure_mode", false)
	viewport.add_child(board)
	await process_frame
	await process_frame

	if not _audit_state(
		board,
		Rect2(0, 130, 960, 640),
		Rect2(0, 0, 960, 900),
		false,
		"narrow"):
		return

	viewport.size = Vector2i(2560, 1080)
	await process_frame
	await process_frame
	await process_frame
	if not _audit_state(
		board,
		Rect2(320, 0, 1920, 1080),
		Rect2(0, 0, 2560, 1080),
		true,
		"ultrawide"):
		return

	var board_canvas := board.get_node("BoardCanvas")
	for overlay_name in ["WinnerBannerRoot", "DefeatFinisherRoot"]:
		var overlay := board.get_node_or_null(overlay_name)
		if overlay == null or overlay.get_parent() != board:
			_fail("%s should remain a root-level overlay" % overlay_name)
			return
		if board_canvas.is_ancestor_of(overlay):
			_fail("%s must not be clipped by BoardCanvas" % overlay_name)
			return

	print("GAME_BOARD_BACKDROP_VISIBILITY_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _audit_state(
		board: Control,
		expected_canvas: Rect2,
		expected_fill: Rect2,
		expected_standard: bool,
		label: String) -> bool:
	var backdrop_fill := board.get_node("BackdropFill") as TextureRect
	var board_canvas := board.get_node("BoardCanvas") as Control
	var vertical_backdrop := board.get_node("BoardCanvas/VerticalBackdrop") as TextureRect
	var board_art := board.get_node("BoardCanvas/BoardArt") as TextureRect
	if not _rects_close(backdrop_fill.get_global_rect(), expected_fill):
		_fail("%s BackdropFill mismatch expected=%s actual=%s" % [label, expected_fill, backdrop_fill.get_global_rect()])
		return false
	if not _rects_close(board_canvas.get_global_rect(), expected_canvas):
		_fail("%s BoardCanvas mismatch expected=%s actual=%s" % [label, expected_canvas, board_canvas.get_global_rect()])
		return false
	if not _rects_close(board_art.get_global_rect(), expected_canvas) \
			or not _rects_close(vertical_backdrop.get_global_rect(), expected_canvas):
		_fail("%s board backgrounds should fill BoardCanvas" % label)
		return false
	if board_art.visible != expected_standard or vertical_backdrop.visible == expected_standard:
		_fail("%s background visibility does not match expected variant" % label)
		return false
	return true


func _rects_close(first: Rect2, second: Rect2) -> bool:
	return first.position.distance_to(second.position) <= RECT_TOLERANCE \
		and first.size.distance_to(second.size) <= RECT_TOLERANCE


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
