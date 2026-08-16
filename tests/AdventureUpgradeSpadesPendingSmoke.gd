extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0
const ENEMY_PLAYER_INDEX := 1

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
		or not board.has_method("DebugReplaceAdventureUpgrade") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugSimulateAdventureEnemyMiss") \
		or not board.has_method("DebugSimulateAdventureEnemyCapture") \
		or not board.has_method("DebugGetAdventureEnemyNextCardFacesUp") \
		or not board.has_method("DebugGetAdventureEnemyNextCardInsertModeName") \
		or not board.has_method("DebugGetAdventureEnemyNextCaptureDamageDelta") \
		or not board.has_method("DebugHasAdventurePendingExtraBaseCardAction") \
		or not board.has_method("DebugGetPlayerScoreValue") \
		or not board.has_method("DebugSetPlayerScore"):
		_fail("Missing spades pending debug hooks")
		return

	if not await _wait_for_upgrade_selection(board):
		return

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to dismiss opening adventure upgrade selection")
		return

	if not await _wait_for_main_action(board):
		return

	if not bool(board.call("DebugSetPlayerScore", MAIN_PLAYER_INDEX, 100)) \
		or not bool(board.call("DebugSetPlayerScore", ENEMY_PLAYER_INDEX, 100)):
		_fail("Failed to normalize player scores before pending-state assertions")
		return

	var capture_triggered_upgrades := [
		"spades_dark_harbor_reveal",
		"spades_wrong_shore_lineup",
	]
	for upgrade_id in capture_triggered_upgrades:
		if not bool(board.call("DebugAcquireAdventureUpgrade", upgrade_id)):
			_fail("Failed to inject adventure upgrade %s" % upgrade_id)
			return

	if not bool(board.call("DebugSimulateAdventureEnemyMiss", 4, true)):
		_fail("Failed to simulate enemy miss before capture-trigger assertions")
		return

	if bool(board.call("DebugGetAdventureEnemyNextCardFacesUp")):
		_fail("Expected dark harbor reveal not to trigger from enemy miss")
		return

	if String(board.call("DebugGetAdventureEnemyNextCardInsertModeName")) != "None":
		_fail("Expected wrong shore lineup not to trigger from enemy miss")
		return

	if int(board.call("DebugGetAdventureEnemyNextCaptureDamageDelta")) != 0:
		_fail("Expected capture-triggered upgrades not to alter next capture damage")
		return

	if not bool(board.call("DebugSimulateAdventureEnemyCapture")):
		_fail("Failed to simulate enemy capture pending state")
		return

	if not bool(board.call("DebugGetAdventureEnemyNextCardFacesUp")):
		_fail("Expected dark harbor reveal to trigger from enemy capture")
		return

	if String(board.call("DebugGetAdventureEnemyNextCardInsertModeName")) != "Leftmost":
		_fail("Expected wrong shore lineup to trigger from enemy capture")
		return

	if int(board.call("DebugGetAdventureEnemyNextCaptureDamageDelta")) != 0:
		_fail("Expected enemy capture pending state to leave damage delta unchanged")
		return

	var miss_triggered_upgrades := [
		"spades_missed_beat_blunt_hurt",
		"spades_miss_bleed",
		"spades_intercept_beat",
	]
	for upgrade_id in miss_triggered_upgrades:
		if not bool(board.call("DebugAcquireAdventureUpgrade", upgrade_id)):
			_fail("Failed to inject adventure upgrade %s" % upgrade_id)
			return

	var replacement_fillers := [
		"hearts_thick_blood",
		"hearts_shoal_preparation",
		"hearts_warm_current_supply",
		"diamonds_small_insurance",
		"diamonds_multi_card_premium",
		"clubs_cross_graft",
	]
	for upgrade_id in replacement_fillers:
		if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) >= 8:
			break
		board.call("DebugAcquireAdventureUpgrade", upgrade_id)
	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 8:
		_fail("Failed to fill the adventure upgrade capacity for conflict replacement")
		return
	if not bool(board.call(
			"DebugReplaceAdventureUpgrade",
			"spades_return_tide_misorder",
			"spades_wrong_shore_lineup")):
		_fail("Failed to atomically replace wrong-shore lineup with return-tide misorder")
		return

	if not bool(board.call("DebugSimulateAdventureEnemyMiss", 5, true)):
		_fail("Failed to simulate below-threshold enemy miss pending state")
		return

	if int(board.call("DebugGetPlayerScoreValue", ENEMY_PLAYER_INDEX)) != 100:
		_fail("Expected miss bleed not to deal direct damage below 6 table cards")
		return

	if not bool(board.call("DebugSimulateAdventureEnemyMiss", 6, true)):
		_fail("Failed to simulate enemy miss pending state")
		return

	if bool(board.call("DebugGetAdventureEnemyNextCardFacesUp")):
		_fail("Expected enemy next base card to remain face-down")
		return

	if String(board.call("DebugGetAdventureEnemyNextCardInsertModeName")) != "BeforeTrackedOldest":
		_fail("Expected misorder to override insert mode to BeforeTrackedOldest")
		return

	if int(board.call("DebugGetAdventureEnemyNextCaptureDamageDelta")) != -3:
		_fail("Expected next enemy capture damage delta to be -3")
		return

	if not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("Expected intercept beat to arm an extra base-card action")
		return

	if int(board.call("DebugGetPlayerScoreValue", ENEMY_PLAYER_INDEX)) != 98:
		_fail("Expected miss bleed to deal 2 direct damage to the enemy")
		return

	print("ADVENTURE_UPGRADE_SPADES_PENDING_SMOKE_OK")
	board.queue_free()
	await _wait_frames(12)
	await _dispose_audio_before_quit()
	quit(0)


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for adventure upgrade selection")
			return false
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true
	_fail("Timed out waiting for opening adventure upgrade selection")
	return false


func _wait_for_main_action(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() + int(TIMEOUT_SECONDS * 1000.0)
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for main action state")
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for main action state")
	return false


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
	await _wait_frames(12)


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_SPADES_PENDING_SMOKE_FAIL %s" % message)
	quit(1)
