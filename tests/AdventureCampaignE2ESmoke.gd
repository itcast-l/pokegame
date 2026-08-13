extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const PROFILE_SLOT := &"profile_0"
const TUTORIAL_IDS := ["fish", "lanternfish"]
const MAIN_PLAYER_INDEX := 0
const ENEMY_PLAYER_INDEX := 1
const FORCED_VICTORY_POINTS := 100
const TIMEOUT_SECONDS := 30.0
const HARNESS_SCRIPT := preload("res://tests/AdventureCampaignE2EHarness.cs")

var _failed := false
var _save_manager: Node
var _harness: RefCounted
var _main_avatar_ids: PackedStringArray


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	_save_manager = await _configure_empty_profile()
	_harness = HARNESS_SCRIPT.new() as RefCounted
	if _failed or _save_manager == null or _harness == null:
		_fail("Adventure campaign E2E dependencies are unavailable")
		return

	_main_avatar_ids = PackedStringArray(_harness.call("GetMainAvatarIds"))
	if _main_avatar_ids.size() != 12:
		_fail("Expected exactly twelve main campaign stages")
		return
	if not _assert_stage_locking(0):
		return

	for tutorial_id in TUTORIAL_IDS:
		if not await _complete_tutorial(String(tutorial_id)):
			return
		var tutorial_profile := _load_profile()
		if int(tutorial_profile.get("highest_completed_main_adventure_stage", -1)) != 0 \
				or not String(tutorial_profile.get("pending_adventure_run_json", "")).is_empty():
			_fail("Tutorial %s changed main campaign progress or created a run" % tutorial_id)
			return

	var board := await _start_main_campaign_from_menu()
	if _failed or board == null:
		return
	if not await _select_visible_upgrade(board, "run-start"):
		return
	var persisted_run := String(_load_profile().get("pending_adventure_run_json", ""))
	if persisted_run.is_empty():
		_fail("Run-start upgrade did not persist the active run")
		return

	if not await _leave_board_through_pause(board):
		return
	var menu := await _wait_for_scene(MENU_SCENE_PATH)
	if menu == null:
		return
	if not await _open_resume_dialog(menu):
		return
	if not bool(menu.call("DebugSelectAdventureRunResumeContinue")):
		_fail("Could not continue the persisted run")
		return
	board = await _wait_for_board(_main_avatar_ids[0])
	if board == null or not await _wait_for_actionable_state(board):
		return
	if String(_load_profile().get("pending_adventure_run_json", "")) != persisted_run:
		_fail("Continuing the run changed its payload before gameplay resumed")
		return

	if not await _exercise_defeat_and_revive(board):
		return
	board = await _wait_for_board(_main_avatar_ids[0])
	if board == null:
		return
	if not await _select_visible_upgrade(board, "restart run-start"):
		return

	for stage_number in range(1, _main_avatar_ids.size() + 1):
		if String(board.get("adventure_enemy_avatar_id")) != _main_avatar_ids[stage_number - 1]:
			_fail("Stage %d opened the wrong enemy" % stage_number)
			return
		if not await _force_main_victory(board, stage_number):
			return
		if stage_number < _main_avatar_ids.size():
			if not bool(board.call("DebugPressWinnerPrimaryButton")):
				_fail("Stage %d next-stage action was unavailable" % stage_number)
				return
			board = await _wait_for_board(_main_avatar_ids[stage_number])
			if board == null or not await _wait_for_actionable_state(board):
				return

	if not await _verify_completed_profile():
		return

	board.queue_free()
	board = null
	current_scene = null
	for _frame in 8:
		await process_frame
	await _dispose_audio_before_quit()
	var cleanup_script := load("res://tests/ManagedTestCleanupHarness.cs") as Script
	var cleanup_harness: RefCounted = cleanup_script.new() as RefCounted if cleanup_script != null else null
	if cleanup_harness != null:
		cleanup_harness.call("DrainFinalizers")
	cleanup_harness = null
	cleanup_script = null
	_harness = null
	for _frame in 3:
		await process_frame
	print("ADVENTURE_CAMPAIGN_E2E_SMOKE_OK")
	quit(0)


