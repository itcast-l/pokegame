extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0

var _failed := false

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var without_upgrade := await _create_board()
	if without_upgrade == null:
		return
	if not await _wait_for_actionable_state(without_upgrade):
		return
	if not bool(without_upgrade.call("DebugConfigureAdventureCrownCanopyScenario")):
		_fail("Failed to configure Crown Canopy baseline")
		return
	var baseline_probe := without_upgrade.call("DebugGetCrownCanopyCaptureProbe") as Dictionary
	if not _assert_probe(baseline_probe, 0, 1, 1, "without upgrade"):
		return
	var enemy_score_before := int(without_upgrade.call("DebugGetPlayerScoreValue", 1))
	if not bool(without_upgrade.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play baseline Crown Canopy capture")
		return
	if not await _wait_for_settled_state(without_upgrade):
		return
	var baseline_damage := int(without_upgrade.call("DebugGetPlayerScoreValue", 1)) - enemy_score_before
	if baseline_damage != -2:
		_fail("Baseline capture did not apply exactly 2 damage (score delta %d)" % baseline_damage)
		return
	without_upgrade.queue_free()
	await process_frame

	var with_upgrade := await _create_board()
	if with_upgrade == null:
		return
	if not await _wait_for_actionable_state(with_upgrade):
		return
	if not bool(with_upgrade.call("DebugConfigureAdventureCrownCanopyScenario")) \
		or not bool(with_upgrade.call("DebugAcquireAdventureUpgrade", "clubs_crown_canopy")):
		_fail("Failed to configure Crown Canopy upgrade")
		return
	var upgrade_probe := with_upgrade.call("DebugGetCrownCanopyCaptureProbe") as Dictionary
	if not _assert_probe(upgrade_probe, 10, 3, 3, "with upgrade"):
		return
	enemy_score_before = int(with_upgrade.call("DebugGetPlayerScoreValue", 1))
	if not bool(with_upgrade.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play upgraded Crown Canopy capture")
		return
	if not await _wait_for_settled_state(with_upgrade):
		return
	if int(with_upgrade.call("DebugGetPlayerScoreValue", 1)) - enemy_score_before != -12:
		_fail("Crown Canopy capture did not apply exactly 12 damage")
		return

	if not bool(with_upgrade.call("DebugConfigureAdventureCrownCanopyScenario")) \
		or not bool(with_upgrade.call(
			"DebugAttachItemEffectToMainPlayerHandCard",
			0,
			"endpoint_crown"
		)):
		_fail("Failed to configure overlapping Crown Canopy and endpoint crown")
		return
	enemy_score_before = int(with_upgrade.call("DebugGetPlayerScoreValue", 1))
	if not bool(with_upgrade.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play overlapping Crown Canopy endpoint capture")
		return
	if not await _wait_for_settled_state(with_upgrade):
		return
	if int(with_upgrade.call("DebugGetPlayerScoreValue", 1)) - enemy_score_before != -12:
		_fail("Crown Canopy double-counted an endpoint already carrying endpoint_crown")
		return

	# collect_table is deliberately outside the normal base-card path.
	if not bool(with_upgrade.call("DebugConfigureAdventureCrownCanopyScenario")):
		_fail("Failed to reset direct collect_table scenario")
		return
	enemy_score_before = int(with_upgrade.call("DebugGetPlayerScoreValue", 1))
	var collect_result := {}
	if not bool(with_upgrade.call("DebugStartCollectTableCaptureOwnershipProbe", collect_result)):
		_fail("Failed to start direct collect_table probe")
		return
	if not await _wait_for_result(collect_result):
		return
	if String(collect_result.get("error", "")) != "" or not bool(collect_result.get("played", false)):
		_fail("direct collect_table did not complete: %s" % str(collect_result))
		return
	if int(with_upgrade.call("DebugGetPlayerScoreValue", 1)) - enemy_score_before != 0:
		_fail("direct collect_table gained Crown Canopy endpoint damage")
		return

	print("ADVENTURE_UPGRADE_CLUBS_CROWN_CANOPY_SMOKE_OK")
	quit()

func _create_board() -> Node:
	await process_frame
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load game board scene")
		return null
	var board := packed_scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	await process_frame
	for method in ["DebugConfigureAdventureCrownCanopyScenario", "DebugGetCrownCanopyCaptureProbe", "DebugAcquireAdventureUpgrade", "DebugAttachItemEffectToMainPlayerHandCard", "DebugGetPlayerScoreValue", "DebugStartCollectTableCaptureOwnershipProbe"]:
		if not board.has_method(method):
			_fail("Missing Crown Canopy debug hook: %s" % method)
			return null
	return board

func _assert_probe(probe: Dictionary, expected_endpoints: int, expected_points: int, expected_visuals: int, label: String) -> bool:
	if probe.is_empty() or int(probe.get("capture_start_index", -1)) != 0:
		_fail("%s probe did not retain captureStartIndex 0" % label)
		return false
	if int(probe.get("raw_point_count", 0)) != expected_points or int(probe.get("resolved_point_count", 0)) != expected_points:
		_fail("%s did not deduplicate overlapping endpoints: %s" % [label, str(probe)])
		return false
	if int(probe.get("endpoint_points", -1)) != expected_endpoints:
		_fail("%s endpoint points mismatch: %s" % [label, str(probe)])
		return false
	if int(probe.get("score_fill_visual_count", 0)) != expected_visuals:
		_fail("%s score-fill visual count mismatch: %s" % [label, str(probe)])
		return false
	return true

func _wait_for_actionable_state(board: Node) -> bool:
	return await _wait_for_phase(board, ["AwaitingMainPlayerAction"])

func _wait_for_settled_state(board: Node) -> bool:
	return await _wait_for_phase(board, ["AwaitingMainPlayerAction", "GameOver"])

func _wait_for_phase(board: Node, phases: Array) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly")
			return false
		if phases.has(String(board.call("DebugGetGameLoopPhaseName"))):
			return true
	_fail("Timed out waiting for board state %s" % str(phases))
	return false

func _wait_for_result(result: Dictionary) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for collect_table result")
	return false

func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_CLUBS_CROWN_CANOPY_SMOKE_FAIL %s" % message)
	quit(1)
