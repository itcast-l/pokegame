extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const HAND_AREA_PATH := "LayoutAreas/Player1HandArea"
const BADGE_NODE_NAME := "AdventureMissClimbBadge"
const UPGRADE_ID := "clubs_miss_climb_growth"
const MAX_WAIT_FRAMES := 3600
const VIEWPORT_SPECS := [
	{"name": "1280x720", "size": Vector2i(1280, 720)},
	{"name": "1680x720", "size": Vector2i(1680, 720)},
]

var _failed := false
var _screenshot_dir := ""
var _capture_screenshots := true


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	_screenshot_dir = OS.get_environment("CODEX_MISS_CLIMB_BADGE_SCREENSHOT_DIR")
	_capture_screenshots = OS.get_environment(
		"CODEX_MISS_CLIMB_BADGE_CAPTURE_SCREENSHOTS"
	) != "0"
	if _screenshot_dir.is_empty():
		_screenshot_dir = ProjectSettings.globalize_path(
			"res://outputs/adventure_upgrade_clubs_miss_climb_badge_smoke"
		)
	var directory_error := DirAccess.make_dir_recursive_absolute(_screenshot_dir)
	if directory_error != OK:
		_fail("Failed to create screenshot directory: %s" % _screenshot_dir)
		return

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
	root.add_child(board)
	await process_frame
	await process_frame

	if not _require_methods(board):
		return
	if not await _wait_for_actionable(board):
		return

	# A positive debug stack without the upgrade must not expose implementation state.
	if not bool(board.call("DebugConfigureAdventureMissClimbBadgeScenario", 2)):
		_fail("Failed to configure the non-acquired miss-climb badge scenario")
		return
	await process_frame
	if int(board.call("DebugGetAdventureMissClimbBadgeCount")) != 0:
		_fail("Miss-climb badges must not exist before the upgrade is acquired")
		return

	if not bool(board.call("DebugAcquireAdventureUpgrade", UPGRADE_ID)):
		_fail("Failed to acquire %s" % UPGRADE_ID)
		return
	if not bool(board.call("DebugConfigureAdventureMissClimbBadgeScenario", 0)):
		_fail("Failed to configure the zero-stack miss-climb badge scenario")
		return
	await process_frame
	if int(board.call("DebugGetAdventureMissClimbBadgeCount")) != 0:
		_fail("Zero miss-climb stacks must not create badges")
		return

	if not bool(board.call("DebugSimulateAdventureMainPlayerMiss", 1)):
		_fail("Failed to apply the first miss-climb stack")
		return
	await process_frame
	if int(board.call("DebugGetAdventureMissClimbGrowthStacks")) != 1 \
		or int(board.call("DebugGetAdventureMissClimbBadgeCount")) != 2:
		_fail("First miss did not immediately render the eligible +1 badges")
		return
	var first_growth_badge := _get_badge(board, 0)
	var first_growth_label: Label = null
	if first_growth_badge != null:
		first_growth_label = first_growth_badge.get_node_or_null("GrowthLabel") as Label
	if first_growth_label == null or first_growth_label.text != "♣ +1":
		_fail("First miss rendered an incorrect growth label")
		return

	if not bool(board.call("DebugSimulateAdventureMainPlayerMiss", 1)):
		_fail("Failed to apply the second miss-climb stack")
		return
	await process_frame
	await process_frame
	if int(board.call("DebugGetAdventureMissClimbGrowthStacks")) != 2:
		_fail("Second miss did not immediately advance the badge to +2")
		return
	if int(board.call("DebugGetAdventureMissClimbBadgeCount")) != 2:
		_fail("Expected badges only on the Hearts 4 and Spades 6 cards")
		return
	if not _verify_badge_eligibility(board):
		return
	if not await _verify_hover_and_keyboard_states(board):
		return
	if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
		if not bool(board.call("DebugDisposeActiveSelectionOverlayForAwaitProbe")):
			_fail("Failed to remove the startup upgrade overlay before screenshots")
			return
		for _frame in range(4):
			await process_frame
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			_fail("Startup upgrade overlay remained visible before screenshots")
			return

	for viewport_spec in VIEWPORT_SPECS:
		if not await _capture_viewport_states(board, viewport_spec):
			return

	root.size = Vector2i(1280, 720)
	await process_frame
	if not bool(board.call("DebugConfigureAdventureMissClimbBadgeScenario", 2)):
		_fail("Failed to restore the capture-reset scenario")
		return
	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the fixed Hearts 4 capture card")
		return
	if not await _wait_for_successful_capture_reset(board):
		return
	if int(board.call("DebugGetAdventureMissClimbBadgeCount")) != 0:
		_fail("Successful capture reset left stale miss-climb badges in hand")
		return

	print("ADVENTURE_UPGRADE_CLUBS_MISS_CLIMB_BADGE_SMOKE_OK screenshots=%s" % _screenshot_dir)
	board.queue_free()
	for _frame in range(12):
		await process_frame
	await _dispose_audio_before_quit()
	quit(0)


