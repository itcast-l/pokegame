extends SceneTree

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const PLAYER_AVATAR_PATH := "LayoutAreas/BottomPlayerArea/Player1AvatarArea/Player1Avatar"
const ENEMY_PLAYER_INDEX := 1
const FORCED_VICTORY_POINTS := 100
const TIMEOUT_SECONDS := 8.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return

	var board := packed_scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", FORCED_VICTORY_POINTS)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_tutorial_mode", true)
	board.set("adventure_enemy_avatar_id", "fish")
	root.add_child(board)
	await process_frame

	if not board.has_method("DebugAddScoreAndCheckWinner") \
			or not board.has_method("DebugRestartWithoutOpeningDealAnimation"):
		_fail("Missing board restart debug hooks")
		return

	var avatar := board.get_node_or_null(PLAYER_AVATAR_PATH)
	if avatar == null:
		_fail("Missing main player avatar")
		return

	if not bool(board.call("DebugAddScoreAndCheckWinner", ENEMY_PLAYER_INDEX, FORCED_VICTORY_POINTS)):
		_fail("Failed to force adventure defeat")
		return

	if not await _wait_for_defeat_pose(avatar):
		return

	if not bool(board.call("DebugRestartWithoutOpeningDealAnimation")):
		_fail("Failed to restart adventure battle")
		return

	await process_frame
	var portrait := avatar.get_node("PortraitPivot/Portrait") as TextureRect
	var pivot := avatar.get_node("PortraitPivot") as Control
	if portrait.texture == null or not portrait.texture.resource_path.contains("/idle/"):
		_fail("Player avatar frame did not return to idle: %s" % portrait.texture.resource_path)
		return
	if not portrait.modulate.is_equal_approx(Color.WHITE):
		_fail("Player avatar tint did not reset to idle: %s" % portrait.modulate)
		return
	if not is_zero_approx(pivot.rotation):
		_fail("Player avatar rotation did not reset to idle: %s" % pivot.rotation)
		return
	if not pivot.scale.is_equal_approx(Vector2.ONE):
		_fail("Player avatar scale did not reset to idle: %s" % pivot.scale)
		return

	board.queue_free()
	await process_frame
	print("ADVENTURE_DEFEAT_AVATAR_RESTART_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _wait_for_defeat_pose(avatar: Node) -> bool:
	var portrait := avatar.get_node("PortraitPivot/Portrait") as TextureRect
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if portrait.modulate.r < 0.8:
			return true

	_fail("Timed out waiting for player avatar defeat pose")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_DEFEAT_AVATAR_RESTART_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
