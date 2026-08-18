extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return

	var shrimp_board := await _create_board(packed_scene, true, false, "shrimp", 1)
	if shrimp_board == null:
		return
	if not _expect_probe(shrimp_board, true, 2, 0, 0, 0, true, "Capture", 0, "stage 1 best capture"):
		return
	if not _expect_probe(shrimp_board, true, 2, 0, 99, 0, true, "None", -1, "rejected capture"):
		return
	if not _expect_probe(shrimp_board, false, 2, 0, 0, 0, true, "Build", 0, "two-card early build"):
		return
	if not _expect_probe(shrimp_board, false, 3, 0, 0, 0, true, "None", -1, "three-card build cutoff"):
		return
	if not _expect_probe(shrimp_board, true, 2, 1, 0, 0, true, "Build", 0, "threshold boundary build"):
		return
	if not _expect_probe(shrimp_board, true, 2, 1, 55, 0, true, "None", -1, "threshold boundary none"):
		return
	if not _expect_probe(shrimp_board, true, 3, 2, 0, 0, true, "None", -1, "three-card threshold boundary"):
		return
	if not _expect_probe(shrimp_board, true, 3, 1, 0, 0, true, "Capture", 0, "capture exceeds threshold"):
		return
	await _free_board(shrimp_board)

	var boss_board := await _create_board(packed_scene, true, false, "boss_k", 1)
	if boss_board == null:
		return
	if not _expect_probe(boss_board, true, 1, 0, 89, 0, true, "None", -1, "boss blocked pair below legacy chance"):
		return
	if not _expect_probe(boss_board, true, 1, 0, 90, 0, true, "None", -1, "boss blocked pair at legacy chance"):
		return
	await _free_board(boss_board)

	for tutorial_avatar in ["fish", "lanternfish"]:
		var tutorial_board := await _create_board(packed_scene, true, true, tutorial_avatar, 1)
		if tutorial_board == null:
			return
		if not _expect_probe(tutorial_board, true, 2, 0, 0, 0, false, "None", -1, "%s tutorial" % tutorial_avatar):
			return
		await _free_board(tutorial_board)

	for difficulty in [0, 1, 2]:
		var solo_board := await _create_board(packed_scene, false, false, "", difficulty)
		if solo_board == null:
			return
		if not _expect_probe(solo_board, true, 2, 0, 0, 0, false, "None", -1, "solo difficulty %d" % difficulty):
			return
		await _free_board(solo_board)

	print("ADVENTURE_ENEMY_LOW_TABLE_AI_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _create_board(
		packed_scene: PackedScene,
		adventure_mode: bool,
		adventure_tutorial_mode: bool,
		avatar_id: String,
		difficulty: int) -> Node:
	var board: Node
	if adventure_tutorial_mode:
		board = GameBoardSmokeFactory.create_tutorial(
			packed_scene,
			avatar_id,
			100,
			true,
			difficulty)
	elif adventure_mode:
		board = GameBoardSmokeFactory.create_adventure(
			packed_scene,
			avatar_id,
			100,
			true,
			difficulty)
	else:
		board = GameBoardSmokeFactory.create_solo(packed_scene, 2, true, 100, difficulty)
	if board == null:
		_fail("Failed to instantiate board")
		return null

	root.add_child(board)
	if not await _prepare_probe_board(board):
		_fail("Adventure AI probe board did not become ready")
		return null
	if not board.has_method("DebugProbeAdventureEnemyLowTableAi"):
		_fail("Missing adventure enemy low-table AI probe")
		return null
	return board


func _prepare_probe_board(board: Node) -> bool:
	for frame_index in range(300):
		await process_frame
		if board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
				and bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
				return false
			continue
		if frame_index % 5 == 0 \
				and board.has_method("DebugRestartWithoutOpeningDealAnimation") \
				and bool(board.call("DebugRestartWithoutOpeningDealAnimation")):
			return true
	return false


func _expect_probe(
		board: Node,
		include_capture: bool,
		table_count: int,
		base_play_capture_threshold: int,
		roll: int,
		ordinal: int,
		expected_eligible: bool,
		expected_kind: String,
		expected_hand_index: int,
		label: String) -> bool:
	var result := board.call(
		"DebugProbeAdventureEnemyLowTableAi",
		include_capture,
		table_count,
		base_play_capture_threshold,
		roll,
		ordinal) as Dictionary
	if result == null:
		_fail("%s returned no probe result" % label)
		return false
	if bool(result.get("eligible", false)) != expected_eligible:
		_fail("%s eligibility mismatch: %s" % [label, result])
		return false
	if String(result.get("kind", "")) != expected_kind:
		_fail("%s selection mismatch: %s" % [label, result])
		return false
	if int(result.get("hand_index", -1)) != expected_hand_index:
		_fail("%s hand index mismatch: %s" % [label, result])
		return false
	return true


func _free_board(board: Node) -> void:
	board.queue_free()
	await process_frame
	await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_ENEMY_LOW_TABLE_AI_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
