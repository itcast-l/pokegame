extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const DESIGN_SIZE := Vector2(1280.0, 720.0)
const RECT_TOLERANCE := 2.0
const VIEWPORT_SPECS := [
	{"name": "1280x720", "size": Vector2i(1280, 720), "variant": "Standard"},
	{"name": "1920x1080", "size": Vector2i(1920, 1080), "variant": "Standard"},
	{"name": "1440x900", "size": Vector2i(1440, 900), "variant": "Standard"},
	{"name": "1600x1000", "size": Vector2i(1600, 1000), "variant": "Standard"},
	{"name": "3200x1859", "size": Vector2i(3200, 1859), "variant": "Standard"},
	{"name": "1080x720", "size": Vector2i(1080, 720), "variant": "VerticalComposite"},
	{"name": "960x720", "size": Vector2i(960, 720), "variant": "VerticalComposite"},
	{"name": "2560x1080", "size": Vector2i(2560, 1080), "variant": "Standard"},
]
const STANDARD_RECTS := {
	"BoardCanvas/LayoutAreas/BottomPlayerArea/Player1AvatarArea": Rect2(280, 541, 94, 93),
	"BoardCanvas/LayoutAreas/BottomPlayerArea/Player1ScoreMeter": Rect2(277.51199, 537.73999, 98.976013, 99.52002),
	"BoardCanvas/LayoutAreas/TopPlayerArea/Player2AvatarArea": Rect2(379, 31, 78, 78),
	"BoardCanvas/LayoutAreas/TopPlayerArea/Player2ScoreMeter": Rect2(372, 24, 92, 92),
	"BoardCanvas/LayoutAreas/LeftPlayerArea/Player3AvatarArea": Rect2(128, 145, 78, 78),
	"BoardCanvas/LayoutAreas/LeftPlayerArea/Player3ScoreMeter": Rect2(121, 138, 92, 92),
	"BoardCanvas/LayoutAreas/RightPlayerArea/Player4AvatarArea": Rect2(1072, 146, 78, 78),
	"BoardCanvas/LayoutAreas/RightPlayerArea/Player4ScoreMeter": Rect2(1065, 139, 92, 92),
	"BoardCanvas/LayoutAreas/Player1HandArea": Rect2(455, 542, 360, 130),
	"BoardCanvas/LayoutAreas/Player2HandArea": Rect2(506, 34, 270, 78),
	"BoardCanvas/LayoutAreas/Player3HandArea": Rect2(115, 222, 92, 266),
	"BoardCanvas/LayoutAreas/Player4HandArea": Rect2(1069, 227, 92, 266),
	"BoardCanvas/LayoutAreas/Player1CardPileArea": Rect2(849, 527, 124, 128),
	"BoardCanvas/LayoutAreas/Player2CardPileArea": Rect2(806, 16, 124, 128),
	"BoardCanvas/LayoutAreas/Player3CardPileArea": Rect2(104, 438, 124, 128),
	"BoardCanvas/LayoutAreas/Player4CardPileArea": Rect2(1055, 433, 124, 128),
	"BoardCanvas/LayoutAreas/TableCardLayoutArea": Rect2(292.5, 182, 683.648, 264.264),
	"BoardCanvas/LayoutAreas/QuestionCardTransformArea": Rect2(574, 238, 138, 168),
	"BoardCanvas/LayoutAreas/CenterArena": Rect2(262.375, 159, 762.66626, 408.23999),
}
const VERTICAL_RECTS := {
	"BoardCanvas/LayoutAreas/BottomPlayerArea/Player1AvatarArea": Rect2(285.33334, 524, 96, 96),
	"BoardCanvas/LayoutAreas/BottomPlayerArea/Player1ScoreMeter": Rect2(287.33334, 526, 92, 92),
	"BoardCanvas/LayoutAreas/TopPlayerArea/Player2AvatarArea": Rect2(381.66666, 70.333336, 78, 78),
	"BoardCanvas/LayoutAreas/TopPlayerArea/Player2ScoreMeter": Rect2(374.66666, 63.333336, 92, 92),
	"BoardCanvas/LayoutAreas/LeftPlayerArea/Player3AvatarArea": Rect2(149, 158.33333, 78, 78),
	"BoardCanvas/LayoutAreas/LeftPlayerArea/Player3ScoreMeter": Rect2(142, 151.33333, 92, 92),
	"BoardCanvas/LayoutAreas/RightPlayerArea/Player4AvatarArea": Rect2(1051.6666, 158.33333, 78, 78),
	"BoardCanvas/LayoutAreas/RightPlayerArea/Player4ScoreMeter": Rect2(1044.6666, 151.33333, 92, 92),
	"BoardCanvas/LayoutAreas/Player1HandArea": Rect2(437, 503, 360, 130),
	"BoardCanvas/LayoutAreas/Player2HandArea": Rect2(491, 83, 270, 78),
	"BoardCanvas/LayoutAreas/Player3HandArea": Rect2(195, 225, 92, 266),
	"BoardCanvas/LayoutAreas/Player4HandArea": Rect2(991, 228, 92, 266),
	"BoardCanvas/LayoutAreas/Player1CardPileArea": Rect2(806, 493, 124, 128),
	"BoardCanvas/LayoutAreas/Player2CardPileArea": Rect2(762, 46, 124, 128),
	"BoardCanvas/LayoutAreas/Player3CardPileArea": Rect2(183, 435, 124, 128),
	"BoardCanvas/LayoutAreas/Player4CardPileArea": Rect2(978, 433, 124, 128),
	"BoardCanvas/LayoutAreas/TableCardLayoutArea": Rect2(330, 182, 602.948, 264.264),
	"BoardCanvas/LayoutAreas/QuestionCardTransformArea": Rect2(562, 238, 138, 168),
	"BoardCanvas/LayoutAreas/CenterArena": Rect2(318, 159, 643.49963, 408.23999),
}
const STANDARD_MARKERS := {
	"%Player1ScoreEffectTarget": Vector2(327, 587.5),
	"%Player2ScoreEffectTarget": Vector2(418, 70),
	"%Player3ScoreEffectTarget": Vector2(167, 184),
	"%Player4ScoreEffectTarget": Vector2(1111, 185),
}
const VERTICAL_MARKERS := {
	"%Player1ScoreEffectTarget": Vector2(333.33334, 572),
	"%Player2ScoreEffectTarget": Vector2(420.66666, 109.333336),
	"%Player3ScoreEffectTarget": Vector2(188, 197.33333),
	"%Player4ScoreEffectTarget": Vector2(1090.6666, 197.33333),
}