func _require_methods(board: Node) -> bool:
	var methods := [
		"DebugAcquireAdventureUpgrade",
		"DebugConfigureAdventureMissClimbBadgeScenario",
		"DebugGetAdventureMissClimbGrowthStacks",
		"DebugGetAdventureMissClimbBadgeCount",
		"DebugSetAdventureMissClimbBadgeHoverState",
		"DebugSetAdventureMissClimbBadgeKeyboardSelection",
		"DebugSimulateAdventureMainPlayerMiss",
		"DebugPlayFirstAvailableLocalMainPlayerHandCard",
		"DebugIsProcessingCardPlayForOwnershipProbe",
		"DebugGetGameLoopPhaseName",
		"DebugIsAdventureUpgradeSelectionVisible",
		"DebugDisposeActiveSelectionOverlayForAwaitProbe",
	]
	for method in methods:
		if not board.has_method(method):
			_fail("Missing miss-climb badge debug hook: %s" % method)
			return false
	return true


func _verify_badge_eligibility(board: Node) -> bool:
	var low_badge := _get_badge(board, 0)
	var second_suit_badge := _get_badge(board, 1)
	if low_badge == null or second_suit_badge == null:
		_fail("Expected eligible badges on two numeric suits")
		return false
	if _get_badge(board, 2) != null:
		_fail("Rank 10 must not show a +2 badge because the adjusted rank exceeds 10")
		return false
	if _get_badge(board, 3) != null:
		_fail("J face cards must not show a miss-climb badge")
		return false
	if _get_badge(board, 4) != null:
		_fail("Item cards must not show a miss-climb badge")
		return false

	var growth_label := low_badge.get_node_or_null("GrowthLabel") as Label
	if growth_label == null or growth_label.text != "♣ +2":
		_fail("Expected stable badge text '♣ +2'")
		return false
	if low_badge.visible or second_suit_badge.visible:
		_fail("Eligible badges must be hidden at rest")
		return false
	return true


func _verify_hover_and_keyboard_states(board: Node) -> bool:
	var first_badge := _get_badge(board, 0)
	if first_badge == null:
		_fail("Missing first eligible badge before hover verification")
		return false
	if not bool(board.call("DebugSetAdventureMissClimbBadgeHoverState", 0, true)):
		_fail("Failed to enable the first badge hover state")
		return false
	await process_frame
	if not first_badge.visible:
		_fail("Hovered badge did not become visible")
		return false
	if first_badge.modulate.a >= 0.999 or first_badge.scale.x >= 0.999:
		_fail("Hovered badge should ease in instead of snapping to its final state")
		return false
	for _frame in range(12):
		await process_frame
	if first_badge.modulate.a < 0.98 or absf(first_badge.scale.x - 1.0) > 0.02:
		_fail("Hovered badge did not settle at full opacity and scale")
		return false

	if not bool(board.call("DebugSetAdventureMissClimbBadgeHoverState", 0, false)) \
		or not bool(board.call("DebugSetAdventureMissClimbBadgeHoverState", 1, true)):
		_fail("Failed to switch hover to the adjacent eligible card")
		return false
	for _frame in range(30):
		await process_frame
	var second_badge := _get_badge(board, 1)
	if first_badge.visible or second_badge == null or not second_badge.visible:
		_fail(
			"Adjacent hover switch did not settle (old_visible=%s new_present=%s new_visible=%s)"
			% [
				str(first_badge.visible),
				str(second_badge != null),
				str(second_badge != null and second_badge.visible),
			]
		)
		return false
	if not bool(board.call("DebugSetAdventureMissClimbBadgeHoverState", 1, false)):
		_fail("Failed to clear the adjacent hover state")
		return false
	for _frame in range(30):
		await process_frame
	if second_badge.visible:
		_fail("Mouse exit did not hide the unselected badge")
		return false

	if not bool(board.call("DebugSetAdventureMissClimbBadgeKeyboardSelection", 0, true)):
		_fail("Failed to keyboard-select the first eligible card")
		return false
	await process_frame
	for _frame in range(12):
		await process_frame
	first_badge = _get_badge(board, 0)
	if first_badge == null or not first_badge.visible or first_badge.modulate.a < 0.98:
		_fail("Keyboard-selected badge did not become visible")
		return false
	if not bool(board.call("DebugSetAdventureMissClimbBadgeHoverState", 0, false)):
		_fail("Failed to emit mouse exit while the card remained keyboard-selected")
		return false
	for _frame in range(30):
		await process_frame
	if not first_badge.visible or first_badge.modulate.a < 0.98:
		_fail("Mouse exit hid a badge that remained keyboard-selected")
		return false

	if not bool(board.call("DebugSetAdventureMissClimbBadgeKeyboardSelection", 0, false)):
		_fail("Failed to cancel keyboard selection")
		return false
	await process_frame
	first_badge = _get_badge(board, 0)
	if first_badge == null or first_badge.visible:
		_fail("Canceling keyboard selection did not restore the hidden rest state")
		return false
	return true


