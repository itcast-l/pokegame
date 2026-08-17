extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "shrimp")
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	root.add_child(board)
	await process_frame

	if not board.has_method("DebugShowAdventureQuestionRejudgeOverlaySample"):
		_fail("Missing direct rejudge overlay debug hook")
		return

	if not bool(board.call("DebugShowAdventureQuestionRejudgeOverlaySample")):
		_fail("Failed to show direct rejudge overlay sample")
		return

	await process_frame
	await process_frame

	var overlay := board.get_node_or_null("AdventureQuestionRejudgeOverlay")
	if overlay == null:
		_fail("Expected direct question rejudge overlay scene")
		return

	var reward_preview := overlay.get_node_or_null("RewardPreview") as Control
	if reward_preview == null:
		_fail("Expected question rejudge overlay reward preview root")
		return

	if reward_preview is PanelContainer:
		_fail("Question rejudge overlay should not keep the old square reward preview panel")
		return

	var reward_card := overlay.get_node_or_null("RewardPreview/RewardMargin/RewardCard") as TextureRect
	if reward_card == null or reward_card.texture == null:
		_fail("Expected question rejudge overlay to show a reward card preview")
		return

	var reward_effect_label := overlay.get_node_or_null("RewardPreview/RewardMargin/RewardOverlay/RewardEffectPanel/RewardEffectMargin/RewardEffectLabel") as Label
	if reward_effect_label == null:
		_fail("Expected question rejudge overlay to show the current reward effect text")
		return

	if String(reward_effect_label.text).strip_edges().is_empty():
		_fail("Expected question rejudge overlay current reward effect text to be populated")
		return

	var expected_effect := tr("ITEM_DESC_OVERTIME")
	if expected_effect != "ITEM_DESC_OVERTIME" and reward_effect_label.text != expected_effect:
		_fail("Expected question rejudge overlay current reward effect text to match the reward")
		return

	if reward_preview.size.x > 280.0:
		_fail("Question rejudge overlay reward preview should stay compact")
		return

	var prompt_panel := overlay.get_node_or_null("PromptPanel") as Control
	if prompt_panel == null:
		_fail("Expected question rejudge overlay prompt panel")
		return

	if prompt_panel.size.x > 500.0 or prompt_panel.size.y > 170.0:
		_fail("Question rejudge overlay prompt strip should stay smaller than the first implementation")
		return

	var prompt_label := overlay.get_node_or_null("PromptPanel/PromptMargin/PromptLayout/PromptLabel") as Label
	if prompt_label == null or String(prompt_label.text).strip_edges().is_empty():
		_fail("Expected question rejudge overlay prompt text")
		return

	var keep_button := overlay.get_node_or_null("PromptPanel/PromptMargin/PromptLayout/ButtonsRow/KeepButton") as Button
	var reroll_button := overlay.get_node_or_null("PromptPanel/PromptMargin/PromptLayout/ButtonsRow/RerollButton") as Button
	if keep_button == null or reroll_button == null:
		_fail("Expected question rejudge overlay decision buttons")
		return

	if not keep_button.visible or not reroll_button.visible:
		_fail("Expected question rejudge overlay decision buttons to be visible")
		return

	var cancel_button := overlay.get_node_or_null("PromptPanel/PromptMargin/PromptLayout/ButtonsRow/CancelButton")
	if cancel_button != null:
		_fail("Question rejudge overlay should not keep a third large cancel button")
		return

	keep_button.emit_signal("pressed")
	await process_frame
	await process_frame

	if board.get_node_or_null("AdventureQuestionRejudgeOverlay") != null:
		_fail("Expected question rejudge overlay to dismiss after selection")
		return

	board.queue_free()
	for _frame in range(12):
		await process_frame

	await _run_rejudge_await_completion_phase()
	if _failed:
		return

	print("ADVENTURE_QUESTION_REJUDGE_OVERLAY_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_QUESTION_REJUDGE_OVERLAY_SMOKE_FAIL %s" % message)
	quit(1)


func _run_rejudge_await_completion_phase() -> void:
	for terminal in ["select", "cancel", "dispose", "replace", "restart", "exit"]:
		await _run_rejudge_await_terminal(terminal)
		if _failed:
			return


