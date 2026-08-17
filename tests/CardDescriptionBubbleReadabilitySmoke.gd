extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const MAIN_HAND_AREA_PATH := "BoardCanvas/LayoutAreas/Player1HandArea"
const ENDPOINT_CROWN_ITEM_ID := "endpoint_crown"
const TIMEOUT_SECONDS := 20.0
const COMPACT_VIEWPORT_SIZE := Vector2i(450, 236)
const NATIVE_VIEWPORT_SIZE := Vector2i(1280, 720)

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_mute_audio()
	TranslationServer.set_locale("zh_CN")
	root.size = NATIVE_VIEWPORT_SIZE \
		if OS.get_environment("CODEX_CARD_DESCRIPTION_NATIVE_VIEWPORT") == "1" \
		else COMPACT_VIEWPORT_SIZE

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load game board scene")
		return

	var board := GameBoardSmokeFactory.create_adventure(packed_scene, "shrimp")
	root.add_child(board)
	await process_frame
	await process_frame

	for method in [
		"DebugConfigureAdventureTriggeredAttachmentMissScenario",
		"DebugAttachItemEffectToMainPlayerHandCard",
		"DebugGetGameLoopPhaseName",
		"DebugIsAdventureUpgradeSelectionVisible",
		"DebugDisposeActiveSelectionOverlayForAwaitProbe",
	]:
		if not board.has_method(method):
			_fail("Missing card description debug hook: %s" % method)
			return

	if not await _wait_for_actionable_state(board):
		return
	if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
		board.call("DebugDisposeActiveSelectionOverlayForAwaitProbe")
		for _frame in range(4):
			await process_frame

	if not bool(board.call("DebugConfigureAdventureTriggeredAttachmentMissScenario")):
		_fail("Failed to configure attached-card table fixture")
		return
	if not bool(board.call(
		"DebugAttachItemEffectToMainPlayerHandCard",
		0,
		ENDPOINT_CROWN_ITEM_ID
	)):
		_fail("Failed to attach endpoint crown to the fixture card")
		return
	for _frame in range(3):
		await process_frame

	var hand_area := board.get_node_or_null(MAIN_HAND_AREA_PATH) as Control
	if hand_area == null:
		_fail("Missing main hand area")
		return
	var hover_card := hand_area.get_node_or_null("HandCard_0") as Control
	var bubble: Control = null
	if hover_card != null:
		bubble = hover_card.get_node_or_null("CardDescriptionBubble") as Control
	if bubble == null or hover_card == null:
		_fail("Attached hand card did not create its description bubble")
		return

	hover_card.emit_signal("mouse_entered")
	for _frame in range(3):
		await process_frame
	if not _verify_bubble(bubble):
		return
	if not await _save_optional_screenshot(board):
		return

	hover_card.emit_signal("mouse_exited")
	await process_frame
	if bubble.visible:
		_fail("Mouse exit left the card description visible")
		return

	print("CARD_DESCRIPTION_BUBBLE_READABILITY_SMOKE_OK")
	board.queue_free()
	for _frame in range(12):
		await process_frame
	await _dispose_audio_before_quit()
	quit(0)


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for the initial player action")
	return false


func _verify_bubble(bubble: Control) -> bool:
	if not bubble.visible:
		_fail("Hover did not reveal the card description bubble")
		return false
	if bubble.size.x < 540.0 or bubble.size.y < 72.0:
		_fail("Card description bubble is too small: %s" % bubble.size)
		return false

	if not bool(bubble.get("SuppressVisuals")):
		_fail("Card description must not draw a background window")
		return false

	var label := bubble.get_node_or_null("CardDescriptionLabel") as Label
	if label == null or label.text != tr("ITEM_DESC_ENDPOINT_CROWN"):
		_fail("Card description bubble does not contain the complete localized text")
		return false
	if label.get_theme_font_size("font_size") < 20 \
			or label.get_theme_constant("outline_size") < 2 \
			or label.get_theme_constant("shadow_outline_size") < 1:
		_fail("Card description typography is below the readability baseline")
		return false
	var font_color := label.get_theme_color("font_color")
	var font_luminance := font_color.r * 0.2126 + font_color.g * 0.7152 + font_color.b * 0.0722
	if font_luminance < 0.9:
		_fail("Card description text is not bright enough against the dark surface")
		return false

	var board_canvas := bubble.get_node_or_null("../../../..") as Control
	var bubble_rect := bubble.get_global_rect()
	if board_canvas == null or not board_canvas.get_global_rect().encloses(bubble_rect):
		_fail("Card description bubble leaves the board canvas: %s" % bubble_rect)
		return false
	return true


func _save_optional_screenshot(board: Node) -> bool:
	var path := OS.get_environment("CODEX_CARD_DESCRIPTION_SCREENSHOT_PATH")
	if path.is_empty():
		return true
	await process_frame
	await RenderingServer.frame_post_draw
	var image := board.get_viewport().get_texture().get_image()
	if image == null or image.is_empty():
		_fail("Card description screenshot is empty")
		return false
	var save_error := image.save_png(path)
	if save_error != OK:
		_fail("Failed to save card description screenshot: %s" % error_string(save_error))
		return false
	return true


func _mute_audio() -> void:
	for bus_index in range(AudioServer.bus_count):
		AudioServer.set_bus_mute(bus_index, true)


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
	push_error("CARD_DESCRIPTION_BUBBLE_READABILITY_SMOKE_FAIL %s" % message)
	quit(1)