func _configure_empty_profile() -> Node:
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return null
	save_manager.call("ensure_slot_for_file_base", PROFILE_SLOT, String(PROFILE_SLOT))
	for key in [&"migration.settings_cfg.v1", &"migration.progress_profile.v1", &"migration.progress_profile.v2"]:
		save_manager.call("set_value", key, true)
	save_manager.call("set_value", &"progress.active_profile_slot", String(PROFILE_SLOT))
	if int(save_manager.call("persist")) != OK:
		_fail("Could not activate the isolated E2E profile")
		return null
	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 3,
		"highest_completed_main_adventure_stage": 0,
		"completed_adventure_tutorial_ids_csv": "",
		"selected_avatar_id": "fox",
		"player_name": "Campaign E2E",
		"pending_avatar_unlock_queue": "",
		"pending_adventure_run_json": "",
		"unlocked_skill_codex_upgrade_ids_csv": "",
		"playtime_seconds": 0.0,
		"created_unix_time": now,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", PROFILE_SLOT, profile)) != OK:
		_fail("Could not create the isolated empty profile")
		return null
	return save_manager


func _complete_tutorial(tutorial_id: String) -> bool:
	var menu := await _instantiate_menu(tutorial_id)
	if menu == null:
		return false
	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null or start_button.disabled:
		_fail("Tutorial %s did not expose Start" % tutorial_id)
		return false
	start_button.emit_signal("pressed")
	var board := await _wait_for_board(tutorial_id)
	if board == null:
		return false
	if not bool(board.get("adventure_tutorial_mode")):
		_fail("Tutorial %s did not use tutorial mode" % tutorial_id)
		return false
	if not bool(board.call("DebugAddScoreAndCheckWinner", MAIN_PLAYER_INDEX, FORCED_VICTORY_POINTS)):
		_fail("Could not accelerate tutorial %s victory" % tutorial_id)
		return false
	if not await _wait_for_winner_banner(board):
		return false
	if not _profile_contains_tutorial(tutorial_id):
		_fail("Tutorial %s victory did not persist through production completion" % tutorial_id)
		return false
	if not bool(board.call("DebugPressWinnerReturnButton")):
		_fail("Tutorial %s return action was unavailable" % tutorial_id)
		return false
	return await _wait_for_scene(MENU_SCENE_PATH) != null


func _start_main_campaign_from_menu() -> Node:
	var menu := current_scene
	if menu == null or not is_instance_valid(menu) or menu.scene_file_path != MENU_SCENE_PATH:
		menu = await _instantiate_menu(_main_avatar_ids[0])
	else:
		var main_mode_button := menu.get_node_or_null("%deep_tide_throneAdventureEntry/Button") as Button
		if main_mode_button == null:
			_fail("Adventure menu is missing the main campaign entry")
			return null
		main_mode_button.emit_signal("pressed")
		await _wait_frames(3)
	var stage_two_button := menu.get_node_or_null("%LevelList/crabLevelEntry/Button") as Button
	if stage_two_button == null or not stage_two_button.disabled:
		_fail("Clean main campaign exposed stage two before stage one completion")
		return null
	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null or start_button.disabled:
		_fail("Main stage one did not expose Start")
		return null
	start_button.emit_signal("pressed")
	var board := await _wait_for_board(_main_avatar_ids[0])
	if board == null:
		return null
	if not await _wait_for_upgrade_selection(board):
		return null
	return board


