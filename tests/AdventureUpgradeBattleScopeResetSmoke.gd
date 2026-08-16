extends SceneTree

const Support := preload("res://tests/AdventureUpgradeInteractionSmokeSupport.gd")

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
		"DebugReplaceAdventureUpgrade",
		"DebugResolveAdventureQuestionRewardRejudgeWithChoice",
		"DebugHasAdventureQuestionRejudgeUsed",
		"DebugSimulateAdventureEnemyMiss",
		"DebugSimulateAdventureEnemyCapture",
		"DebugGetAdventureEnemyNextCardFacesUp",
		"DebugGetAdventureEnemyNextCardInsertModeName",
		"DebugGetAdventureEnemyNextCaptureDamageDelta",
		"DebugHasAdventurePendingExtraBaseCardAction",
		"DebugGetAdventureAcquiredUpgradeCount",
		"DebugRestartWithoutOpeningDealAnimation",
		"DebugGetGameLoopPhaseName",
	], "battle-scope reset"):
		return
	if not await _support.wait_for_actionable(board):
		return

	var battle_upgrades := [
		"diamonds_rejudge_once",
		"spades_missed_beat_blunt_hurt",
		"spades_return_tide_misorder",
		"spades_intercept_beat",
		"spades_dark_harbor_reveal",
	]
	for upgrade_id in battle_upgrades:
		if not bool(board.call("DebugAcquireAdventureUpgrade", upgrade_id)):
			_fail("Failed to inject adventure upgrade %s" % upgrade_id)
			return

	var rerolled_reward := String(board.call(
		"DebugResolveAdventureQuestionRewardRejudgeWithChoice",
		"bomb",
		true
	))
	if rerolled_reward.is_empty() or not bool(board.call("DebugHasAdventureQuestionRejudgeUsed")):
		_fail("Failed to seed the once-per-battle rejudge state")
		return
	if not bool(board.call("DebugSimulateAdventureEnemyMiss", 6, true)):
		_fail("Failed to seed enemy-miss battle state")
		return
	if not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")) \
		or int(board.call("DebugGetAdventureEnemyNextCaptureDamageDelta")) != -3 \
		or String(board.call("DebugGetAdventureEnemyNextCardInsertModeName")) != "BeforeTrackedOldest":
		_fail("Enemy-miss battle state was not armed before reset")
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
		_fail("Failed to fill upgrade capacity before the battle-state replacement")
		return
	if not bool(board.call(
			"DebugReplaceAdventureUpgrade",
			"spades_wrong_shore_lineup",
			"spades_return_tide_misorder")):
		_fail("Failed to replace return-tide misorder with wrong-shore lineup")
		return
	if not bool(board.call("DebugSimulateAdventureEnemyCapture")):
		_fail("Failed to seed enemy-capture battle state")
		return
	if not bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")) \
		or not bool(board.call("DebugGetAdventureEnemyNextCardFacesUp")) \
		or int(board.call("DebugGetAdventureEnemyNextCaptureDamageDelta")) != -3 \
		or String(board.call("DebugGetAdventureEnemyNextCardInsertModeName")) != "Leftmost":
		_fail("Enemy-capture battle state was not armed before reset")
		return

	var acquired_count_before := int(board.call("DebugGetAdventureAcquiredUpgradeCount"))
	if not bool(board.call("DebugRestartWithoutOpeningDealAnimation")):
		_fail("Failed to cross the real new-battle board boundary")
		return
	if not await _support.wait_for_actionable(board, "post-reset main action"):
		return

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != acquired_count_before:
		_fail("Run-scoped acquired upgrades were lost at the battle boundary")
		return
	if bool(board.call("DebugHasAdventureQuestionRejudgeUsed")):
		_fail("The once-per-battle rejudge flag leaked into the new battle")
		return
	if bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")):
		_fail("An extra-action root chain leaked into the new battle")
		return
	if bool(board.call("DebugGetAdventureEnemyNextCardFacesUp")) \
		or String(board.call("DebugGetAdventureEnemyNextCardInsertModeName")) != "None" \
		or int(board.call("DebugGetAdventureEnemyNextCaptureDamageDelta")) != 0:
		_fail("Enemy pending modifiers leaked into the new battle")
		return

	var next_battle_reward := String(board.call(
		"DebugResolveAdventureQuestionRewardRejudgeWithChoice",
		"lens",
		true
	))
	if next_battle_reward.is_empty() or not bool(board.call("DebugHasAdventureQuestionRejudgeUsed")):
		_fail("Rejudge Once was not reusable after the battle-scope reset")
		return

	if not await _support.dispose_board_and_audio(board, "battle-scope reset"):
		return
	print("ADVENTURE_UPGRADE_BATTLE_SCOPE_RESET_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_BATTLE_SCOPE_RESET_SMOKE_FAIL %s" % message)
	quit(1)
