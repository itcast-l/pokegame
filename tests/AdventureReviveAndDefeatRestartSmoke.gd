extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const PLAYER_AVATAR_PATH := "BoardCanvas/LayoutAreas/BottomPlayerArea/Player1AvatarArea/Player1Avatar"
const MIDDLE_STAGE_AVATAR_ID := "manta_ray"
const FIRST_STAGE_AVATAR_ID := "shrimp"
const MIDDLE_STAGE_INDEX := 4
const MAIN_PLAYER_INDEX := 0
const ENEMY_PLAYER_INDEX := 1
const FORCED_VICTORY_POINTS := 100
const TIMEOUT_SECONDS := 24.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var board := await _instantiate_board(MIDDLE_STAGE_AVATAR_ID)
	if _failed or board == null:
		return

	if not _has_required_board_hooks(board):
		return
	if not await _wait_for_actionable_state(board):
		return
	if not bool(board.call("DebugAcquireAdventureUpgrade", "spades_black_ledger")):
		_fail("Failed to seed the middle-stage run with an upgrade")
		return
	await process_frame

	var before_first_defeat := board.call("DebugGetAdventureRunStateSnapshot") as Dictionary
	if not _assert_run_state(before_first_defeat, MIDDLE_STAGE_INDEX, MIDDLE_STAGE_AVATAR_ID, 1, false):
		return
	var expected_revive_health := maxi(1, int(before_first_defeat["max_health"]) / 2)

	if not bool(board.call("DebugAddScoreAndCheckWinner", ENEMY_PLAYER_INDEX, FORCED_VICTORY_POINTS)):
		_fail("Failed to force the first main-adventure defeat")
		return
	if not await _wait_for_winner_banner(board):
		return

	var first_defeat := board.call("DebugGetAdventureRunStateSnapshot") as Dictionary
	if String(first_defeat["defeat_resolution"]) != "RetryCurrentStage":
		_fail("First defeat did not settle as RetryCurrentStage")
		return
	if not bool(first_defeat["has_used_revive"]):
		_fail("First defeat did not consume the one revive")
		return
	if int(first_defeat["current_health"]) != expected_revive_health:
		_fail("First defeat health mismatch: expected %d, got %d" % [expected_revive_health, int(first_defeat["current_health"])])
		return
	if not _assert_run_state(first_defeat, MIDDLE_STAGE_INDEX, MIDDLE_STAGE_AVATAR_ID, 1, true):
		return
	if String(board.call("DebugGetWinnerPrimaryButtonText")) != tr("GAME_RETRY_ADVENTURE_STAGE"):
		_fail("First defeat primary button does not use GAME_RETRY_ADVENTURE_STAGE")
		return

	if not bool(board.call("DebugPressWinnerReturnButton")):
		_fail("Failed to return to the adventure menu after first defeat")
		return
	var menu := await _wait_for_menu_scene()
	if _failed or menu == null:
		return
	if not await _wait_for_resume_dialog(menu):
		return
	if not bool(menu.call("DebugAreAdventureMenuBackgroundButtonsDisabled")):
		_fail("Active run dialog did not lock stage-selection controls")
		return
	if not bool(menu.call("DebugSelectAdventureRunResumeContinue")):
		_fail("Failed to continue the preserved run")
		return

	board = await _wait_for_board_scene()
	if _failed or board == null:
		return
	if GameBoardSmokeFactory.get_adventure_enemy_avatar_id(board) != MIDDLE_STAGE_AVATAR_ID:
		_fail("Continue did not reopen the defeated middle stage")
		return
	if not await _wait_for_actionable_state(board):
		return
	var resumed := board.call("DebugGetAdventureRunStateSnapshot") as Dictionary
	if not _assert_run_state(resumed, MIDDLE_STAGE_INDEX, MIDDLE_STAGE_AVATAR_ID, 1, true):
		return
	if int(resumed["current_health"]) != expected_revive_health:
		_fail("Resumed stage did not retain revived health")
		return

	if not bool(board.call("DebugAddScoreAndCheckWinner", ENEMY_PLAYER_INDEX, FORCED_VICTORY_POINTS)):
		_fail("Failed to force the second main-adventure defeat")
		return
	if not await _wait_for_winner_banner(board):
		return
	var second_defeat := board.call("DebugGetAdventureRunStateSnapshot") as Dictionary
	if String(second_defeat["defeat_resolution"]) != "RestartJourney":
		_fail("Second defeat did not settle as RestartJourney")
		return
	if String(board.call("DebugGetWinnerPrimaryButtonText")) != tr("GAME_RESTART_ADVENTURE_JOURNEY"):
		_fail("Second defeat primary button does not use GAME_RESTART_ADVENTURE_JOURNEY")
		return
	if not await _wait_for_defeat_pose(board):
		return
	if not bool(board.call("DebugPressWinnerPrimaryButton")):
		_fail("Failed to activate the second-defeat restart button")
		return

	var restarted_board := await _wait_for_board_scene(FIRST_STAGE_AVATAR_ID)
	if _failed or restarted_board == null:
		return
	if GameBoardSmokeFactory.get_adventure_enemy_avatar_id(restarted_board) != FIRST_STAGE_AVATAR_ID:
		_fail("Second defeat did not switch to the first main stage")
		return
	var restarted := restarted_board.call("DebugGetAdventureRunStateSnapshot") as Dictionary
	if not _assert_run_state(restarted, 1, FIRST_STAGE_AVATAR_ID, 0, false):
		return
	if int(restarted["current_health"]) != 100 or int(restarted["max_health"]) != 100:
		_fail("Restarted journey did not restore 100/100 health")
		return
	if not await _wait_for_idle_avatar(restarted_board):
		return

	restarted_board.queue_free()
	await process_frame
	print("ADVENTURE_REVIVE_AND_DEFEAT_RESTART_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _instantiate_board(avatar_id: String) -> Node:
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return null
	var board := GameBoardSmokeFactory.create_adventure(
		packed_scene,
		avatar_id,
		FORCED_VICTORY_POINTS)
	root.add_child(board)
	current_scene = board
	await process_frame
	return board


func _has_required_board_hooks(board: Node) -> bool:
	var methods := [
		"DebugAddScoreAndCheckWinner",
		"DebugGetAdventureRunStateSnapshot",
		"DebugGetWinnerPrimaryButtonText",
		"DebugIsWinnerBannerVisible",
		"DebugPressWinnerPrimaryButton",
		"DebugPressWinnerReturnButton",
		"DebugAcquireAdventureUpgrade",
		"DebugIsAdventureUpgradeSelectionVisible",
		"DebugGetGameLoopPhaseName",
	]
	for method in methods:
		if not board.has_method(method):
			_fail("Missing board debug hook: %s" % method)
			return false
	return true


func _assert_run_state(state: Dictionary, stage_index: int, avatar_id: String, upgrade_count: int, revive_used: bool) -> bool:
	if not bool(state.get("active", false)):
		_fail("Adventure run state is not active")
		return false
	if int(state.get("main_stage_index", 0)) != stage_index:
		_fail("Unexpected main stage index: %s" % state.get("main_stage_index"))
		return false
	if String(state.get("enemy_avatar_id", "")) != avatar_id:
		_fail("Unexpected run enemy avatar: %s" % state.get("enemy_avatar_id"))
		return false
	if int(state.get("acquired_upgrade_count", -1)) != upgrade_count:
		_fail("Unexpected acquired upgrade count: %s" % state.get("acquired_upgrade_count"))
		return false
	if bool(state.get("has_used_revive", false)) != revive_used:
		_fail("Unexpected revive state: %s" % state.get("has_used_revive"))
		return false
	return true


func _wait_for_upgrade_selection(board: Node) -> bool:
	return await _wait_for_board_predicate(board, "DebugIsAdventureUpgradeSelectionVisible", true, "upgrade selection")


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed while waiting for actionable state")
			return false
		if not bool(board.call("DebugIsAdventureUpgradeSelectionVisible")) \
				and String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for actionable board state")
	return false


func _wait_for_winner_banner(board: Node) -> bool:
	return await _wait_for_board_predicate(board, "DebugIsWinnerBannerVisible", true, "winner banner")


func _wait_for_board_predicate(board: Node, method: String, expected: bool, label: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed while waiting for %s" % label)
			return false
		if bool(board.call(method)) == expected:
			return true
	var phase: String = String(board.call("DebugGetGameLoopPhaseName")) if board.has_method("DebugGetGameLoopPhaseName") else "unknown"
	var state: Variant = board.call("DebugGetAdventureRunStateSnapshot") if board.has_method("DebugGetAdventureRunStateSnapshot") else {}
	_fail("Timed out waiting for %s; phase=%s state=%s" % [label, phase, state])
	return false


func _wait_for_menu_scene() -> Node:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		var scene := current_scene
		if scene != null and is_instance_valid(scene) and scene.scene_file_path == MENU_SCENE_PATH:
			return scene
	_fail("Timed out waiting for adventure menu")
	return null


func _wait_for_resume_dialog(menu: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(menu):
			_fail("Adventure menu freed while waiting for resume dialog")
			return false
		if menu.has_method("DebugIsAdventureRunResumeDialogVisible") \
				and bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
			return true
	_fail("Timed out waiting for automatic run resume dialog")
	return false


func _wait_for_board_scene(expected_avatar_id: String = "") -> Node:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		var scene := current_scene
		if scene != null and is_instance_valid(scene) and scene.scene_file_path == BOARD_SCENE_PATH \
				and scene.has_method("DebugGetAdventureRunStateSnapshot") \
				and (expected_avatar_id.is_empty() or GameBoardSmokeFactory.get_adventure_enemy_avatar_id(scene) == expected_avatar_id):
			return scene
	_fail("Timed out waiting for board scene")
	return null


func _wait_for_defeat_pose(board: Node) -> bool:
	var avatar := board.get_node_or_null(PLAYER_AVATAR_PATH)
	if avatar == null:
		_fail("Missing main-player avatar")
		return false
	var portrait := avatar.get_node("PortraitPivot/Portrait") as TextureRect
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if portrait.modulate.r < 0.8:
			return true
	_fail("Timed out waiting for defeat avatar pose")
	return false


func _wait_for_idle_avatar(board: Node) -> bool:
	var avatar := board.get_node_or_null(PLAYER_AVATAR_PATH)
	if avatar == null:
		_fail("Missing restarted main-player avatar")
		return false
	var portrait := avatar.get_node("PortraitPivot/Portrait") as TextureRect
	var pivot := avatar.get_node("PortraitPivot") as Control
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if portrait.texture != null \
				and portrait.texture.resource_path.contains("/idle/") \
				and portrait.modulate.is_equal_approx(Color.WHITE) \
				and is_zero_approx(pivot.rotation) \
				and pivot.scale.is_equal_approx(Vector2.ONE):
			return true
	_fail("Restarted main-player avatar did not return to idle")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_REVIVE_AND_DEFEAT_RESTART_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
