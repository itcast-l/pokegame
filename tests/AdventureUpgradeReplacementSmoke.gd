extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0
const MAIN_PLAYER_INDEX := 0
const FORCED_VICTORY_POINTS := 100
const VIEWPORT_SIZES := [
	Vector2i(1280, 720),
	Vector2i(960, 900),
	Vector2i(1680, 720),
]
const REQUIRED_METHODS := [
	"DebugIsAdventureUpgradeSelectionVisible",
	"DebugGetAdventureUpgradeOfferIds",
	"DebugSelectAdventureUpgradeOption",
	"DebugFillAdventureUpgradeCapacityExcludingCurrentOffer",
	"DebugProbeAdventureReplacementBoardStateCleanup",
	"DebugIsAdventureUpgradeReplacementVisible",
	"DebugGetAdventureUpgradeReplacementPage",
	"DebugGetAdventureUpgradeReplacementPageCount",
	"DebugAdvanceAdventureUpgradeReplacementPage",
	"DebugRewindAdventureUpgradeReplacementPage",
	"DebugReturnFromAdventureUpgradeReplacement",
	"DebugSelectAdventureUpgradeReplacementSlot",
	"DebugGetAdventureActiveUpgradeIds",
	"DebugGetAdventureRetiredUpgradeIds",
	"DebugGetAdventureCurrentUpgradeIdsInCatalogOrder",
	"DebugForceNextAdventureUpgradeReplacementOverlayDisplayFailure",
	"DebugAddScoreAndCheckWinner",
	"DebugGetPendingAdventureUpgradeTriggerCount",
	"DebugIsAdventureUpgradeResolverBusy",
	"DebugGetGameLoopPhaseName",
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = VIEWPORT_SIZES[0]
	var board := await _create_board()
	if board == null:
		return
	if not await _exercise_interactive_replacement(board):
		return
	if not await _exercise_display_failure_replacement(board):
		return

	board.queue_free()
	await _wait_frames(12)
	print("ADVENTURE_UPGRADE_REPLACEMENT_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _create_board() -> Node:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return null

	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "shrimp")
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return null
	root.add_child(board)
	await process_frame

	for method in REQUIRED_METHODS:
		if not board.has_method(method):
			_fail("Missing adventure replacement debug hook: %s" % method)
			return null
	if not await _wait_for_selection(board):
		return null
	return board


func _exercise_interactive_replacement(board: Node) -> bool:
	if not bool(board.call("DebugProbeAdventureReplacementBoardStateCleanup")):
		return _fail_bool("Replacement board-state cleanup probe failed")

	var original_offer := _string_array(board.call("DebugGetAdventureUpgradeOfferIds"))
	if original_offer.is_empty() or original_offer.size() > 3:
		return _fail_bool("Expected 1-3 upgrade offer IDs")
	if not bool(board.call("DebugFillAdventureUpgradeCapacityExcludingCurrentOffer")):
		return _fail_bool("Failed to fill eight upgrades without consuming the visible offer")

	var active_before := _string_array(board.call("DebugGetAdventureActiveUpgradeIds"))
	if active_before.size() != 8 or original_offer[0] in active_before:
		return _fail_bool("Replacement setup did not preserve eight old upgrades and one new candidate")
	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		return _fail_bool("Failed to choose the new upgrade")
	if not await _wait_for_replacement(board):
		return false

	if int(board.call("DebugGetAdventureUpgradeReplacementPage")) != 0 \
			or int(board.call("DebugGetAdventureUpgradeReplacementPageCount")) != 3:
		return _fail_bool("Eight upgrades should produce three replacement pages starting at page zero")
	for viewport_size in VIEWPORT_SIZES:
		root.size = viewport_size
		await _wait_frames(4)
		if not _assert_replacement_layout(board, viewport_size):
			return false

	if not bool(board.call("DebugAdvanceAdventureUpgradeReplacementPage")) \
			or int(board.call("DebugGetAdventureUpgradeReplacementPage")) != 1:
		return _fail_bool("Replacement next-page navigation did not reach page two")
	if not bool(board.call("DebugAdvanceAdventureUpgradeReplacementPage")) \
			or int(board.call("DebugGetAdventureUpgradeReplacementPage")) != 2:
		return _fail_bool("Replacement next-page navigation did not reach the final page")
	await _wait_frames(2)
	if not _assert_last_page_footprint(board):
		return false
	if not bool(board.call("DebugRewindAdventureUpgradeReplacementPage")) \
			or int(board.call("DebugGetAdventureUpgradeReplacementPage")) != 1:
		return _fail_bool("Replacement previous-page navigation did not return to page two")

	if not bool(board.call("DebugReturnFromAdventureUpgradeReplacement")):
		return _fail_bool("Replacement back action failed")
	if not await _wait_for_selection(board):
		return false
	var returned_offer := _string_array(board.call("DebugGetAdventureUpgradeOfferIds"))
	if returned_offer != original_offer:
		return _fail_bool("Returning from replacement rerolled or reordered the original offer")

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		return _fail_bool("Failed to reselect the unchanged new upgrade")
	if not await _wait_for_replacement(board):
		return false
	if not bool(board.call("DebugAdvanceAdventureUpgradeReplacementPage")) \
			or not bool(board.call("DebugAdvanceAdventureUpgradeReplacementPage")):
		return _fail_bool("Failed to return to the final replacement page")
	if not bool(board.call("DebugSelectAdventureUpgradeReplacementSlot", 1)):
		return _fail_bool("Failed to select the eighth old upgrade on the final page")
	if not await _wait_for_resolution(board):
		return false

	return _assert_replacement_result(board, active_before, original_offer[0], "interactive")


func _exercise_display_failure_replacement(board: Node) -> bool:
	var active_before := _string_array(board.call("DebugGetAdventureActiveUpgradeIds"))
	var retired_before := _string_array(board.call("DebugGetAdventureRetiredUpgradeIds"))
	var catalog_order := _string_array(board.call("DebugGetAdventureCurrentUpgradeIdsInCatalogOrder"))
	if active_before.size() != 8 or catalog_order.size() != 8:
		return _fail_bool("Display-failure setup did not expose eight stable replacement targets")

	board.call("DebugForceNextAdventureUpgradeReplacementOverlayDisplayFailure")
	if not bool(board.call(
			"DebugAddScoreAndCheckWinner",
			MAIN_PLAYER_INDEX,
			FORCED_VICTORY_POINTS)):
		return _fail_bool("Failed to trigger a victory upgrade for display-failure replacement")
	if not await _wait_for_selection(board):
		return false

	var offer := _string_array(board.call("DebugGetAdventureUpgradeOfferIds"))
	if offer.is_empty():
		return _fail_bool("Display-failure setup has no upgrade offer")

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		return _fail_bool("Failed to select candidate for display-failure replacement")
	if not await _wait_for_resolution(board):
		return false
	if not _assert_replacement_result(board, active_before, offer[0], "display failure"):
		return false

	var retired := _string_array(board.call("DebugGetAdventureRetiredUpgradeIds"))
	if retired.size() != retired_before.size() + 1 or catalog_order[0] not in retired:
		return _fail_bool("Display failure did not retire the first catalog-ordered legal target")
	return true


func _assert_replacement_result(
		board: Node,
		active_before: Array[String],
		new_upgrade_id: String,
		context: String) -> bool:
	var active_after := _string_array(board.call("DebugGetAdventureActiveUpgradeIds"))
	var retired_after := _string_array(board.call("DebugGetAdventureRetiredUpgradeIds"))
	if active_after.size() != 8 or new_upgrade_id not in active_after:
		return _fail_bool("%s replacement did not leave eight active upgrades including the new one" % context)

	var removed: Array[String] = []
	for upgrade_id in active_before:
		if upgrade_id not in active_after:
			removed.append(upgrade_id)
	if removed.size() != 1 or removed[0] not in retired_after:
		return _fail_bool("%s replacement did not retire exactly the removed old upgrade" % context)
	if bool(board.call("DebugIsAdventureUpgradeReplacementVisible")):
		return _fail_bool("%s replacement overlay remained visible after commit" % context)
	return true


func _assert_replacement_layout(board: Node, viewport_size: Vector2i) -> bool:
	var overlay := board.find_child("AdventureUpgradeReplacementOverlay", true, false) as Control
	if overlay == null:
		return _fail_bool("Missing replacement overlay at %s" % viewport_size)
	var viewport_rect := overlay.get_global_rect()
	var backdrop := overlay.get_node_or_null("TableBackdrop") as TextureRect
	if backdrop == null \
			or backdrop.get_global_rect().position.distance_to(viewport_rect.position) > 1.0 \
			or backdrop.get_global_rect().size.distance_to(viewport_rect.size) > 2.0 \
			or backdrop.stretch_mode != TextureRect.STRETCH_KEEP_ASPECT_COVERED:
		return _fail_bool("Replacement backdrop does not cover the logical viewport at %s" % viewport_size)

	var required_paths := [
		"Margin/Layout/Header/Title",
		"Margin/Layout/Content/NewColumn/Label",
		"Margin/Layout/Content/CurrentColumn/Label",
		"Margin/Layout/Content/CurrentColumn/PageLabel",
		"Margin/Layout/BackButton",
	]
	for node_path in required_paths:
		var control := overlay.get_node_or_null(node_path) as Control
		if control == null or not control.visible or not _contains_rect(viewport_rect, control.get_global_rect()):
			return _fail_bool("Replacement control %s overflowed at %s" % [node_path, viewport_size])
		if control is Label and (control as Label).text.is_empty():
			return _fail_bool("Replacement label %s is empty at %s" % [node_path, viewport_size])
		if control is Button and (control as Button).text.is_empty():
			return _fail_bool("Replacement button %s is empty at %s" % [node_path, viewport_size])

	var card_controls: Array[Control] = []
	var new_card := overlay.get_node_or_null("Margin/Layout/Content/NewColumn/Card") as Control
	if new_card == null:
		return _fail_bool("Replacement overlay is missing the new-upgrade card")
	card_controls.append(new_card)
	for slot_index in range(3):
		var button := overlay.get_node_or_null(
			"Margin/Layout/Content/CurrentColumn/PageRow/CardsRow/Option%dButton" % (slot_index + 1)) as Button
		if button == null or not button.visible:
			return _fail_bool("Replacement card slot %d lost its stable footprint" % slot_index)
		card_controls.append(button)

	for index in range(card_controls.size()):
		var card_rect := card_controls[index].get_global_rect()
		if not _contains_rect(viewport_rect, card_rect):
			return _fail_bool("Replacement card %d leaves the viewport at %s" % [index, viewport_size])
		for other_index in range(index + 1, card_controls.size()):
			if card_rect.intersects(card_controls[other_index].get_global_rect()):
				return _fail_bool("Replacement cards overlap at %s" % viewport_size)

	var focus_owner := overlay.get_viewport().gui_get_focus_owner()
	if focus_owner == null or not overlay.is_ancestor_of(focus_owner) or not focus_owner is Button:
		return _fail_bool("Replacement overlay has no keyboard/gamepad focus at %s" % viewport_size)
	return true


func _assert_last_page_footprint(board: Node) -> bool:
	var overlay := board.find_child("AdventureUpgradeReplacementOverlay", true, false) as Control
	if overlay == null:
		return _fail_bool("Missing replacement overlay on final page")

	var enabled_count := 0
	for slot_index in range(3):
		var button := overlay.get_node(
			"Margin/Layout/Content/CurrentColumn/PageRow/CardsRow/Option%dButton" % (slot_index + 1)) as Button
		if not button.visible:
			return _fail_bool("Final replacement page changed the three-card footprint")
		if not button.disabled:
			enabled_count += 1
	if enabled_count != 2:
		return _fail_bool("Final replacement page should contain two enabled cards and one placeholder")
	return true


func _wait_for_selection(board: Node) -> bool:
	return await _wait_for_board_state(board, "selection")


func _wait_for_replacement(board: Node) -> bool:
	return await _wait_for_board_state(board, "replacement")


func _wait_for_resolution(board: Node) -> bool:
	return await _wait_for_board_state(board, "resolved")


func _wait_for_board_state(board: Node, state: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(board):
			return _fail_bool("Board freed while waiting for %s" % state)
		match state:
			"selection":
				if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
					return true
			"replacement":
				if bool(board.call("DebugIsAdventureUpgradeReplacementVisible")):
					return true
			"resolved":
				if not bool(board.call("DebugIsAdventureUpgradeSelectionVisible")) \
						and not bool(board.call("DebugIsAdventureUpgradeReplacementVisible")) \
						and not bool(board.call("DebugIsAdventureUpgradeResolverBusy")) \
						and int(board.call("DebugGetPendingAdventureUpgradeTriggerCount")) == 0 \
						and String(board.call("DebugGetGameLoopPhaseName")) in [
							"AwaitingMainPlayerAction",
							"GameOver",
						]:
					return true
	return _fail_bool("Timed out waiting for adventure replacement state: %s" % state)


func _string_array(value: Variant) -> Array[String]:
	var result: Array[String] = []
	for entry in value:
		result.append(String(entry))
	return result


func _contains_rect(outer: Rect2, inner: Rect2) -> bool:
	return inner.position.x >= outer.position.x - 0.5 \
		and inner.position.y >= outer.position.y - 0.5 \
		and inner.end.x <= outer.end.x + 0.5 \
		and inner.end.y <= outer.end.y + 0.5


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _fail_bool(message: String) -> bool:
	_fail(message)
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_REPLACEMENT_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await _wait_frames(12)
