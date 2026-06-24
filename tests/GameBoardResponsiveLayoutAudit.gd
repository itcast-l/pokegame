extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SPECS := [
	{"name": "baseline", "size": Vector2i(1280, 720)},
	{"name": "tall", "size": Vector2i(960, 900)},
	{"name": "wide", "size": Vector2i(1680, 720)},
]
const RECT_TOLERANCE := 1.5
const NORMALIZED_TOLERANCE := 0.02
const GUTTER_SAMPLE_BAND_HEIGHT := 96
const GUTTER_COLOR_DELTA_MIN := 0.035
const TRACKED_NODE_PATHS := [
	"LayoutAreas/Player1CardPileArea",
	"LayoutAreas/Player2CardPileArea",
	"LayoutAreas/Player3CardPileArea",
	"LayoutAreas/Player1HandArea",
	"LayoutAreas/Player2HandArea",
	"LayoutAreas/Player3HandArea",
	"LayoutAreas/TableCardLayoutArea",
	"LayoutAreas/BottomPlayerArea/Player1AvatarArea",
	"LayoutAreas/TopPlayerArea/Player2AvatarArea",
	"LayoutAreas/LeftPlayerArea/Player3AvatarArea",
]

var _failed := false
var _baseline_metrics := {}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	for spec in VIEWPORT_SPECS:
		if _failed:
			return
		await _audit_viewport(spec)

	if not _failed:
		print("GAME_BOARD_RESPONSIVE_LAYOUT_AUDIT_OK")
		await _dispose_audio_before_quit()
		quit(0)


func _audit_viewport(spec: Dictionary) -> void:
	root.size = spec.get("size", Vector2i(1280, 720))
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

	var mode_name := String(spec.get("name", "unknown"))
	var backdrop := board.get_node_or_null("UltraWideBackdrop") as TextureRect
	var board_art := board.get_node_or_null("BoardArt") as TextureRect
	var layout_areas := board.get_node_or_null("LayoutAreas") as Control
	if backdrop == null or board_art == null or layout_areas == null:
		_fail("%s missing UltraWideBackdrop, BoardArt, or LayoutAreas" % mode_name)
		return
	if backdrop.get_global_rect().size.x <= 0.0 or backdrop.get_global_rect().size.y <= 0.0:
		_fail("%s backdrop has invalid rect: %s" % [mode_name, str(backdrop.get_global_rect())])
		return

	var board_rect := _get_board_art_visible_rect(board_art)
	var layout_rect := layout_areas.get_global_rect()
	if not _rects_close(board_rect, layout_rect, RECT_TOLERANCE):
		_fail("%s layout areas do not match visible board rect: board=%s layout=%s" % [
			mode_name,
			str(board_rect),
			str(layout_rect),
		])
		return

	var metrics := {}
	for node_path in TRACKED_NODE_PATHS:
		var control := board.get_node_or_null(node_path) as Control
		if control == null:
			_fail("%s missing control: %s" % [mode_name, node_path])
			return

		var rect := control.get_global_rect()
		if not _rect_inside(rect, board_rect, RECT_TOLERANCE):
			_fail("%s control outside board rect: %s rect=%s board=%s" % [
				mode_name,
				node_path,
				str(rect),
				str(board_rect),
			])
			return

		metrics[node_path] = _build_normalized_metric(rect, board_rect)

	if mode_name == "baseline":
		_baseline_metrics = metrics
	else:
		for node_path in TRACKED_NODE_PATHS:
			if not _metrics_close(_baseline_metrics.get(node_path, {}), metrics.get(node_path, {}), NORMALIZED_TOLERANCE):
				_fail("%s normalized metric drifted for %s baseline=%s current=%s" % [
					mode_name,
					node_path,
					str(_baseline_metrics.get(node_path, {})),
					str(metrics.get(node_path, {})),
				])
				return

	if mode_name == "wide":
		await RenderingServer.frame_post_draw
		var image := root.get_viewport().get_texture().get_image()
		if image == null:
			_fail("%s viewport image missing" % mode_name)
			return

		var left_gutter := Rect2i(
			0,
			maxi(0, int(board_rect.get_center().y) - GUTTER_SAMPLE_BAND_HEIGHT / 2),
			maxi(0, int(board_rect.position.x)),
			mini(GUTTER_SAMPLE_BAND_HEIGHT, image.get_height()))
		var right_gutter_width := maxi(0, image.get_width() - int(ceil(board_rect.end.x)))
		var right_gutter := Rect2i(
			int(ceil(board_rect.end.x)),
			maxi(0, int(board_rect.get_center().y) - GUTTER_SAMPLE_BAND_HEIGHT / 2),
			right_gutter_width,
			mini(GUTTER_SAMPLE_BAND_HEIGHT, image.get_height()))
		if left_gutter.size.x <= 0 or right_gutter.size.x <= 0:
			_fail("%s expected visible side gutters, got left=%s right=%s board=%s image=%dx%d" % [
				mode_name,
				str(left_gutter),
				str(right_gutter),
				str(board_rect),
				image.get_width(),
				image.get_height(),
			])
			return

		var left_color := _average_color(image, left_gutter)
		var right_color := _average_color(image, right_gutter)
		if not _is_underwater_tinted(left_color):
			_fail("%s left gutter still too neutral: %s rect=%s" % [mode_name, str(left_color), str(left_gutter)])
			return
		if not _is_underwater_tinted(right_color):
			_fail("%s right gutter still too neutral: %s rect=%s" % [mode_name, str(right_color), str(right_gutter)])
			return

	board.queue_free()
	await process_frame


