extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const BASIC_SCENE_PATH := "res://scenes/adventure_tutorial_game_board_scene.tscn"
const BOMB_SCENE_PATH := "res://scenes/adventure_item_tutorial_game_board_scene.tscn"
const HAND_AREA_PATH := "BoardCanvas/LayoutAreas/Player1HandArea"
const PROFILE_SLOT := &"profile_0"
const MAIN_PLAYER_INDEX := 0
const ENEMY_PLAYER_INDEX := 1
const BASIC_TUTORIAL_ID := "fish"
const BOMB_TUTORIAL_ID := "lanternfish"
const BOMB_ITEM_ID := "bomb"
const TIMEOUT_SECONDS := 45.0

var _failed := false
var _save_manager: Node


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	_save_manager = await _configure_profile()
	if _failed or _save_manager == null:
		return

	if not await _run_basic_capture_tutorial():
		return
	if not await _run_bomb_item_tutorial():
		return

	print("ADVENTURE_TUTORIAL_COMPLETION_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _configure_profile() -> Node:
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return null

	save_manager.call("ensure_slot_for_file_base", PROFILE_SLOT, String(PROFILE_SLOT))
	save_manager.call("set_value", &"migration.settings_cfg.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v2", true)
	save_manager.call("set_value", &"progress.active_profile_slot", String(PROFILE_SLOT))
	if int(save_manager.call("persist")) != OK:
		_fail("Failed to persist the tutorial completion smoke profile")
		return null

	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 3,
		"highest_completed_main_adventure_stage": 0,
		"completed_adventure_tutorial_ids_csv": "",
		"selected_avatar_id": "fox",
		"player_name": "Tutorial Completion Smoke",
		"pending_avatar_unlock_queue": "",
		"pending_adventure_run_json": "",
		"unlocked_skill_codex_upgrade_ids_csv": "",
		"playtime_seconds": 0.0,
		"created_unix_time": now,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", PROFILE_SLOT, profile)) != OK:
		_fail("Failed to seed the tutorial completion smoke profile")
		return null
	return save_manager


func _run_basic_capture_tutorial() -> bool:
	var board := await _instantiate_tutorial_board(BASIC_SCENE_PATH, BASIC_TUTORIAL_ID)
	if _failed or board == null:
		return false
	if not _assert_positive_initial_health(board, BASIC_TUTORIAL_ID):
		return false
	if not await _advance_continue_callouts(board, 6):
		return false
	if not await _wait_for_action_callout(board, BASIC_TUTORIAL_ID):
		return false
	if not _assert_base_hand_codes(board, ["Hearts:7", "Hearts:2"], BASIC_TUTORIAL_ID):
		return false

	var hearts_seven := await _wait_for_hand_card(board, "Hearts:7")
	if hearts_seven == null:
		_fail("Basic tutorial did not expose the highlighted Hearts 7")
		return false
	if not await _wait_for_highlighted_hand_entry(board, "Hearts:7"):
		return false
	if not await _play_highlighted_base_card(board, "Hearts:7"):
		return false
	if not await _wait_for_next_highlighted_card(board, "Hearts:2", "basic Hearts 7 capture"):
		return false
	if String(board.call("DebugGetMatchOutcomeKindName")) != "InProgress" \
			or int(board.call("DebugGetWinnerPlayerIndex")) != -1:
		_fail("Basic tutorial declared a winner after Hearts 7 instead of remaining in progress")
		return false

	var hearts_two := await _wait_for_hand_card(board, "Hearts:2")
	if hearts_two == null:
		_fail("Basic tutorial did not expose the highlighted Hearts 2")
		return false
	if not await _wait_for_highlighted_hand_entry(board, "Hearts:2"):
		return false
	if not await _play_highlighted_base_card(board, "Hearts:2"):
		return false
	if not await _wait_for_tutorial_victory(board, BASIC_TUTORIAL_ID):
		return false

	board.queue_free()
	await _wait_for_board_release()
	return true


func _run_bomb_item_tutorial() -> bool:
	var board := await _instantiate_tutorial_board(BOMB_SCENE_PATH, BOMB_TUTORIAL_ID)
	if _failed or board == null:
		return false
	if not _assert_positive_initial_health(board, BOMB_TUTORIAL_ID):
		return false
	if not await _advance_continue_callouts(board, 1):
		return false
	if not await _wait_for_action_callout(board, BOMB_TUTORIAL_ID):
		return false
	if not _assert_base_hand_codes(board, ["Hearts:4"], BOMB_TUTORIAL_ID):
		return false
	var hearts_four := await _wait_for_hand_card(board, "Hearts:4")
	if hearts_four == null:
		_fail("Bomb tutorial did not expose the highlighted Hearts 4")
		return false
	if not await _wait_for_highlighted_hand_entry(board, "Hearts:4"):
		return false
	if not await _play_highlighted_base_card(board, "Hearts:4"):
		return false
	if not await _wait_for_bomb_reward_delivery(board):
		return false
	if String(board.call("DebugGetMatchOutcomeKindName")) != "InProgress" \
			or int(board.call("DebugGetWinnerPlayerIndex")) != -1:
		_fail("Bomb tutorial declared a winner while collecting the forced reward")
		return false

	var bomb_card := await _wait_for_item_card(board)
	if bomb_card == null:
		_fail("Bomb tutorial did not expose the highlighted bomb item")
		return false
	if not await _wait_for_highlighted_item_entry(board):
		return false
	if not await _play_highlighted_item_entry(board):
		return false
	if not await _wait_for_tutorial_victory(board, BOMB_TUTORIAL_ID):
		return false

	board.queue_free()
	await _wait_for_board_release()
	return true


func _instantiate_tutorial_board(scene_path: String, tutorial_id: String) -> Node:
	var packed_scene := load(scene_path) as PackedScene
	if packed_scene == null:
		_fail("Missing tutorial board scene: %s" % scene_path)
		return null
	var board := GameBoardSmokeFactory.create_tutorial(packed_scene, tutorial_id)
	if board == null:
		_fail("Could not instantiate tutorial board: %s" % scene_path)
		return null
	root.add_child(board)
	if not _require_board_methods(board):
		return null

	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var overlay := board.get_node_or_null("AdventureTutorialOverlay") as Control
		if overlay != null and overlay.visible:
			return board
	_fail("Tutorial board did not show its opening callout for %s" % tutorial_id)
	return null


func _require_board_methods(board: Node) -> bool:
	for method in [
		"DebugGetPlayerScoreValue",
		"DebugGetWinnerPlayerIndex",
		"DebugGetMatchOutcomeKindName",
		"DebugGetHandCardCode",
		"DebugGetTotalHandCardCountForPlayer",
		"DebugGetTotalItemHandCardCountForPlayer",
		"DebugGetLastQuestionRewardItemCardId",
		"DebugGetBoardTrackedTaskCount",
	]:
		if not board.has_method(method):
			_fail("Tutorial board is missing debug read API %s" % method)
			return false
	return true


func _assert_positive_initial_health(board: Node, tutorial_id: String) -> bool:
	var main_health := int(board.call("DebugGetPlayerScoreValue", MAIN_PLAYER_INDEX))
	var enemy_health := int(board.call("DebugGetPlayerScoreValue", ENEMY_PLAYER_INDEX))
	if main_health <= 0 or enemy_health <= 0:
		_fail("Tutorial %s initialized non-positive health: main=%d enemy=%d" % [
			tutorial_id,
			main_health,
			enemy_health,
		])
		return false
	return true


func _assert_base_hand_codes(board: Node, expected_codes: Array, tutorial_id: String) -> bool:
	var item_count := int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	var total_count := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	var base_card_count := total_count - item_count
	var actual_codes: Array[String] = []
	for hand_index in range(maxi(0, base_card_count)):
		actual_codes.append(String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, hand_index)))
	actual_codes.sort()
	var normalized_expected: Array[String] = []
	for code in expected_codes:
		normalized_expected.append(String(code))
	normalized_expected.sort()
	if actual_codes != normalized_expected:
		_fail("Tutorial %s scripted hand changed: expected=%s actual=%s items=%d" % [
			tutorial_id,
			str(normalized_expected),
			str(actual_codes),
			item_count,
		])
		return false
	return true


