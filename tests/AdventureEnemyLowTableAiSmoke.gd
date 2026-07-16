extends SceneTree

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
	if not _expect_probe(shrimp_board, true, 2, 0, 0, true, "Capture", 0, "stage 1 best capture"):
		return
	if not _expect_probe(shrimp_board, true, 2, 99, 0, true, "None", -1, "rejected capture"):
		return
	if not _expect_probe(shrimp_board, false, 2, 0, 0, true, "Build", 0, "two-card early build"):
		return
	if not _expect_probe(shrimp_board, false, 3, 0, 0, true, "None", -1, "three-card build cutoff"):
		return
	await _free_board(shrimp_board)

	var boss_board := await _create_board(packed_scene, true, false, "boss_k", 1)
	if boss_board == null:
		return
	if not _expect_probe(boss_board, true, 1, 89, 0, true, "Capture", 0, "boss pair threshold minus one"):
		return
	if not _expect_probe(boss_board, true, 1, 90, 0, true, "None", -1, "boss pair threshold"):
		return
	await _free_board(boss_board)

	for tutorial_avatar in ["fish", "lanternfish"]:
		var tutorial_board := await _create_board(packed_scene, true, true, tutorial_avatar, 1)
		if tutorial_board == null:
			return
		if not _expect_probe(tutorial_board, true, 2, 0, 0, false, "None", -1, "%s tutorial" % tutorial_avatar):
			return
		await _free_board(tutorial_board)

	for difficulty in [0, 1, 2]:
		var solo_board := await _create_board(packed_scene, false, false, "", difficulty)
		if solo_board == null:
			return
		if not _expect_probe(solo_board, true, 2, 0, 0, false, "None", -1, "solo difficulty %d" % difficulty):
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
	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate board")
		return null

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("solo_mode", true)
	board.set("solo_ai_difficulty", difficulty)
	board.set("adventure_mode", adventure_mode)
	board.set("adventure_tutorial_mode", adventure_tutorial_mode)
	if not avatar_id.is_empty():
		board.set("adventure_enemy_avatar_id", avatar_id)
	root.add_child(board)
	await process_frame
	if not board.has_method("DebugProbeAdventureEnemyLowTableAi"):
		_fail("Missing adventure enemy low-table AI probe")
		return null
	return board


func _expect_probe(
		board: Node,
		include_capture: bool,
		table_count: int,
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
