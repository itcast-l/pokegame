extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1080, 720)
const CENTER_TOLERANCE := 0.05
const PLAYER_PATHS := [
	{
		"area": "BoardCanvas/LayoutAreas/BottomPlayerArea/Player1AvatarArea",
		"meter": "BoardCanvas/LayoutAreas/BottomPlayerArea/Player1ScoreMeter",
		"effect": "%Player1ScoreEffectTarget",
		"avatar": "Player1Avatar",
	},
	{
		"area": "BoardCanvas/LayoutAreas/TopPlayerArea/Player2AvatarArea",
		"meter": "BoardCanvas/LayoutAreas/TopPlayerArea/Player2ScoreMeter",
		"effect": "%Player2ScoreEffectTarget",
		"avatar": "Player2Avatar",
	},
	{
		"area": "BoardCanvas/LayoutAreas/LeftPlayerArea/Player3AvatarArea",
		"meter": "BoardCanvas/LayoutAreas/LeftPlayerArea/Player3ScoreMeter",
		"effect": "%Player3ScoreEffectTarget",
		"avatar": "Player3Avatar",
	},
	{
		"area": "BoardCanvas/LayoutAreas/RightPlayerArea/Player4AvatarArea",
		"meter": "BoardCanvas/LayoutAreas/RightPlayerArea/Player4ScoreMeter",
		"effect": "%Player4ScoreEffectTarget",
		"avatar": "Player4Avatar",
	},
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var viewport := SubViewport.new()
	viewport.size = VIEWPORT_SIZE
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

	var layout_areas := board.get_node("BoardCanvas/LayoutAreas") as Control
	if layout_areas.get_script() != null:
		_fail("LayoutAreas should not depend on an authoring preview script")
		return
	if not layout_areas.position.is_equal_approx(Vector2(-100, 0)) \
			or not layout_areas.size.is_equal_approx(Vector2(1280, 720)) \
			or not layout_areas.scale.is_equal_approx(Vector2.ONE):
		_fail("Vertical design transform mismatch position=%s size=%s scale=%s" % [
			layout_areas.position,
			layout_areas.size,
			layout_areas.scale,
		])
		return

	if not _assert_avatar_alignment(board, "vertical"):
		return

	viewport.size = Vector2i(1280, 720)
	await process_frame
	await process_frame
	if not _assert_avatar_alignment(board, "standard"):
		return

	print("GAME_BOARD_MANUAL_LAYOUT_PARITY_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _assert_avatar_alignment(board: Control, layout_name: String) -> bool:
	for paths in PLAYER_PATHS:
		var area := board.get_node(paths.area) as Control
		var meter := board.get_node(paths.meter) as Control
		var effect := board.get_node(paths.effect) as Marker2D
		var avatar := area.get_node(paths.avatar) as Control
		var area_center := area.get_global_rect().get_center()
		var meter_center := meter.get_global_rect().get_center()
		if area_center.distance_to(meter_center) > CENTER_TOLERANCE:
			_fail("%s avatar and score centers drifted area=%s meter=%s" % [layout_name, area_center, meter_center])
			return false
		if area_center.distance_to(effect.global_position) > CENTER_TOLERANCE:
			_fail("%s avatar and effect centers drifted area=%s effect=%s" % [layout_name, area_center, effect.global_position])
			return false
		if not area.scale.is_equal_approx(Vector2.ONE):
			_fail("Avatar area still uses mixed scale: %s" % area.scale)
			return false
		if not avatar.scale.is_equal_approx(Vector2.ONE):
			_fail("CharacterAvatar should fill an unscaled area: %s" % avatar.scale)
			return false
		if not avatar.get_global_rect().is_equal_approx(area.get_global_rect()):
			_fail("CharacterAvatar does not fill its authored area avatar=%s area=%s" % [
				avatar.get_global_rect(),
				area.get_global_rect(),
			])
			return false
	return true


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
