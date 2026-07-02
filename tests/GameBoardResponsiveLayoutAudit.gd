extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SPECS := [
	{"name": "baseline", "size": Vector2i(1280, 720)},
	{"name": "tall", "size": Vector2i(960, 900)},
	{"name": "wide", "size": Vector2i(1680, 720)},
]
const RECT_TOLERANCE := 1.5
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
	var vertical_backdrop := board.get_node_or_null("VerticalBackdrop") as TextureRect
	var board_art := board.get_node_or_null("BoardArt") as TextureRect
	var layout_areas := board.get_node_or_null("LayoutAreas") as Control
	if backdrop == null or vertical_backdrop == null or board_art == null or layout_areas == null:
		_fail("%s missing UltraWideBackdrop, VerticalBackdrop, BoardArt, or LayoutAreas" % mode_name)
		return
	if backdrop.get_global_rect().size.x <= 0.0 or backdrop.get_global_rect().size.y <= 0.0:
		_fail("%s backdrop has invalid rect: %s" % [mode_name, str(backdrop.get_global_rect())])
		return

	var layout_rect := layout_areas.get_global_rect()
	var board_root := board as Control
	if board_root == null:
		_fail("%s scene root is not a Control" % mode_name)
		return

	var board_rect := board_root.get_global_rect()
	if not _rects_close(board_rect, layout_rect, RECT_TOLERANCE):
		_fail("%s layout areas do not match board rect: board=%s layout=%s" % [
			mode_name,
			str(board_rect),
			str(layout_rect),
		])
		return

	for node_path in TRACKED_NODE_PATHS:
		var control := board.get_node_or_null(node_path) as Control
		if control == null:
			_fail("%s missing control: %s" % [mode_name, node_path])
			return

		var rect := control.get_global_rect()
		if not _rect_inside(rect, layout_rect, RECT_TOLERANCE):
			_fail("%s control outside layout rect: %s rect=%s layout=%s" % [
				mode_name,
				node_path,
				str(rect),
				str(layout_rect),
			])
			return

	if mode_name == "wide":
		if not backdrop.visible:
			_fail("%s expected UltraWideBackdrop to be visible" % mode_name)
			return
		if vertical_backdrop.visible:
			_fail("%s expected VerticalBackdrop to be hidden" % mode_name)
			return
		if not board_art.visible:
			_fail("%s expected BoardArt to remain visible" % mode_name)
			return

	board.queue_free()
	await process_frame


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
