extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1080, 720)
const GUIDE_OUTPUT_ENV := "CODEX_VERTICAL_BACKDROP_GUIDE_OUTPUT"
const KEEP_RUNTIME_SCRIPT_ENV := "CODEX_VERTICAL_BACKDROP_KEEP_RUNTIME_SCRIPT"
const DEFAULT_OUTPUT_PATH := "res://generated-images/vertical-backdrop-authoring-guide-1080x720.png"
const SETTLE_FRAMES := 2
const GUIDE_AREAS := [
	{
		"path": "BoardCanvas/LayoutAreas/TopPlayerArea/Player2AvatarArea",
		"label": "P2 avatar",
		"border": Color(0.52, 0.98, 1.0, 0.95),
		"fill": Color(0.52, 0.98, 1.0, 0.16),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player2HandArea",
		"label": "P2 hand",
		"border": Color(0.52, 0.98, 1.0, 0.95),
		"fill": Color(0.52, 0.98, 1.0, 0.10),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player2CardPileArea",
		"label": "P2 pile",
		"border": Color(0.52, 0.98, 1.0, 0.95),
		"fill": Color(0.52, 0.98, 1.0, 0.20),
	},
	{
		"path": "BoardCanvas/LayoutAreas/BottomPlayerArea/Player1AvatarArea",
		"label": "P1 avatar",
		"border": Color(1.0, 0.84, 0.48, 0.95),
		"fill": Color(1.0, 0.84, 0.48, 0.16),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player1HandArea",
		"label": "P1 hand",
		"border": Color(1.0, 0.84, 0.48, 0.95),
		"fill": Color(1.0, 0.84, 0.48, 0.10),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player1CardPileArea",
		"label": "P1 pile",
		"border": Color(1.0, 0.84, 0.48, 0.95),
		"fill": Color(1.0, 0.84, 0.48, 0.20),
	},
	{
		"path": "BoardCanvas/LayoutAreas/LeftPlayerArea/Player3AvatarArea",
		"label": "P3 avatar",
		"border": Color(0.90, 0.54, 1.0, 0.95),
		"fill": Color(0.90, 0.54, 1.0, 0.16),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player3HandArea",
		"label": "P3 hand",
		"border": Color(0.90, 0.54, 1.0, 0.95),
		"fill": Color(0.90, 0.54, 1.0, 0.10),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player3CardPileArea",
		"label": "P3 pile",
		"border": Color(0.90, 0.54, 1.0, 0.95),
		"fill": Color(0.90, 0.54, 1.0, 0.20),
	},
	{
		"path": "BoardCanvas/LayoutAreas/RightPlayerArea/Player4AvatarArea",
		"label": "P4 avatar",
		"border": Color(0.44, 1.0, 0.66, 0.95),
		"fill": Color(0.44, 1.0, 0.66, 0.16),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player4HandArea",
		"label": "P4 hand",
		"border": Color(0.44, 1.0, 0.66, 0.95),
		"fill": Color(0.44, 1.0, 0.66, 0.10),
	},
	{
		"path": "BoardCanvas/LayoutAreas/Player4CardPileArea",
		"label": "P4 pile",
		"border": Color(0.44, 1.0, 0.66, 0.95),
		"fill": Color(0.44, 1.0, 0.66, 0.20),
	},
]

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

	if OS.get_environment(KEEP_RUNTIME_SCRIPT_ENV).strip_edges() == "":
		board.set_script(null)
	viewport.add_child(board)

	for _frame in range(SETTLE_FRAMES):
		await process_frame

	var vertical_backdrop := board.get_node_or_null("BoardCanvas/VerticalBackdrop") as TextureRect
	var board_art := board.get_node_or_null("BoardCanvas/BoardArt") as TextureRect
	var layout_areas := board.get_node_or_null("BoardCanvas/LayoutAreas") as Control
	if vertical_backdrop == null or board_art == null or layout_areas == null:
		_fail("Scene missing VerticalBackdrop, BoardArt, or LayoutAreas")
		return

	vertical_backdrop.visible = true
	board_art.visible = false
	for _frame in range(SETTLE_FRAMES):
		await process_frame

	if not vertical_backdrop.visible:
		_fail("Expected VerticalBackdrop to be visible in the 1080x720 authoring preview")
		return

	if board_art.visible:
		_fail("Expected BoardArt to be hidden in the 1080x720 authoring preview")
		return

	var overlay := _build_overlay(board)
	if overlay == null:
		return
	viewport.add_child(overlay)
	for _frame in range(SETTLE_FRAMES + 2):
		await process_frame

	var image := viewport.get_texture().get_image()
	if image == null:
		_fail("Viewport image missing while exporting vertical backdrop authoring guide")
		return

	var output_path := _resolve_output_path()
	var output_dir := output_path.get_base_dir()
	var mkdir_error := DirAccess.make_dir_recursive_absolute(output_dir)
	if mkdir_error != OK:
		_fail("Failed to create output dir %s error=%s" % [output_dir, error_string(mkdir_error)])
		return

	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save guide %s error=%s" % [output_path, error_string(save_error)])
		return

	print("VERTICAL_BACKDROP_AUTHORING_PREVIEW_OK path=%s" % output_path)
	await _dispose_audio_before_quit()
	quit(0)


func _build_overlay(board: Node) -> Control:
	var overlay := Control.new()
	overlay.name = "AuthoringGuideOverlay"
	overlay.set_anchors_preset(Control.PRESET_FULL_RECT)
	overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE

	for area in GUIDE_AREAS:
		var node_path := String(area.get("path", ""))
		var target := board.get_node_or_null(node_path) as Control
		if target == null:
			_fail("Missing guide target: %s" % node_path)
			return null

		var rect := target.get_global_rect()
		var panel := Panel.new()
		panel.name = String(area.get("label", node_path)).replace(" ", "_")
		panel.position = rect.position
		panel.size = rect.size
		panel.mouse_filter = Control.MOUSE_FILTER_IGNORE

		var style := StyleBoxFlat.new()
		style.bg_color = area.get("fill", Color(1, 1, 1, 0.14))
		style.border_color = area.get("border", Color(1, 1, 1, 0.95))
		style.border_width_left = 3
		style.border_width_top = 3
		style.border_width_right = 3
		style.border_width_bottom = 3
		panel.add_theme_stylebox_override("panel", style)
		overlay.add_child(panel)
	return overlay


func _resolve_output_path() -> String:
	var requested_path := OS.get_environment(GUIDE_OUTPUT_ENV).strip_edges()
	if not requested_path.is_empty():
		return requested_path

	return ProjectSettings.globalize_path(DEFAULT_OUTPUT_PATH)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("VERTICAL_BACKDROP_AUTHORING_PREVIEW_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