func _advance_continue_callouts(board: Node, count: int) -> bool:
	for callout_index in range(count):
		var button := await _wait_for_enabled_continue_button(board)
		if button == null:
			_fail("Tutorial Continue button did not unlock at callout %d" % (callout_index + 1))
			return false
		button.emit_signal("pressed")
		await process_frame
		await process_frame
	return true


func _wait_for_enabled_continue_button(board: Node) -> Button:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var button := board.get_node_or_null(
			"AdventureTutorialOverlay/AdventureTutorialCallout/AdventureTutorialContinueButton") as Button
		if button != null and button.visible and not button.disabled:
			return button
	return null


func _wait_for_action_callout(board: Node, tutorial_id: String) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var overlay := board.get_node_or_null("AdventureTutorialOverlay") as Control
		var blocker := board.get_node_or_null(
			"AdventureTutorialOverlay/AdventureTutorialInputBlocker") as ColorRect
		var button := board.get_node_or_null(
			"AdventureTutorialOverlay/AdventureTutorialCallout/AdventureTutorialContinueButton") as Button
		if overlay != null and overlay.visible \
				and blocker != null and not blocker.visible \
				and button != null and not button.visible:
			return true
	_fail("Tutorial %s did not enter its card action callout" % tutorial_id)
	return false