func _get_board_art_visible_rect(board_art: TextureRect) -> Rect2:
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


func _build_normalized_metric(rect: Rect2, board_rect: Rect2) -> Dictionary:
	var center := rect.get_center()
	return {
		"center_x": (center.x - board_rect.position.x) / board_rect.size.x,
		"center_y": (center.y - board_rect.position.y) / board_rect.size.y,
		"width": rect.size.x / board_rect.size.x,
		"height": rect.size.y / board_rect.size.y,
	}


func _metrics_close(first: Dictionary, second: Dictionary, tolerance: float) -> bool:
	return absf(float(first.get("center_x", 0.0)) - float(second.get("center_x", 0.0))) <= tolerance \
		and absf(float(first.get("center_y", 0.0)) - float(second.get("center_y", 0.0))) <= tolerance \
		and absf(float(first.get("width", 0.0)) - float(second.get("width", 0.0))) <= tolerance \
		and absf(float(first.get("height", 0.0)) - float(second.get("height", 0.0))) <= tolerance


func _rects_close(first: Rect2, second: Rect2, tolerance: float) -> bool:
	return absf(first.position.x - second.position.x) <= tolerance \
		and absf(first.position.y - second.position.y) <= tolerance \
		and absf(first.size.x - second.size.x) <= tolerance \
		and absf(first.size.y - second.size.y) <= tolerance


func _rect_inside(inner: Rect2, outer: Rect2, tolerance: float) -> bool:
	return inner.position.x >= outer.position.x - tolerance \
		and inner.position.y >= outer.position.y - tolerance \
		and inner.end.x <= outer.end.x + tolerance \
		and inner.end.y <= outer.end.y + tolerance


func _average_color(image: Image, rect: Rect2i) -> Color:
	var start_x := maxi(0, rect.position.x)
	var start_y := maxi(0, rect.position.y)
	var end_x := mini(image.get_width(), rect.end.x)
	var end_y := mini(image.get_height(), rect.end.y)
	if start_x >= end_x or start_y >= end_y:
		return Color.BLACK

	var sum := Color(0, 0, 0, 0)
	var count := 0
	var step_x := maxi(1, (end_x - start_x) / 16)
	var step_y := maxi(1, (end_y - start_y) / 8)
	for y in range(start_y, end_y, step_y):
		for x in range(start_x, end_x, step_x):
			sum += image.get_pixel(x, y)
			count += 1

	if count == 0:
		return Color.BLACK

	return sum / float(count)


func _is_underwater_tinted(color: Color) -> bool:
	var max_channel := maxf(color.r, maxf(color.g, color.b))
	var min_channel := minf(color.r, minf(color.g, color.b))
	return color.g - color.r >= GUTTER_COLOR_DELTA_MIN \
		and color.b - color.r >= GUTTER_COLOR_DELTA_MIN \
		and max_channel - min_channel >= GUTTER_COLOR_DELTA_MIN


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
