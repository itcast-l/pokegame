extends SceneTree

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

	print("ADVENTURE_QUESTION_REJUDGE_OVERLAY_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_QUESTION_REJUDGE_OVERLAY_SMOKE_FAIL %s" % message)
	quit(1)