func _exercise_defeat_and_revive(board: Node) -> bool:
	var reward_count := int(_harness.call("GetUnlockedSkillCount", String(
			_load_profile().get("unlocked_skill_codex_upgrade_ids_csv", ""))))
	if not bool(board.call("DebugAddScoreAndCheckWinner", ENEMY_PLAYER_INDEX, FORCED_VICTORY_POINTS)) \
			or not await _wait_for_winner_banner(board):
		_fail("Could not settle the first defeat")
		return false
	var first_defeat := board.call("DebugGetAdventureRunStateSnapshot") as Dictionary
	if String(first_defeat.get("defeat_resolution", "")) != "RetryCurrentStage" \
			or not bool(first_defeat.get("has_used_revive", false)):
		_fail("First defeat did not consume the one-time revive")
		return false
	if not bool(board.call("DebugPressWinnerPrimaryButton")):
		_fail("First defeat retry action was unavailable")
		return false
	if not await _wait_for_actionable_state(board):
		return false
	if not bool(board.call("DebugAddScoreAndCheckWinner", ENEMY_PLAYER_INDEX, FORCED_VICTORY_POINTS)) \
			or not await _wait_for_winner_banner(board):
		_fail("Could not settle the second defeat")
		return false
	var second_defeat := board.call("DebugGetAdventureRunStateSnapshot") as Dictionary
	if String(second_defeat.get("defeat_resolution", "")) != "RestartJourney":
		_fail("Second defeat did not require a journey restart")
		return false
	var rewards_after_defeats := int(_harness.call("GetUnlockedSkillCount", String(
			_load_profile().get("unlocked_skill_codex_upgrade_ids_csv", ""))))
	if rewards_after_defeats != reward_count:
		_fail("Defeat path duplicated skill rewards")
		return false
	if not bool(board.call("DebugPressWinnerPrimaryButton")):
		_fail("Second defeat restart action was unavailable")
		return false
	var restarted_board := await _wait_for_board(_main_avatar_ids[0], board)
	return restarted_board != null and await _wait_for_upgrade_selection(restarted_board)


func _force_main_victory(board: Node, stage_number: int) -> bool:
	if not await _wait_for_actionable_state(board):
		return false
	if not bool(board.call("DebugAddScoreAndCheckWinner", MAIN_PLAYER_INDEX, FORCED_VICTORY_POINTS)):
		_fail("Could not accelerate stage %d victory" % stage_number)
		return false
	if not await _wait_for_upgrade_selection(board):
		return false
	if not await _select_visible_upgrade(board, "stage %d victory" % stage_number):
		return false
	if not await _wait_for_winner_banner(board):
		return false
	var profile := _load_profile()
	if int(profile.get("highest_completed_main_adventure_stage", -1)) != stage_number:
		_fail("Stage %d victory did not advance progress by exactly one" % stage_number)
		return false
	if stage_number < _main_avatar_ids.size():
		if not _assert_stage_locking(stage_number):
			return false
	else:
		if not String(profile.get("pending_adventure_run_json", "")).is_empty():
			_fail("Final boss victory left a pending run")
			return false
	return true


func _verify_completed_profile() -> bool:
	var profile := _load_profile()
	var unlocked_csv := String(profile.get("unlocked_skill_codex_upgrade_ids_csv", ""))
	if int(profile.get("highest_completed_main_adventure_stage", -1)) != 12 \
			or not String(profile.get("pending_adventure_run_json", "")).is_empty():
		_fail("Completed profile did not retain final progress or clear the run")
		return false
	if int(_harness.call("GetUnlockedSkillCount", unlocked_csv)) < 2 \
			or bool(_harness.call("HasDuplicateUnlockedSkills", unlocked_csv)):
		_fail("Skill codex unlock persistence is missing or duplicated")
		return false
	for avatar_id in _main_avatar_ids:
		if not bool(_harness.call("IsAvatarUnlocked", avatar_id)):
			_fail("Completed campaign did not unlock avatar %s" % avatar_id)
			return false
	var consumed: Array[String] = []
	while true:
		var avatar_id := String(_harness.call("ConsumePendingAvatarUnlock"))
		if avatar_id.is_empty():
			break
		if avatar_id in consumed:
			_fail("Avatar unlock queue duplicated %s" % avatar_id)
			return false
		consumed.append(avatar_id)
	if consumed.size() != _main_avatar_ids.size():
		_fail("Expected 12 avatar reward entries, consumed %d" % consumed.size())
		return false
	var reloaded := _load_profile()
	if int(reloaded.get("highest_completed_main_adventure_stage", -1)) != 12 \
			or not String(reloaded.get("pending_adventure_run_json", "")).is_empty() \
			or String(reloaded.get("unlocked_skill_codex_upgrade_ids_csv", "")) != unlocked_csv \
			or not String(reloaded.get("pending_avatar_unlock_queue", "")).is_empty():
		_fail("Reloaded completed profile was not stable")
		return false
	return true


