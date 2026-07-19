extends SceneTree

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const TIMEOUT_SECONDS := 20.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var board := await _create_actionable_board()
	if _failed or board == null:
		return

	if not bool(board.call("DebugStartAdventureSaveGateProbe", 1)):
		_fail("Failed to start retry-success save gate probe")
		return
	await process_frame
	if not bool(board.call("DebugIsAdventureSaveInProgress")):
		_fail("Save gate did not enter the in-progress state")
		return
	if bool(board.call("DebugCanMainPlayerAct")):
		_fail("Main-player input stayed enabled while save retry was active")
		return
	if not await _wait_for_probe_success(board):
		return

	board.queue_free()
	await process_frame
	board = await _create_actionable_board()
	if _failed or board == null:
		return
	if not bool(board.call("DebugStartAdventureSaveGateProbe", 10)):
		_fail("Failed to start exhausted save gate probe")
		return
	await process_frame
	if not bool(board.call("DebugIsAdventureSaveInProgress")) \
			or bool(board.call("DebugCanMainPlayerAct")):
		_fail("Exhausted probe did not lock input during retries")
		return
	if not await _wait_for_adventure_menu():
		return

	print("ADVENTURE_RUN_SAVE_GATE_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _create_actionable_board() -> Node:
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return null
	var board := packed_scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	current_scene = board
	await process_frame
	for method in [
		"DebugStartAdventureSaveGateProbe",
		"DebugIsAdventureSaveInProgress",
		"DebugGetAdventureSaveGateProbeAttemptCount",
		"DebugCanMainPlayerAct",
		"DebugIsAdventureUpgradeSelectionVisible",
		"DebugSelectAdventureUpgradeOption",
		"DebugGetGameLoopPhaseName",
	]:
		if not board.has_method(method):
			_fail("Missing save gate debug hook: %s" % method)
			return null

	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
				_fail("Failed to resolve run-start upgrade")
				return null
		elif String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return board
	_fail("Timed out waiting for actionable board")
	return null


func _wait_for_probe_success(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board disappeared during retry-success probe")
			return false
		if int(board.call("DebugGetAdventureSaveGateProbeAttemptCount")) >= 2 \
				and not bool(board.call("DebugIsAdventureSaveInProgress")):
			if not bool(board.call("DebugCanMainPlayerAct")):
				_fail("Input did not resume after a successful retry")
				return false
			return true
	_fail("Save gate did not continue after retry success")
	return false


func _wait_for_adventure_menu() -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		var scene := current_scene
		if scene != null and is_instance_valid(scene) and scene.scene_file_path == MENU_SCENE_PATH:
			return true
	_fail("Save retry exhaustion did not return to adventure menu")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_RUN_SAVE_GATE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
