extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const SAVED_RUN_AVATAR_ID := "shrimp"
const RESTART_TARGET_AVATAR_ID := "crab"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var saved_board := await _create_saved_run_board()
	if _failed or saved_board == null:
		return

	saved_board.queue_free()
	await process_frame
	await process_frame

	var continue_menu := await _instantiate_menu(RESTART_TARGET_AVATAR_ID)
	if _failed or continue_menu == null:
		return

	if not await _open_resume_dialog(continue_menu):
		return

	if not bool(continue_menu.call("DebugSelectAdventureRunResumeContinue")):
		_fail("Failed to select Continue on the adventure run resume dialog")
		return

	var continued_board := await _wait_for_board_scene()
	if _failed or continued_board == null:
		return

	if String(continued_board.get("adventure_enemy_avatar_id")) != SAVED_RUN_AVATAR_ID:
		_fail("Continue should reopen the saved adventure stage instead of the highlighted selection")
		return

	if not await _wait_for_actionable_state(continued_board):
		return

	if int(continued_board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Continue should preserve previously acquired adventure upgrades")
		return

	continued_board.queue_free()
	await process_frame
	await process_frame

	var restart_menu := await _instantiate_menu(RESTART_TARGET_AVATAR_ID)
	if _failed or restart_menu == null:
		return

	if not await _open_resume_dialog(restart_menu):
		return

	if not bool(restart_menu.call("DebugSelectAdventureRunResumeRestart")):
		_fail("Failed to select Restart on the adventure run resume dialog")
		return

	var restarted_board := await _wait_for_board_scene()
	if _failed or restarted_board == null:
		return

	if String(restarted_board.get("adventure_enemy_avatar_id")) != RESTART_TARGET_AVATAR_ID:
		_fail("Restart should start from the currently highlighted adventure selection")
		return

	if not await _wait_for_actionable_state(restarted_board):
		return

	if int(restarted_board.call("DebugGetAdventureAcquiredUpgradeCount")) != 0:
		_fail("Restart should clear previously acquired adventure upgrades")
		return

	restarted_board.queue_free()
	await process_frame
	print("ADVENTURE_UPGRADE_RUN_RESUME_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _create_saved_run_board() -> Node:
	var board := await _instantiate_board(SAVED_RUN_AVATAR_ID)
	if _failed or board == null:
		return null

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing adventure upgrade run-start debug hooks")
		return null

	if not await _wait_for_upgrade_selection(board):
		return null

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to select the first upgrade while creating a saved run")
		return null

	if not await _wait_for_actionable_state(board):
		return null

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Expected exactly one acquired upgrade after seeding the saved run")
		return null

	return board


func _instantiate_board(adventure_avatar_id: String) -> Node:
	await process_frame

	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % BOARD_SCENE_PATH)
		return null

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % BOARD_SCENE_PATH)
		return null

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", adventure_avatar_id)

	root.add_child(board)
	await process_frame
	return board


func _instantiate_menu(selected_avatar_id: String) -> Node:
	await process_frame

	var packed_scene := load(MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % MENU_SCENE_PATH)
		return null

	var menu := packed_scene.instantiate()
	if menu == null:
		_fail("Failed to instantiate scene: %s" % MENU_SCENE_PATH)
		return null

	menu.set("selected_avatar_id", selected_avatar_id)
	root.add_child(menu)
	await process_frame

	if not menu.has_method("DebugIsAdventureRunResumeDialogVisible") \
		or not menu.has_method("DebugSelectAdventureRunResumeContinue") \
		or not menu.has_method("DebugSelectAdventureRunResumeRestart"):
		_fail("Missing adventure run resume dialog debug hooks")
		return null

	return menu


func _open_resume_dialog(menu: Node) -> bool:
	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null:
		_fail("Adventure avatar menu is missing StartButton")
		return false

	start_button.emit_signal("pressed")
	return await _wait_for_resume_dialog(menu)


func _wait_for_resume_dialog(menu: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(menu):
			_fail("Adventure avatar menu freed unexpectedly while waiting for resume dialog")
			return false

		if bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
			return true

	_fail("Timed out waiting for the adventure run resume dialog")
	return false


func _wait_for_board_scene() -> Node:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		var scene := current_scene
		if scene != null \
			and is_instance_valid(scene) \
			and scene.has_method("DebugGetAdventureAcquiredUpgradeCount") \
			and scene.has_method("DebugGetGameLoopPhaseName"):
			return scene

	_fail("Timed out waiting for the adventure board scene to load")
	return null


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for upgrade selection")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

	_fail("Timed out waiting for the adventure upgrade selection")
	return false


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for actionable state")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			continue

		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true

	_fail("Timed out waiting for the adventure board to become actionable")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_RUN_RESUME_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
