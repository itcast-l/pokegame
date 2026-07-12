extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load game board scene")
		return

	var board := packed_scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	await process_frame

	if not board.has_method("DebugStartAdventureSuitLegendaryProbe"):
		_fail("Missing four-suit legendary debug probe")
		return
	if not await _wait_for_upgrade_selection(board):
		return
	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to dismiss opening upgrade selection")
		return
	if not await _wait_for_main_action(board):
		return

	var result := {}
	if not bool(board.call("DebugStartAdventureSuitLegendaryProbe", result)):
		_fail("Failed to start four-suit legendary probe")
		return
	if not await _wait_for_result(result):
		return
	if not String(result.get("error", "")).is_empty():
		_fail("Four-suit legendary probe error: %s" % str(result["error"]))
		return

	_assert_value(result, "hearts_x", 2)
	_assert_value(result, "diamonds_x", 2)
	_assert_value(result, "spades_x", 2)
	_assert_value(result, "clubs_x", 4)
	_assert_value(result, "health_before", 10)
	_assert_value(result, "health_after_healing", 15)
	_assert_value(result, "health_after_zero_healing", 15)
	_assert_value(result, "player_capture_damage", 24)
	_assert_value(result, "all_gains_bonus_damage", 2)
	_assert_value(result, "all_gains_feedback_table_index", 1)
	_assert_value(result, "enemy_capture_damage", 0)
	_assert_value(result, "zero_base_player_capture_damage", 2)
	_assert_value(result, "acquisition_total_hand", 8)
	_assert_value(result, "acquisition_item_hand", 1)
	_assert_value(result, "acquisition_flourish_stacks", 2)
	_assert_value(result, "acquisition_deck_count", 0)
	_assert_value(result, "battle_start_total_hand", 8)
	_assert_value(result, "battle_start_item_hand", 1)
	_assert_value(result, "battle_start_flourish_stacks", 2)
	_assert_value(result, "battle_start_deck_count", 0)
	_assert_value(result, "empty_deck_total_hand", 7)
	_assert_value(result, "empty_deck_flourish_stacks", 2)
	_assert_value(result, "restored_deck_total_hand", 8)
	_assert_value(result, "restored_deck_flourish_stacks", 1)
	if _failed:
		return

	if not bool(result.get("healing_applied", false)):
		_fail("Expected Blood Tide healing to use the board health path")
		return
	if not bool(result.get("tooltip_visible", false)) \
		or not bool(result.get("tooltip_flourish_visible", false)):
		_fail("Expected the player status tooltip to show Flourish")
		return
	var flourish_text := String(result.get("tooltip_flourish_text", ""))
	if flourish_text.is_empty() or not flourish_text.contains("1"):
		_fail("Expected Flourish tooltip text to include the remaining stack: %s" % flourish_text)
		return
	var tooltip_height := float(result.get("tooltip_panel_height", 0.0))
	var content_minimum_height := float(result.get("tooltip_content_minimum_height", 0.0))
	if tooltip_height < 90.0 or content_minimum_height <= 0.0 \
		or content_minimum_height > tooltip_height:
		_fail("Flourish tooltip layout does not fit its panel: %s" % str(result))
		return

	print("ADVENTURE_UPGRADE_SUIT_LEGENDARIES_SMOKE_OK")
	quit(0)


func _assert_value(result: Dictionary, key: String, expected: int) -> void:
	if _failed:
		return
	var actual := int(result.get(key, -999999))
	if actual != expected:
		_fail("Expected %s=%d, got %d. Probe: %s" % [key, expected, actual, str(result)])


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true
	_fail("Timed out waiting for opening upgrade selection")
	return false


func _wait_for_main_action(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for main player action")
	return false


func _wait_for_result(result: Dictionary) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for four-suit legendary probe")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_SUIT_LEGENDARIES_SMOKE_FAIL %s" % message)
	quit(1)
