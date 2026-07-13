extends SceneTree

const Support := preload("res://tests/AdventureUpgradeInteractionSmokeSupport.gd")
const MAIN_PLAYER_INDEX := 0
const ENEMY_PLAYER_INDEX := 1
const ENDPOINT_CROWN_ITEM_ID := "endpoint_crown"
const BORROW_TIDE_ITEM_ID := "borrow_tide"
const OVERTIME_ITEM_ID := "overtime"

var _failed := false
var _support: RefCounted


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_support = Support.new(self, Callable(self, "_fail"))
	var board: Node = await _support.create_board()
	if _failed or board == null:
		return
	if not _support.require_methods(board, [
		"DebugAcquireAdventureUpgrade",
		"DebugConfigureAdventureTriggeredAttachmentMissScenario",
		"DebugConfigureAdventureSuitCollectReturnScenario",
		"DebugAttachItemEffectToMainPlayerHandCard",
		"DebugSimulateAdventureEnemyMiss",
		"DebugSimulateAdventureMainPlayerMiss",
		"DebugHasAdventurePendingExtraBaseCardAction",
		"DebugAddScoreAndCheckWinner",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugIsProcessingCardPlayForOwnershipProbe",
		"DebugGetPlayerScoreValue",
		"DebugGetTotalHandCardCountForPlayer",
		"DebugGetDeckCardCountForPlayer",
		"DebugGetTableCardCount",
		"DebugGetGameLoopPhaseName",
	], "attached capture context"):
		return
	if not await _support.wait_for_actionable(board):
		return
	if not bool(board.call("DebugAcquireAdventureUpgrade", "hearts_enchanted_recovery")):
		_fail("Failed to inject hearts_enchanted_recovery")
		return
	if not bool(board.call("DebugConfigureAdventureTriggeredAttachmentMissScenario")):
		_fail("Failed to configure the triggered-attachment miss fixture")
		return
	if not bool(board.call(
		"DebugAttachItemEffectToMainPlayerHandCard",
		0,
		OVERTIME_ITEM_ID
	)):
		_fail("Failed to attach overtime to the miss card")
		return
	if not bool(board.call("DebugAddScoreAndCheckWinner", ENEMY_PLAYER_INDEX, 10)):
		_fail("Failed to lower main-player health for the miss recovery probe")
		return
	var miss_health_before := int(board.call("DebugGetPlayerScoreValue", MAIN_PLAYER_INDEX))
	if int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX)) != 1 \
		or int(board.call("DebugGetDeckCardCountForPlayer", MAIN_PLAYER_INDEX)) != 1 \
		or int(board.call("DebugGetTableCardCount")) != 3:
		_fail("Triggered-attachment miss fixture did not start from the expected 1/1/3 state")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the overtime miss card")
		return
	if not await _support.wait_for_processing_end_with_max_hand_count(
		board,
		1,
		"Enchanted Recovery miss effects"
	):
		return
	if int(board.call("DebugGetPlayerScoreValue", MAIN_PLAYER_INDEX)) != miss_health_before + 3:
		_fail("Enchanted Recovery did not heal exactly 3 after a triggered attachment miss")
		return
	if int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX)) != 1 \
		or int(board.call("DebugGetDeckCardCountForPlayer", MAIN_PLAYER_INDEX)) != 0:
		_fail("Enchanted Recovery incorrectly drew an extra card after a miss")
		return
	if not await _support.wait_for_actionable_table_count(
		board,
		4,
		"triggered-attachment miss continuation"
	):
		return

	if not bool(board.call("DebugConfigureAdventureSuitCollectReturnScenario")):
		_fail("Failed to configure the attached suit-collect capture")
		return
	for upgrade_id in [
		"spades_intercept_beat",
		"clubs_continue_branch",
	]:
		if not bool(board.call("DebugAcquireAdventureUpgrade", upgrade_id)):
			_fail("Failed to inject adventure upgrade %s" % upgrade_id)
			return
	if not bool(board.call(
		"DebugAttachItemEffectToMainPlayerHandCard",
		0,
		ENDPOINT_CROWN_ITEM_ID
	)):
		_fail("Failed to attach endpoint_crown to the first base card")
		return
	if not bool(board.call("DebugAddScoreAndCheckWinner", ENEMY_PLAYER_INDEX, 10)):
		_fail("Failed to lower and persist main-player health for the recovery probe")
		return
	var health_before := int(board.call("DebugGetPlayerScoreValue", MAIN_PLAYER_INDEX))
	var enemy_health_before := int(board.call("DebugGetPlayerScoreValue", ENEMY_PLAYER_INDEX))
	if not bool(board.call("DebugSimulateAdventureEnemyMiss", 6, false)) \
		or not bool(board.call("DebugSimulateAdventureMainPlayerMiss", 6)) \
		or not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("Failed to seed the two-grant pre-refill observation chain")
		return

	if int(board.call("DebugGetTotalHandCardCountForPlayer", MAIN_PLAYER_INDEX)) != 1 \
		or int(board.call("DebugGetDeckCardCountForPlayer", MAIN_PLAYER_INDEX)) != 1 \
		or int(board.call("DebugGetTableCardCount")) != 2:
		_fail("Attached capture fixture did not start from the expected 1/1/2 state")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to miss the endpoint-crown card onto the table")
		return
	if not await _support.wait_for_actionable_table_count(
		board,
		3,
		"endpoint-crown setup miss"
	):
		return
	if int(board.call("DebugGetPlayerScoreValue", MAIN_PLAYER_INDEX)) != health_before:
		_fail("An on-collect attachment incorrectly triggered during the setup miss")
		return
	if not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("The second grant was lost after the setup miss")
		return
	if not bool(board.call(
		"DebugAttachItemEffectToMainPlayerHandCard",
		0,
		BORROW_TIDE_ITEM_ID
	)):
		_fail("Failed to attach borrow_tide to the capture card")
		return
	enemy_health_before = int(board.call("DebugGetPlayerScoreValue", ENEMY_PLAYER_INDEX))
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the multi-attachment Borrow Tide capture")
		return
	if not await _support.wait_for_processing_hand_count(
		board,
		2,
		"replacement draw plus Enchanted Recovery draw"
	):
		return
	var deck_count_during_effect := int(board.call(
		"DebugGetDeckCardCountForPlayer",
		MAIN_PLAYER_INDEX
	))
	if deck_count_during_effect != 2:
		_fail(
			"Enchanted Recovery did not add exactly one successful-capture draw " \
			+ "after replacement (deck=%d)" % deck_count_during_effect
		)
		return
	if not await _support.wait_for_processing_end_with_max_hand_count(
		board,
		2,
		"Enchanted Recovery capture effects"
	):
		return
	if int(board.call("DebugGetPlayerScoreValue", ENEMY_PLAYER_INDEX)) \
		!= enemy_health_before - 16:
		_fail("The Borrow Tide and endpoint-crown attachments did not both trigger")
		return
	if not await _support.wait_for_actionable_table_count(
		board,
		0,
		"attached capture return to actionable phase"
	):
		return

	var health_after := int(board.call("DebugGetPlayerScoreValue", MAIN_PLAYER_INDEX))
	if health_after != health_before + 3:
		_fail(
			"Enchanted Recovery did not consume the real attachment context for recovery " \
			+ "(health=%d before=%d)" % [health_after, health_before]
		)
		return
	if bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("The two-grant observation chain remained queued after both actions")
		return

	print("ADVENTURE_UPGRADE_ATTACHED_CAPTURE_CONTEXT_SMOKE_OK")
	await _support.dispose_audio()
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_ATTACHED_CAPTURE_CONTEXT_SMOKE_FAIL %s" % message)
	quit(1)