func _capture_viewport_states(board: Node, viewport_spec: Dictionary) -> bool:
	var viewport_size: Vector2i = viewport_spec.get("size", Vector2i(1280, 720))
	var viewport_name := String(viewport_spec.get("name", "viewport"))
	root.size = viewport_size
	for _frame in range(4):
		await process_frame
	if not bool(board.call("DebugConfigureAdventureMissClimbBadgeScenario", 2)):
		_fail("Failed to configure screenshot state for %s" % viewport_name)
		return false
	await process_frame

	if not bool(board.call("DebugSetAdventureMissClimbBadgeHoverState", 0, true)):
		_fail("Failed to set screenshot hover state for %s" % viewport_name)
		return false
	for _frame in range(12):
		await process_frame
	if not _verify_badge_layout(board, 0, viewport_size, viewport_name + " hover"):
		return false
	if _capture_screenshots \
		and not await _save_screenshot(board, "%s_hover.png" % viewport_name):
		return false
	if not bool(board.call("DebugSetAdventureMissClimbBadgeHoverState", 0, false)):
		_fail("Failed to clear screenshot hover state for %s" % viewport_name)
		return false
	for _frame in range(30):
		await process_frame

	if not bool(board.call("DebugSetAdventureMissClimbBadgeKeyboardSelection", 1, true)):
		_fail("Failed to set screenshot keyboard state for %s" % viewport_name)
		return false
	for _frame in range(12):
		await process_frame
	if not _verify_badge_layout(board, 1, viewport_size, viewport_name + " keyboard"):
		return false
	if _capture_screenshots \
		and not await _save_screenshot(board, "%s_keyboard.png" % viewport_name):
		return false
	if not bool(board.call("DebugSetAdventureMissClimbBadgeKeyboardSelection", 1, false)):
		_fail("Failed to clear screenshot keyboard state for %s" % viewport_name)
		return false
	await process_frame
	return true


func _verify_badge_layout(
	board: Node,
	hand_index: int,
	viewport_size: Vector2i,
	label: String
) -> bool:
	var card_visual := _get_card_visual(board, hand_index)
	var badge := _get_badge(board, hand_index)
	if card_visual == null or badge == null or not badge.visible:
		_fail("%s is missing a visible card visual or badge" % label)
		return false

	var card_rect := card_visual.get_global_rect()
	var badge_rect := badge.get_global_rect()
	if badge_rect.position.x < card_rect.get_center().x \
		or badge_rect.position.y > card_rect.position.y + 14.0:
		_fail("%s badge is not anchored at the card's upper-right: card=%s badge=%s" % [
			label,
			str(card_rect),
			str(badge_rect),
		])
		return false
	if badge_rect.position.x < -0.5 \
		or badge_rect.position.y < -0.5 \
		or badge_rect.end.x > float(viewport_size.x) + 0.5 \
		or badge_rect.end.y > float(viewport_size.y) + 0.5:
		_fail("%s badge exceeds the safe viewport: badge=%s viewport=%s" % [
			label,
			str(badge_rect),
			str(viewport_size),
		])
		return false
	return true


func _save_screenshot(board: Node, filename: String) -> bool:
	await process_frame
	var image := board.get_viewport().get_texture().get_image()
	if image == null or image.is_empty():
		_fail("Failed to capture screenshot image: %s" % filename)
		return false
	var output_path := _screenshot_dir.path_join(filename)
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save screenshot %s: %s" % [output_path, error_string(save_error)])
		return false
	print("MISS_CLIMB_BADGE_SCREENSHOT %s" % output_path)
	return true


func _wait_for_actionable(board: Node) -> bool:
	for _frame in range(MAX_WAIT_FRAMES):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed while waiting for the initial actionable state")
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for the initial actionable state")
	return false


func _wait_for_successful_capture_reset(board: Node) -> bool:
	for _frame in range(MAX_WAIT_FRAMES):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed while waiting for successful capture reset")
			return false
		var stacks := int(board.call("DebugGetAdventureMissClimbGrowthStacks"))
		var processing := bool(board.call("DebugIsProcessingCardPlayForOwnershipProbe"))
		if stacks == 0 and not processing:
			return true
	_fail("Timed out waiting for successful capture to clear miss-climb stacks")
	return false


func _get_card_visual(board: Node, display_index: int) -> Control:
	return board.get_node_or_null(
		"%s/HandCard_%d/HandCardVisual" % [HAND_AREA_PATH, display_index]
	) as Control


func _get_badge(board: Node, display_index: int) -> Control:
	return board.get_node_or_null(
		"%s/HandCard_%d/HandCardVisual/%s" \
		% [HAND_AREA_PATH, display_index, BADGE_NODE_NAME]
	) as Control


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_CLUBS_MISS_CLIMB_BADGE_SMOKE_FAIL %s" % message)
	quit(1)
