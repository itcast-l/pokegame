extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const RECORDING_SETUP_ID := "shrimp_capture_victory_order_01"
const MAIN_PLAYER_INDEX := 0
const ENEMY_PLAYER_INDEX := 1
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
	board.set("solo_mode", false)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	board.set("debug_recording_setup_id", RECORDING_SETUP_ID)

	root.add_child(board)
	await process_frame

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugPlayFirstAvailableLocalMainPlayerHandCard") \
		or not board.has_method("DebugGetGameLoopPhaseName") \
		or not board.has_method("DebugGetCaptureDamageTargetPlayerIndexForHandCard") \
		or not board.has_method("DebugGetCapturePointsForHandCard") \
		or not board.has_method("DebugGetPendingAdventureUpgradeTriggerCount") \
		or not board.has_method("DebugGetPendingAdventureWinnerPlayerIndex") \
		or not board.has_method("DebugIsAdventureUpgradeResolverBusy") \
		or not board.has_method("DebugGetPlayerScoreValue") \
		or not board.has_method("DebugSetPlayerScore"):
		_fail("Missing big-capture victory-order debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	var damage_target := int(board.call("DebugGetCaptureDamageTargetPlayerIndexForHandCard", MAIN_PLAYER_INDEX, 0))
	if damage_target < 0:
		_fail("Expected the recording setup hand card to produce a capture target")
		return

	var capture_points := int(board.call("DebugGetCapturePointsForHandCard", MAIN_PLAYER_INDEX, 0))
	if capture_points <= 0:
		_fail("Expected the recording setup hand card to produce positive capture points")
		return

	print("BIG_CAPTURE_VICTORY_ORDER_TARGET %d" % damage_target)
	print("BIG_CAPTURE_VICTORY_ORDER_POINTS %d" % capture_points)

	var other_player := ENEMY_PLAYER_INDEX if damage_target == MAIN_PLAYER_INDEX else MAIN_PLAYER_INDEX
	if not bool(board.call("DebugSetPlayerScore", MAIN_PLAYER_INDEX, 100)) \
		or not bool(board.call("DebugSetPlayerScore", ENEMY_PLAYER_INDEX, 100)) \
		or not bool(board.call("DebugSetPlayerScore", damage_target, capture_points)) \
		or not bool(board.call("DebugSetPlayerScore", other_player, 100)):
		_fail("Failed to prepare near-victory health values")
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the recording setup hand card")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 0:
		_fail("No upgrade should be counted before first big-capture selection")
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to resolve first upgrade selection")
		return

	print("BIG_CAPTURE_VICTORY_ORDER_AFTER_FIRST_P0 %d" % int(board.call("DebugGetPlayerScoreValue", MAIN_PLAYER_INDEX)))
	print("BIG_CAPTURE_VICTORY_ORDER_AFTER_FIRST_P1 %d" % int(board.call("DebugGetPlayerScoreValue", ENEMY_PLAYER_INDEX)))
	print("BIG_CAPTURE_VICTORY_ORDER_AFTER_FIRST_PENDING %d" % int(board.call("DebugGetPendingAdventureUpgradeTriggerCount")))
	print("BIG_CAPTURE_VICTORY_ORDER_AFTER_FIRST_WINNER %d" % int(board.call("DebugGetPendingAdventureWinnerPlayerIndex")))
	print("BIG_CAPTURE_VICTORY_ORDER_AFTER_FIRST_BUSY %s" % str(board.call("DebugIsAdventureUpgradeResolverBusy")))

	if not await _wait_for_upgrade_selection(board):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Expected exactly one acquired upgrade before victory-upgrade selection")
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to resolve second upgrade selection")
		return

	if not await _wait_for_winner_phase(board):
		return

	board.queue_free()
	await process_frame
	print("ADVENTURE_UPGRADE_BIG_CAPTURE_VICTORY_ORDER_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


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


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for upgrade selection")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

	_fail("Timed out waiting for upgrade selection")
	return false


func _wait_for_winner_phase(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for winner phase")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			continue

		if String(board.call("DebugGetGameLoopPhaseName")) == "GameOver":
			return true

	_fail("Timed out waiting for game over after ordered upgrade selections")
	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_BIG_CAPTURE_VICTORY_ORDER_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
