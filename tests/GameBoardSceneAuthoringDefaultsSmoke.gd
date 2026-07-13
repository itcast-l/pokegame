extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const FLOAT_TOLERANCE := 0.001

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	if board.get_node_or_null("UltraWideBackdrop") != null:
		_fail("Scene should not contain the removed UltraWideBackdrop")
		return

	var vertical_backdrop := board.get_node_or_null("VerticalBackdrop") as TextureRect
	var board_art := board.get_node_or_null("BoardArt") as TextureRect
	var layout_areas := board.get_node_or_null("LayoutAreas") as Control
	if vertical_backdrop == null or board_art == null or layout_areas == null:
		_fail("Scene missing VerticalBackdrop, BoardArt, or LayoutAreas")
		return

	if vertical_backdrop.visible:
		_fail("VerticalBackdrop should be hidden in the saved scene authoring state")
		return

	if not board_art.visible:
		_fail("BoardArt should be visible in the saved scene authoring state")
		return
	if board_art.stretch_mode != TextureRect.STRETCH_KEEP_ASPECT_COVERED:
		_fail("BoardArt should use a single aspect-covered background")
		return

	if layout_areas.get_script() != null:
		_fail("LayoutAreas should not have an authoring preview script attached in the saved scene")
		return

	_assert_close("LayoutAreas.anchor_left", layout_areas.anchor_left, 0.0)
	_assert_close("LayoutAreas.anchor_top", layout_areas.anchor_top, 0.0)
	_assert_close("LayoutAreas.anchor_right", layout_areas.anchor_right, 1.0)
	_assert_close("LayoutAreas.anchor_bottom", layout_areas.anchor_bottom, 1.0)
	_assert_close("LayoutAreas.offset_left", layout_areas.offset_left, 0.0)
	_assert_close("LayoutAreas.offset_top", layout_areas.offset_top, 0.0)
	_assert_close("LayoutAreas.offset_right", layout_areas.offset_right, 0.0)
	_assert_close("LayoutAreas.offset_bottom", layout_areas.offset_bottom, 0.0)
	if _failed:
		return

	board.free()
	print("GAME_BOARD_SCENE_AUTHORING_DEFAULTS_SMOKE_OK")
	quit(0)


func _assert_close(label: String, actual: float, expected: float) -> void:
	if _failed:
		return

	if absf(actual - expected) > FLOAT_TOLERANCE:
		_fail("%s expected %.3f got %.3f" % [label, expected, actual])


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("GAME_BOARD_SCENE_AUTHORING_DEFAULTS_SMOKE_FAIL %s" % message)
	quit(1)