func _wait_for_hand_card(board: Node, card_code: String) -> Control:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var card_slot := _find_hand_card(board, card_code)
		if card_slot != null:
			return card_slot
	return null


func _find_hand_card(board: Node, card_code: String) -> Control:
	var hand_area := board.get_node_or_null(HAND_AREA_PATH)
	if hand_area == null:
		return null
	var display_index := 0
	while true:
		var card_slot := hand_area.get_node_or_null("HandCard_%d" % display_index) as Control
		if card_slot == null:
			return null
		if card_slot.visible \
				and String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, display_index)) == card_code:
			return card_slot
		display_index += 1
	return null


func _wait_for_highlighted_hand_entry(board: Node, card_code: String) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var card_slot := _find_hand_card(board, card_code)
		if card_slot != null and card_slot.get_node_or_null("MagnetSelectionPulse") != null:
			return true
	_fail("Tutorial card %s never became the highlighted playable entry" % card_code)
	return false


func _wait_for_highlighted_item_entry(board: Node) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var card_slot := await _wait_for_item_card(board)
		if card_slot != null and card_slot.get_node_or_null("MagnetSelectionPulse") != null:
			return true
	_fail("Bomb tutorial item never became the highlighted playable entry")
	return false


func _wait_for_next_highlighted_card(board: Node, card_code: String, label: String) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var card_slot := _find_hand_card(board, card_code)
		if card_slot != null \
				and card_slot.get_node_or_null("MagnetSelectionPulse") != null \
				and String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
		if String(board.call("DebugGetMatchOutcomeKindName")) != "InProgress":
			break
	var hand_codes: Array[String] = []
	var base_card_count := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX)) \
		- int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	for hand_index in range(maxi(0, base_card_count)):
		hand_codes.append(String(board.call("DebugGetHandCardCode", MAIN_PLAYER_INDEX, hand_index)))
	_fail("Timed out waiting for %s to highlight %s; phase=%s outcome=%s winner=%d tasks=%d hand=%s" % [
		label,
		card_code,
		String(board.call("DebugGetGameLoopPhaseName")),
		String(board.call("DebugGetMatchOutcomeKindName")),
		int(board.call("DebugGetWinnerPlayerIndex")),
		int(board.call("DebugGetBoardTrackedTaskCount")),
		str(hand_codes),
	])
	return false


func _wait_for_bomb_reward_delivery(board: Node) -> bool:
	var deadline := _deadline()
	var saw_logical_reward := false
	var saw_transform := false
	while _before_deadline(deadline):
		await process_frame
		var reward_id := String(board.call("DebugGetLastQuestionRewardItemCardId"))
		var item_count := int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX))
		if reward_id == BOMB_ITEM_ID and item_count == 1:
			saw_logical_reward = true
			var transform := board.get_node_or_null("QuestionCardRewardTransform") as Control
			if transform != null:
				saw_transform = true
			if not saw_transform or transform != null:
				var early_slot := _get_first_item_card_slot(board)
				if early_slot != null and early_slot.visible:
					_fail("Bomb reward appeared in hand before the question card finished transforming")
					return false
			elif String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
				var delivered_slot := _get_first_item_card_slot(board)
				if delivered_slot == null or not delivered_slot.visible:
					_fail("Bomb reward stayed hidden after the question card returned to hand")
					return false
				return true
		if String(board.call("DebugGetMatchOutcomeKindName")) != "InProgress":
			break
	_fail("Bomb tutorial reward delivery did not complete; reward=%s items=%d logical=%s transform=%s outcome=%s winner=%d" % [
		String(board.call("DebugGetLastQuestionRewardItemCardId")),
		int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX)),
		str(saw_logical_reward),
		str(saw_transform),
		String(board.call("DebugGetMatchOutcomeKindName")),
		int(board.call("DebugGetWinnerPlayerIndex")),
	])
	return false


