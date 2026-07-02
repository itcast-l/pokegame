extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1080, 720)
const RECT_TOLERANCE := 0.6
const EXPECTED_RECTS := {
	"LayoutAreas/TopPlayerArea/Player2AvatarArea": Rect2(315.375, 49.875, 78.0, 78.0),
	"LayoutAreas/TopPlayerArea/Player2ScoreMeter": Rect2(308.375, 42.875, 92.0, 92.0),
	"LayoutAreas/BottomPlayerArea/Player1AvatarArea": Rect2(238.0, 511.0, 78.0, 78.0),
	"LayoutAreas/BottomPlayerArea/Player1ScoreMeter": Rect2(232.02399, 504.47998, 92.0, 92.0),
	"LayoutAreas/LeftPlayerArea/Player3AvatarArea": Rect2(119.625, 126.375, 78.0, 78.0),
	"LayoutAreas/LeftPlayerArea/Player3ScoreMeter": Rect2(112.625, 119.375, 92.0, 92.0),
	"LayoutAreas/RightPlayerArea/Player4AvatarArea": Rect2(880.125, 126.375, 78.0, 78.0),
	"LayoutAreas/RightPlayerArea/Player4ScoreMeter": Rect2(873.125, 119.375, 92.0, 92.0),
	"LayoutAreas/Player1HandArea": Rect2(337.0, 503.0, 360.0, 130.0),
	"LayoutAreas/Player2HandArea": Rect2(391.0, 83.0, 270.0, 78.0),
	"LayoutAreas/Player3HandArea": Rect2(95.0, 225.0, 92.0, 266.0),
	"LayoutAreas/Player4HandArea": Rect2(891.0, 228.0, 92.0, 266.0),
	"LayoutAreas/Player1CardPileArea": Rect2(706.0, 493.0, 124.0, 128.0),
	"LayoutAreas/Player2CardPileArea": Rect2(662.0, 46.0, 124.0, 128.0),
	"LayoutAreas/Player3CardPileArea": Rect2(83.0, 435.0, 124.0, 128.0),
	"LayoutAreas/Player4CardPileArea": Rect2(878.0, 433.0, 124.0, 128.0),
}

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var viewport := SubViewport.new()
	viewport.size = VIEWPORT_SIZE
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

	board.set("player_count", 4)
	board.set("item_mode", false)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", false)
	viewport.add_child(board)
	await process_frame
	await process_frame

	for node_path in EXPECTED_RECTS.keys():
		var control := board.get_node_or_null(node_path) as Control
		if control == null:
			_fail("Missing control: %s" % node_path)
			return

		var actual := control.get_global_rect()
		var expected: Rect2 = EXPECTED_RECTS[node_path]
		if not _rects_close(actual, expected, RECT_TOLERANCE):
			_fail("%s expected=%s actual=%s" % [node_path, str(expected), str(actual)])
			return

	print("GAME_BOARD_FOUR_PLAYER_LAYOUT_SNAPSHOT_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


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
	print("GAME_BOARD_FOUR_PLAYER_LAYOUT_SNAPSHOT_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
