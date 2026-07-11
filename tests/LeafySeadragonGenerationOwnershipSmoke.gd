extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 25.0

var _failed := false

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	if not await _run_restart_during_phase("DebugHasLeafyBladeProjectilePhase"):
		return
	if not await _run_restart_during_phase("DebugHasLeafyBladeBombPhase"):
		return
	if not await _run_reference_replacement_during_projectiles():
		return
	if not await _run_trace_hunt_removal():
		return
	_assert_required_source_guards()
	if _failed:
		return
	print("LEAFY_SEADRAGON_GENERATION_OWNERSHIP_SMOKE_OK")
	quit()

func _run_restart_during_phase(phase_method: String) -> bool:
	var board := await _create_board()
	if board == null:
		return false
	var result := {}
	if not bool(board.call("DebugConfigureLeafyBladeGenerationOwnershipProbe")) \
		or not bool(board.call("DebugStartLeafyBladeGenerationOwnershipProbe", result)) \
		or not await _wait_for_true(board, phase_method):
		_fail("Could not start Leafy Blade probe for %s" % phase_method)
		return false
	if not bool(board.call("DebugRestartMatchForLeafyBladeOwnershipProbe")):
		_fail("Could not restart Leafy Blade match during %s" % phase_method)
		return false
	var fingerprint := _state_fingerprint(board)
	if not await _wait_for_result(result):
		return false
	if String(result.get("error", "")) != "" or _state_fingerprint(board) != fingerprint:
		_fail("Old Leafy Blade continuation mutated restarted board during %s: %s" % [phase_method, str(result)])
		return false
	board.queue_free()
	await process_frame
	return true

func _run_reference_replacement_during_projectiles() -> bool:
	var board := await _create_board()
	if board == null:
		return false
	var result := {}
	if not bool(board.call("DebugConfigureLeafyBladeGenerationOwnershipProbe")) \
		or not bool(board.call("DebugStartLeafyBladeGenerationOwnershipProbe", result)) \
		or not await _wait_for_true(board, "DebugHasLeafyBladeProjectilePhase") \
		or not bool(board.call("DebugReplaceLeafyBladeTargetReference")):
		_fail("Could not replace Leafy Blade target during projectile phase")
		return false
	if not await _wait_for_result(result):
		return false
	if String(result.get("error", "")) != "" \
		or not bool(board.call("DebugLeafyBladeReplacementIsIntact")) \
		or int(board.call("DebugGetLeafyBladeBreakCooldownTurns")) != 0:
		_fail("Leafy Blade removed or changed replacement target: %s" % str(result))
		return false
	board.queue_free()
	await process_frame
	return true

func _run_trace_hunt_removal() -> bool:
	var board := await _create_board()
	if board == null:
		return false
	var result := {}
	var target_code := ""
	var original_count := 0
	if not bool(board.call("DebugConfigureLeafyBladeGenerationOwnershipProbe")) \
		or not bool(board.call("DebugMarkLeafyBladeTargetForTraceHunt")) \
		or int(board.call("DebugGetAdventureTraceHuntMarkedTableCardIndex")) < 0 \
		or not bool(board.call("DebugStartLeafyBladeGenerationOwnershipProbe", result)):
		_fail("Could not complete normal Trace Hunt Leafy Blade probe")
		return false
	target_code = String(board.call("DebugGetTableCardCode", 0))
	original_count = int(board.call("DebugGetTableCardCount"))
	if not await _wait_for_result(result):
		return false
	if String(result.get("error", "")) != "" \
		or int(board.call("DebugGetTableCardCount")) != original_count - 1 \
		or _table_contains_code(board, target_code) \
		or int(board.call("DebugGetAdventureTraceHuntMarkedTableCardIndex")) != -1 \
		or bool(board.call("DebugHasAdventureTraceHuntMarkerOverlay")) \
		or bool(board.call("DebugHasAdventureTraceHuntMarkerIntro")):
		_fail("Committed Leafy Blade removal did not clear Trace Hunt marker: %s" % str(result))
		return false
	board.queue_free()
	await process_frame
	return true

func _create_board() -> Node:
	await process_frame
	var packed := load(SCENE_PATH) as PackedScene
	if packed == null:
		_fail("Failed to load game board scene")
		return null
	var board := packed.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "leafy_seadragon")
	root.add_child(board)
	await process_frame
	for method in ["DebugConfigureLeafyBladeGenerationOwnershipProbe", "DebugStartLeafyBladeGenerationOwnershipProbe", "DebugHasLeafyBladeProjectilePhase", "DebugHasLeafyBladeBombPhase", "DebugRestartMatchForLeafyBladeOwnershipProbe", "DebugReplaceLeafyBladeTargetReference", "DebugLeafyBladeReplacementIsIntact", "DebugGetLeafyBladeBreakCooldownTurns", "DebugMarkLeafyBladeTargetForTraceHunt", "DebugGetTableCardReferenceIdentity"]:
		if not board.has_method(method):
			_fail("Missing Leafy Blade ownership hook: %s" % method)
			return null
	return board

func _state_fingerprint(board: Node) -> String:
	var cards: Array[String] = []
	var table_count := int(board.call("DebugGetTableCardCount"))
	for index in range(table_count):
		cards.append("%s@%s" % [
			String(board.call("DebugGetTableCardCode", index)),
			str(board.call("DebugGetTableCardReferenceIdentity", index)),
		])
	return "%s|%s|%s|%s|%s" % [
		str(table_count),
		",".join(cards),
		str(board.call("DebugGetPlayerScoreValue", 0)),
		str(board.call("DebugGetPlayerScoreValue", 1)),
		str(board.call("DebugGetLeafyBladeBreakCooldownTurns")),
	]

func _table_contains_code(board: Node, target_code: String) -> bool:
	for index in range(int(board.call("DebugGetTableCardCount"))):
		if String(board.call("DebugGetTableCardCode", index)) == target_code:
			return true
	return false

func _wait_for_true(board: Node, method: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(board.call(method)):
			return true
	_fail("Timed out waiting for %s" % method)
	return false

func _assert_required_source_guards() -> void:
	var source := FileAccess.get_file_as_string("res://scripts/GameBoardScene.BossTableAbilities.cs")
	if source.count("IsLeafySeadragonBladeBreakTargetCurrent(") < 4 \
		or not source.contains("ClearAdventureTraceHuntMarkerIfTableCardRemoved(targetTableCard);\n\t\t_tableCards.RemoveAt(targetTableCardIndex);") \
		or not source.contains("if (turnSequenceGeneration != _turnSequenceGeneration)\n\t\t{\n\t\t\treturn;\n\t\t}\n\n\t\tCheckForWinner();"):
		_fail("Leafy Blade generation/reference guards are missing from production resolver")

func _wait_for_result(result: Dictionary) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for Leafy Blade probe result")
	return false

func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("LEAFY_SEADRAGON_GENERATION_OWNERSHIP_SMOKE_FAIL %s" % message)
	quit(1)