func _run_rejudge_await_terminal(terminal: String) -> void:
	var board := await _instantiate_probe_board()
	if board == null:
		return
	for method_name in [
		"DebugStartAdventureQuestionRejudgeAwaitProbe",
		"DebugReplaceAdventureQuestionRejudgeAwaitProbe",
		"DebugDisposeActiveSelectionOverlayForAwaitProbe",
		"DebugCancelActiveSelectionOverlayForAwaitProbe",
		"DebugRestartMatchForSelectionAwaitProbe",
		"DebugSelectAdventureQuestionRejudgeOption",
	]:
		if not board.has_method(method_name):
			_fail("Missing rejudge await probe hook: %s" % method_name)
			return

	var result_a := {}
	if not bool(board.call("DebugStartAdventureQuestionRejudgeAwaitProbe", result_a)):
		_fail("Failed to start rejudge await probe for %s" % terminal)
		return
	if not await _wait_for_overlay(board):
		return

	if terminal == "select":
		if not bool(board.call("DebugSelectAdventureQuestionRejudgeOption", 1)):
			_fail("Failed to select rejudge reroll option")
			return
		if await _wait_for_result(result_a):
			_assert_rejudge_result(result_a, "Reroll", false, false, terminal)
		await process_frame
		if int(result_a.get("completion_count", -1)) != 1:
			_fail("Rejudge select completion was not idempotent")
	elif terminal == "cancel":
		if not bool(board.call("DebugCancelActiveSelectionOverlayForAwaitProbe")):
			_fail("Failed to cancel rejudge await probe")
			return
		if await _wait_for_result(result_a):
			_assert_rejudge_result(result_a, "Keep", false, false, terminal)
	elif terminal == "dispose":
		if not bool(board.call("DebugDisposeActiveSelectionOverlayForAwaitProbe")):
			_fail("Failed to dispose rejudge await probe")
			return
		if await _wait_for_result(result_a):
			_assert_rejudge_result(result_a, "Keep", true, true, terminal)
	elif terminal == "replace":
		var result_b := {}
		if not bool(board.call("DebugReplaceAdventureQuestionRejudgeAwaitProbe", result_b)):
			_fail("Failed to replace rejudge await probe")
			return
		for _frame in range(2):
			await process_frame
		if not bool(result_a.get("completed", false)):
			_fail("Replaced rejudge await probe A did not complete within two frames")
			return
		_assert_rejudge_result(result_a, "Keep", true, true, terminal + " A")
		if bool(result_b.get("completed", false)):
			_fail("Replacement rejudge await probe B completed unexpectedly")
			return
		if not bool(board.call("DebugCancelActiveSelectionOverlayForAwaitProbe")):
			_fail("Failed to cancel replacement rejudge await probe B")
			return
		if await _wait_for_result(result_b):
			_assert_rejudge_result(result_b, "Keep", false, false, terminal + " B")
	elif terminal == "restart":
		if not bool(board.call("DebugRestartMatchForSelectionAwaitProbe")):
			_fail("Failed to restart match with rejudge await probe")
			return
		if await _wait_for_result(result_a):
			_assert_rejudge_result(result_a, "Keep", true, true, terminal)
	elif terminal == "exit":
		board.queue_free()
		if await _wait_for_result(result_a):
			_assert_rejudge_result(result_a, "Keep", true, true, terminal)

	if is_instance_valid(board):
		board.queue_free()
	for _frame in range(12):
		await process_frame


func _instantiate_probe_board() -> Node:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load probe scene: %s" % SCENE_PATH)
		return null
	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "shrimp")
	if board == null:
		_fail("Failed to instantiate rejudge await probe board")
		return null
	root.add_child(board)
	await process_frame
	return board


func _wait_for_overlay(board: Node) -> bool:
	for _frame in range(120):
		await process_frame
		if not is_instance_valid(board):
			_fail("Board freed while waiting for rejudge await overlay")
			return false
		if board.get_node_or_null("AdventureQuestionRejudgeOverlay") != null:
			return true
	_fail("Timed out waiting for rejudge await overlay")
	return false


func _wait_for_result(result: Dictionary) -> bool:
	for _frame in range(120):
		await process_frame
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for rejudge await result")
	return false


func _assert_rejudge_result(result: Dictionary, expected_choice: String, empty_rect: bool, null_overlay: bool, context: String) -> void:
	if String(result.get("error", "")) != "" \
		or int(result.get("completion_count", -1)) != 1 \
		or String(result.get("choice", "")) != expected_choice \
		or bool(result.get("rect_is_empty", not empty_rect)) != empty_rect \
		or bool(result.get("overlay_is_null", not null_overlay)) != null_overlay:
		_fail("Unexpected rejudge await result for %s: %s" % [context, result])