var _failed := false
var _viewport: SubViewport
var _board: Control


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if not await _create_board():
		return

	for spec in VIEWPORT_SPECS:
		if not await _audit_size(spec.size, spec.variant, spec.name):
			return

	if not await _audit_size(Vector2i(1280, 720), "Standard", "roundtrip-baseline"):
		return
	var baseline_rects := _capture_tracked_rects()
	if not await _audit_size(Vector2i(3200, 1859), "Standard", "roundtrip-maximized"):
		return
	if not await _audit_size(Vector2i(1280, 720), "Standard", "roundtrip-restored"):
		return
	var restored_rects := _capture_tracked_rects()
	for path in baseline_rects:
		if not _rects_close(baseline_rects[path], restored_rects[path], 0.1):
			_fail("Roundtrip drifted at %s baseline=%s restored=%s" % [path, baseline_rects[path], restored_rects[path]])
			return

	var hysteresis := [
		{"aspect": 1.61, "variant": "Standard"},
		{"aspect": 1.595, "variant": "Standard"},
		{"aspect": 1.585, "variant": "Standard"},
		{"aspect": 1.58, "variant": "VerticalComposite"},
		{"aspect": 1.59, "variant": "VerticalComposite"},
		{"aspect": 1.60, "variant": "Standard"},
	]
	for item in hysteresis:
		var size := Vector2i(roundi(float(item.aspect) * 1000.0), 1000)
		if not await _audit_size(size, item.variant, "hysteresis-%.3f" % item.aspect):
			return

	print("GAME_BOARD_RESPONSIVE_LAYOUT_AUDIT_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _create_board() -> bool:
	_viewport = SubViewport.new()
	_viewport.size = Vector2i(1280, 720)
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(_viewport)
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return false
	_board = GameBoardSmokeFactory.create_solo(packed_scene, 4, false) as Control
	if _board == null:
		_fail("Failed to instantiate board scene")
		return false
	_viewport.add_child(_board)
	await process_frame
	await process_frame
	return true


func _audit_size(size: Vector2i, expected_variant: String, label: String) -> bool:
	_viewport.size = size
	await process_frame
	await process_frame
	await process_frame
	var expected_canvas := _expected_canvas_rect(Vector2(size))
	var board_canvas := _board.get_node("BoardCanvas") as Control
	var backdrop_fill := _board.get_node("BackdropFill") as TextureRect
	if not _rects_close(board_canvas.get_global_rect(), expected_canvas, RECT_TOLERANCE):
		_fail("%s canvas mismatch expected=%s actual=%s" % [label, expected_canvas, board_canvas.get_global_rect()])
		return false
	if not _rects_close(backdrop_fill.get_global_rect(), Rect2(Vector2.ZERO, Vector2(size)), RECT_TOLERANCE):
		_fail("%s backdrop fill does not cover viewport" % label)
		return false

	var board_art := _board.get_node("BoardCanvas/BoardArt") as TextureRect
	var vertical_backdrop := _board.get_node("BoardCanvas/VerticalBackdrop") as TextureRect
	var is_standard := expected_variant == "Standard"
	if board_art.visible != is_standard or vertical_backdrop.visible == is_standard:
		_fail("%s variant visibility mismatch expected=%s" % [label, expected_variant])
		return false

	var scale := maxf(expected_canvas.size.x / DESIGN_SIZE.x, expected_canvas.size.y / DESIGN_SIZE.y)
	var offset := (expected_canvas.size - DESIGN_SIZE * scale) * 0.5
	var profile: Dictionary = STANDARD_RECTS if is_standard else VERTICAL_RECTS
	for path in profile:
		var control := _board.get_node(path) as Control
		var expected := _transform_rect(profile[path], expected_canvas.position + offset, scale)
		if not _rects_close(control.get_global_rect(), expected, RECT_TOLERANCE):
			_fail("%s %s mismatch expected=%s actual=%s" % [label, path, expected, control.get_global_rect()])
			return false

	var markers: Dictionary = STANDARD_MARKERS if is_standard else VERTICAL_MARKERS
	for path in markers:
		var marker := _board.get_node(path) as Marker2D
		var expected_position: Vector2 = expected_canvas.position + offset + Vector2(markers[path]) * scale
		if marker.global_position.distance_to(expected_position) > RECT_TOLERANCE:
			_fail("%s %s marker mismatch expected=%s actual=%s" % [label, path, expected_position, marker.global_position])
			return false
	return true


func _expected_canvas_rect(viewport_size: Vector2) -> Rect2:
	var aspect := viewport_size.x / viewport_size.y
	if aspect < 1.5:
		var canvas_size := Vector2(viewport_size.x, viewport_size.x / 1.5)
		return Rect2(Vector2(0, (viewport_size.y - canvas_size.y) * 0.5), canvas_size)
	if aspect > 16.0 / 9.0:
		var canvas_size := Vector2(viewport_size.y * 16.0 / 9.0, viewport_size.y)
		return Rect2(Vector2((viewport_size.x - canvas_size.x) * 0.5, 0), canvas_size)
	return Rect2(Vector2.ZERO, viewport_size)


func _transform_rect(rect: Rect2, origin: Vector2, scale: float) -> Rect2:
	return Rect2(origin + rect.position * scale, rect.size * scale)


func _capture_tracked_rects() -> Dictionary:
	var result := {}
	for path in STANDARD_RECTS:
		result[path] = (_board.get_node(path) as Control).get_global_rect()
	return result


func _rects_close(first: Rect2, second: Rect2, tolerance: float) -> bool:
	return first.position.distance_to(second.position) <= tolerance \
		and first.size.distance_to(second.size) <= tolerance


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("GAME_BOARD_RESPONSIVE_LAYOUT_AUDIT_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
