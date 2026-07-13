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
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	await process_frame

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugAcquireAdventureUpgrade") \
		or not board.has_method("DebugResolveAdventureQuestionRewardRejudge") \
		or not board.has_method("DebugResolveAdventureQuestionRewardRejudgeWithChoice") \
		or not board.has_method("DebugHasAdventureQuestionRejudgeUsed") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing rejudge debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", "diamonds_rejudge_once")):
		_fail("Failed to inject diamonds_rejudge_once")
		return

	var first_result := String(board.call("DebugResolveAdventureQuestionRewardRejudge", "bomb"))
	if first_result != "bomb":
		_fail("Expected headless rejudge flow to keep the original reward by default")
		return

	if bool(board.call("DebugHasAdventureQuestionRejudgeUsed")):
		_fail("Expected keeping the first reward to preserve the once-per-battle reroll")
		return

	var second_result := String(board.call("DebugResolveAdventureQuestionRewardRejudgeWithChoice", "lens", true))
	if second_result.is_empty():
		_fail("Expected the next reward to support the preserved reroll")
		return

	if not bool(board.call("DebugHasAdventureQuestionRejudgeUsed")):
		_fail("Expected choosing reroll to consume the once-per-battle opportunity")
		return

	var third_result := String(board.call("DebugResolveAdventureQuestionRewardRejudgeWithChoice", "shuffle", true))
	if third_result != "shuffle":
		_fail("Expected a forced reroll request after consumption to keep the original reward")
		return

	print("ADVENTURE_UPGRADE_REJUDGE_ONCE_SMOKE_OK")
	quit()


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for actionable state")
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for actionable state")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_REJUDGE_ONCE_SMOKE_FAIL %s" % message)
	quit(1)