func _leave_board_through_pause(board: Node) -> bool:
	var escape := InputEventKey.new()
	escape.keycode = KEY_ESCAPE
	escape.pressed = true
	board._input(escape)
	await _wait_frames(2)
	var return_button := board.find_child("ReturnToMainMenuButton", true, false) as Button
	if return_button == null or return_button.disabled:
		_fail("Adventure pause menu did not expose return-to-adventure")
		return false
	return_button.emit_signal("pressed")
	return true


func _instantiate_menu(selected_avatar_id: String) -> Node:
	var packed_scene := load(MENU_SCENE_PATH) as PackedScene
	var menu := packed_scene.instantiate() if packed_scene != null else null
	if menu == null:
		_fail("Could not instantiate adventure menu")
		return null
	menu.set("selected_avatar_id", selected_avatar_id)
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(4)
	return menu


func _open_resume_dialog(menu: Node) -> bool:
	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null or start_button.disabled:
		_fail("Saved run menu did not expose Start")
		return false
	start_button.emit_signal("pressed")
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		if is_instance_valid(menu) and bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
			return true
	_fail("Timed out waiting for the run resume dialog")
	return false


func _select_visible_upgrade(board: Node, context: String) -> bool:
	if not await _wait_for_upgrade_selection(board):
		return false
	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Could not select %s upgrade" % context)
		return false
	return await _wait_for_actionable_or_winner(board)


func _wait_for_upgrade_selection(board: Node) -> bool:
	return await _wait_for_board_predicate(board, "DebugIsAdventureUpgradeSelectionVisible", true, "upgrade selection")


func _wait_for_winner_banner(board: Node) -> bool:
	return await _wait_for_board_predicate(board, "DebugIsWinnerBannerVisible", true, "winner banner")


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board was freed before it became actionable")
			return false
		if not bool(board.call("DebugIsAdventureUpgradeSelectionVisible")) \
				and String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for actionable board state")
	return false


func _wait_for_actionable_or_winner(board: Node) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board was freed while resolving an upgrade")
			return false
		if bool(board.call("DebugIsWinnerBannerVisible")) \
				or (not bool(board.call("DebugIsAdventureUpgradeSelectionVisible")) \
				and String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction"):
			return true
	_fail("Timed out resolving an upgrade")
	return false


func _wait_for_board(expected_avatar_id: String, excluded_board: Node = null) -> Node:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var scene := current_scene
		if scene != null and is_instance_valid(scene) \
				and scene != excluded_board \
				and scene.has_method("DebugAddScoreAndCheckWinner") \
				and String(scene.get("adventure_enemy_avatar_id")) == expected_avatar_id:
			return scene
	_fail("Timed out waiting for board %s" % expected_avatar_id)
	return null


func _wait_for_scene(scene_path: String) -> Node:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var scene := current_scene
		if scene != null and is_instance_valid(scene) and scene.scene_file_path == scene_path:
			return scene
	_fail("Timed out waiting for scene %s" % scene_path)
	return null


func _wait_for_board_predicate(board: Node, method: String, expected: bool, label: String) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board was freed while waiting for %s" % label)
			return false
		if bool(board.call(method)) == expected:
			return true
	_fail("Timed out waiting for %s" % label)
	return false


func _assert_stage_locking(highest_completed: int) -> bool:
	for stage_number in range(1, _main_avatar_ids.size() + 1):
		var expected_unlocked := stage_number <= mini(highest_completed + 1, _main_avatar_ids.size())
		if bool(_harness.call("IsMainStageUnlocked", stage_number)) != expected_unlocked:
			_fail("Stage %d lock state is invalid after completing %d" % [stage_number, highest_completed])
			return false
	return true


func _profile_contains_tutorial(tutorial_id: String) -> bool:
	for completed_id in String(_load_profile().get("completed_adventure_tutorial_ids_csv", "")).split("|", false):
		if completed_id.strip_edges() == tutorial_id:
			return true
	return false


func _load_profile() -> Dictionary:
	return _save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _deadline() -> float:
	return Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS


func _before_deadline(deadline: float) -> bool:
	return Time.get_ticks_msec() / 1000.0 < deadline


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
	await _wait_frames(4)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_CAMPAIGN_E2E_SMOKE_FAIL %s" % message)
	quit(1)