func _wait_for_item_card(board: Node) -> Control:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var card_slot := _get_first_item_card_slot(board)
		if card_slot != null and card_slot.visible:
			return card_slot
	return null


func _get_first_item_card_slot(board: Node) -> Control:
	var item_count := int(board.call("DebugGetTotalItemHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	var total_count := int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX))
	if item_count <= 0 or total_count < item_count:
		return null
	var item_display_index := total_count - item_count
	return board.get_node_or_null(
		"%s/HandCard_%d" % [HAND_AREA_PATH, item_display_index]) as Control


func _wait_for_tutorial_victory(board: Node, tutorial_id: String) -> bool:
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var outcome := String(board.call("DebugGetMatchOutcomeKindName"))
		var winner := int(board.call("DebugGetWinnerPlayerIndex"))
		if outcome == "Winner":
			if winner != MAIN_PLAYER_INDEX:
				_fail("Tutorial %s ended with enemy winner %d" % [tutorial_id, winner])
				return false
			if _active_profile_contains_tutorial(tutorial_id) \
					and bool(board.call("DebugIsWinnerBannerVisible")):
				return _assert_tutorial_result_actions(board, tutorial_id)
		elif outcome != "InProgress":
			_fail("Tutorial %s ended with unexpected outcome %s" % [tutorial_id, outcome])
			return false
	_fail("Tutorial %s did not complete; outcome=%s winner=%d completed=%s" % [
		tutorial_id,
		String(board.call("DebugGetMatchOutcomeKindName")),
		int(board.call("DebugGetWinnerPlayerIndex")),
		String((_save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary).get(
			"completed_adventure_tutorial_ids_csv", "")),
	])
	return false


func _assert_tutorial_result_actions(board: Node, tutorial_id: String) -> bool:
	var result_banner := board.get_node_or_null("WinnerBannerRoot") as Control
	var primary_frame := board.get_node_or_null(
		"WinnerBannerRoot/WinnerCenter/WinnerBannerPanel/WinnerButtonRow/PrimaryFrame") as Control
	var primary_button := board.get_node_or_null(
		"WinnerBannerRoot/WinnerCenter/WinnerBannerPanel/WinnerButtonRow/PrimaryFrame/PrimaryButton") as Button
	var return_frame := board.get_node_or_null(
		"WinnerBannerRoot/WinnerCenter/WinnerBannerPanel/WinnerButtonRow/ReturnFrame") as Control
	var return_button := board.get_node_or_null(
		"WinnerBannerRoot/WinnerCenter/WinnerBannerPanel/WinnerButtonRow/ReturnFrame/ReturnButton") as Button
	if result_banner == null or primary_frame == null or primary_button == null \
			or return_frame == null or return_button == null:
		_fail("Tutorial %s result action scene contract is incomplete" % tutorial_id)
		return false
	if not result_banner.visible:
		_fail("Tutorial %s completed without showing its result banner" % tutorial_id)
		return false
	if primary_frame.visible or primary_button.is_visible_in_tree() or not primary_button.disabled:
		_fail("Tutorial %s still exposed the Play Again action" % tutorial_id)
		return false
	if not return_frame.visible or not return_button.is_visible_in_tree() or return_button.disabled:
		_fail("Tutorial %s did not expose the return-to-stages action" % tutorial_id)
		return false
	if return_button.text != tr("GAME_RETURN_ADVENTURE"):
		_fail("Tutorial %s return action text changed: %s" % [tutorial_id, return_button.text])
		return false
	return true


func _active_profile_contains_tutorial(tutorial_id: String) -> bool:
	var profile := _save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary
	var completed_csv := String(profile.get("completed_adventure_tutorial_ids_csv", ""))
	for completed_id in completed_csv.split("|", false):
		if completed_id.strip_edges() == tutorial_id:
			return true
	return false


func _select_highlighted_hand_entry(board: Node) -> bool:
	_push_board_action(board, &"hand_next")
	var deadline := _deadline()
	while _before_deadline(deadline):
		await process_frame
		var hand_area := board.get_node_or_null(HAND_AREA_PATH)
		if hand_area != null and hand_area.find_child("InputSelectionFrame", true, false) != null:
			return true
	_fail("Tutorial highlighted entry could not be selected through hand navigation")
	return false


func _play_highlighted_base_card(board: Node, card_code: String) -> bool:
	if not await _wait_for_highlighted_hand_entry(board, card_code):
		return false
	var card_slot := _find_hand_card(board, card_code)
	var deadline := _deadline()
	while card_slot != null and card_slot.mouse_filter != Control.MOUSE_FILTER_STOP and _before_deadline(deadline):
		await process_frame
		card_slot = _find_hand_card(board, card_code)
	if card_slot == null or card_slot.mouse_filter != Control.MOUSE_FILTER_STOP:
		_fail("Tutorial card %s did not become a clickable UI control" % card_code)
		return false
	var tracked_tasks_before := int(board.call("DebugGetBoardTrackedTaskCount"))
	var press := InputEventMouseButton.new()
	press.position = card_slot.size / 2.0
	press.global_position = card_slot.get_global_rect().get_center()
	press.button_index = MOUSE_BUTTON_LEFT
	press.button_mask = MOUSE_BUTTON_MASK_LEFT
	press.pressed = true
	card_slot.gui_input.emit(press)
	await process_frame
	var tracked_tasks_after := int(board.call("DebugGetBoardTrackedTaskCount"))
	if tracked_tasks_after <= tracked_tasks_before:
		var hand_area := board.get_node_or_null(HAND_AREA_PATH)
		var has_selection := hand_area != null \
			and hand_area.find_child("InputSelectionFrame", true, false) != null
		_fail("Tutorial card %s confirmation started no board task; phase=%s selected=%s tasks=%d->%d" % [
			card_code,
			String(board.call("DebugGetGameLoopPhaseName")),
			str(has_selection),
			tracked_tasks_before,
			tracked_tasks_after,
		])
		return false
	return true


func _play_highlighted_item_entry(board: Node) -> bool:
	if not await _wait_for_highlighted_item_entry(board):
		return false
	var card_slot := await _wait_for_item_card(board)
	var deadline := _deadline()
	while card_slot != null and card_slot.mouse_filter != Control.MOUSE_FILTER_STOP and _before_deadline(deadline):
		await process_frame
		card_slot = await _wait_for_item_card(board)
	if card_slot == null or card_slot.mouse_filter != Control.MOUSE_FILTER_STOP:
		_fail("Bomb tutorial item did not become a clickable UI control")
		return false
	var tracked_tasks_before := int(board.call("DebugGetBoardTrackedTaskCount"))
	var press := InputEventMouseButton.new()
	press.position = card_slot.size / 2.0
	press.global_position = card_slot.get_global_rect().get_center()
	press.button_index = MOUSE_BUTTON_LEFT
	press.button_mask = MOUSE_BUTTON_MASK_LEFT
	press.pressed = true
	card_slot.gui_input.emit(press)
	await process_frame
	var tracked_tasks_after := int(board.call("DebugGetBoardTrackedTaskCount"))
	if tracked_tasks_after <= tracked_tasks_before:
		_fail("Bomb tutorial mouse click started no board task; phase=%s tasks=%d->%d" % [
			String(board.call("DebugGetGameLoopPhaseName")),
			tracked_tasks_before,
			tracked_tasks_after,
		])
		return false
	return true


func _push_board_action(board: Node, action: StringName) -> void:
	var press := InputEventAction.new()
	press.action = action
	press.pressed = true
	press.strength = 1.0
	board._unhandled_input(press)
	var release := press.duplicate() as InputEventAction
	release.pressed = false
	release.strength = 0.0
	board._unhandled_input(release)


func _wait_for_board_release() -> void:
	for _frame in range(8):
		await process_frame


func _deadline() -> float:
	return Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS


func _before_deadline(deadline: float) -> bool:
	return Time.get_ticks_msec() / 1000.0 < deadline


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_TUTORIAL_COMPLETION_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(32):
		await process_frame
