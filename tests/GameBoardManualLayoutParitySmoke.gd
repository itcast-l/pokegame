extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1080, 720)
const RECT_TOLERANCE := 0.5
const TRACKED_NODE_PATHS := [
	"LayoutAreas",
	"LayoutAreas/TopPlayerArea/Player2AvatarArea",
	"LayoutAreas/Player2HandArea",
	"LayoutAreas/Player2CardPileArea",
	"LayoutAreas/BottomPlayerArea/Player1AvatarArea",
	"LayoutAreas/Player1HandArea",
	"LayoutAreas/Player1CardPileArea",
	"LayoutAreas/LeftPlayerArea/Player3AvatarArea",
	"LayoutAreas/Player3HandArea",
	"LayoutAreas/Player3CardPileArea",
	"LayoutAreas/RightPlayerArea/Player4AvatarArea",
	"LayoutAreas/Player4HandArea",
	"LayoutAreas/Player4CardPileArea",
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var authoring_rects := await _capture_rects(false)
	if _failed:
		return

	var runtime_rects := await _capture_rects(true)
	if _failed:
		return

	for node_path in TRACKED_NODE_PATHS:
		var authoring_rect: Rect2 = authoring_rects.get(node_path, Rect2())
		var runtime_rect: Rect2 = runtime_rects.get(node_path, Rect2())
		if not _rects_close(authoring_rect, runtime_rect, RECT_TOLERANCE):
			_fail("%s drifted between authoring and runtime authoring=%s runtime=%s" % [
				node_path,
				str(authoring_rect),
				str(runtime_rect),
			])
			return

	print("GAME_BOARD_MANUAL_LAYOUT_PARITY_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _capture_rects(use_runtime_script: bool) -> Dictionary:
	var viewport := SubViewport.new()
	viewport.size = VIEWPORT_SIZE
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return {}

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return {}

	if not use_runtime_script:
		board.set_script(null)

	viewport.add_child(board)
	await process_frame
	await process_frame

	var rects := {}
	for node_path in TRACKED_NODE_PATHS:
		var control := board.get_node_or_null(node_path) as Control
		if control == null:
			var mode_name := "runtime" if use_runtime_script else "authoring"
			_fail("Missing control in %s mode: %s" % [
				mode_name,
				node_path,
			])
			return {}

		rects[node_path] = control.get_global_rect()

	board.queue_free()
	await process_frame
	viewport.queue_free()
	await process_frame
	return rects


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
	print("GAME_BOARD_MANUAL_LAYOUT_PARITY_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
