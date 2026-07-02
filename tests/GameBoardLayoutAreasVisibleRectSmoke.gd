extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const EDITOR_PREVIEW_SCRIPT_PATH := "res://scripts/GameBoardLayoutAreasEditorPreview.cs"
const BASELINE_VIEWPORT_SIZE := Vector2i(1280, 720)
const TALL_VIEWPORT_SIZE := Vector2i(960, 900)
const RECT_TOLERANCE := 1.5

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = BASELINE_VIEWPORT_SIZE
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
	root.add_child(board)
	await process_frame
	await process_frame

	var layout_areas := board.get_node_or_null("LayoutAreas") as Control
	var board_art := board.get_node_or_null("BoardArt") as TextureRect
	if layout_areas == null or board_art == null:
		_fail("Scene missing LayoutAreas or BoardArt")
		return

	var layout_script := layout_areas.get_script() as Script
	if layout_script == null or layout_script.resource_path != EDITOR_PREVIEW_SCRIPT_PATH:
		_fail("LayoutAreas missing expected editor preview script: %s" % EDITOR_PREVIEW_SCRIPT_PATH)
		return

	_assert_layout_matches_visible_rect("baseline", layout_areas, board_art)
	if _failed:
		return

	root.size = TALL_VIEWPORT_SIZE
	await process_frame
	await process_frame

	_assert_layout_matches_visible_rect("tall", layout_areas, board_art)
	if _failed:
		return

	root.size = BASELINE_VIEWPORT_SIZE
	await process_frame
	await process_frame

	_assert_layout_matches_visible_rect("return_to_baseline", layout_areas, board_art)
	if _failed:
		return

	print("GAME_BOARD_LAYOUT_AREAS_VISIBLE_RECT_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _assert_layout_matches_visible_rect(mode_name: String, layout_areas: Control, board_art: TextureRect) -> void:
	var visible_rect := _get_visible_rect(board_art)
	var layout_rect := layout_areas.get_global_rect()
	if not _rects_close(visible_rect, layout_rect, RECT_TOLERANCE):
		_fail("%s layout rect mismatch visible=%s layout=%s" % [
			mode_name,
			str(visible_rect),
			str(layout_rect),
		])


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


func _rects_close(first: Rect2, second: Rect2, tolerance: float) -> bool:
	return absf(first.position.x - second.position.x) <= tolerance \
		and absf(first.position.y - second.position.y) <= tolerance \
		and absf(first.size.x - second.size.x) <= tolerance \
		and absf(first.size.y - second.size.y) <= tolerance


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("GAME_BOARD_LAYOUT_AREAS_VISIBLE_RECT_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
