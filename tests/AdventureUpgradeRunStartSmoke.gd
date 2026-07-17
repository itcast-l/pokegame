extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 20.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return
	var board := packed_scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	root.add_child(board)
	await process_frame

	for method in [
		"DebugIsAdventureUpgradeSelectionVisible",
		"DebugGetAdventureUpgradeOptionCount",
		"DebugSelectAdventureUpgradeOption",
		"DebugGetAdventureAcquiredUpgradeCount",
		"DebugGetGameLoopPhaseName",
	]:
		if not board.has_method(method):
			_fail("Missing adventure upgrade debug hook: %s" % method)
			return
	if not await _wait_for_upgrade_selection(board):
		return

	var overlay := board.find_child("AdventureUpgradeSelectionOverlay", true, false)
	if overlay == null:
		_fail("Expected adventure upgrade selection overlay")
		return
	var header := overlay.get_node_or_null("Margin/Layout/Header") as Control
	var table_backdrop := overlay.get_node_or_null("TableBackdrop") as TextureRect
	var table_darken := overlay.get_node_or_null("TableDarken") as ColorRect
	var selection_panel := overlay.get_node_or_null("Margin/SelectionPanel") as Control
	var selection_halo := overlay.get_node_or_null("Margin/SelectionHalo") as Control
	var selection_title := overlay.get_node_or_null("Margin/Layout/Header/SelectionTitle") as Label
	if header == null or not header.visible or selection_title == null or selection_title.text.is_empty():
		_fail("Adventure upgrade overlay must show its localized heading")
		return
	if table_backdrop == null or table_backdrop.texture == null \
			or table_backdrop.stretch_mode != TextureRect.STRETCH_KEEP_ASPECT_COVERED:
		_fail("Adventure upgrade overlay is missing the covered table backdrop")
		return
	if table_darken == null or absf(table_darken.color.a - 0.30) > 0.01:
		_fail("Adventure upgrade table darken layer must preserve the bright tabletop")
		return
	if selection_panel == null or selection_halo == null or selection_panel.visible or selection_halo.visible:
		_fail("Legacy selection panel and halo must stay hidden")
		return

	var option_count := int(board.call("DebugGetAdventureUpgradeOptionCount"))
	if option_count < 1 or option_count > 3:
		_fail("Expected 1-3 run-start options")
		return
	var visible_count := 0
	for option_index in range(3):
		var button := overlay.get_node_or_null("Margin/Layout/CardsRow/Option%dButton" % (option_index + 1)) as Button
		if button == null:
			_fail("Missing adventure upgrade option %d" % option_index)
			return
		if not button.visible:
			continue
		visible_count += 1
		if not button.text.is_empty() or button.custom_minimum_size != Vector2(272, 404):
			_fail("Adventure upgrade option button contract changed")
			return
		var art := button.get_node_or_null("CardFrame/ChoiceArt") as Control
		if art == null \
				or not art.has_method("ConfigureChoice") \
				or not art.has_method("DebugGetCardVisualState"):
			_fail("Adventure upgrade choice art is missing required flat-card interfaces")
			return
		if String(art.call("DebugGetDescriptionText")).is_empty() \
				or String(art.call("DebugGetRarityText")).is_empty():
			_fail("Choice art must show description and localized rarity")
			return

		var card_rect := art.call("DebugGetCardDrawRect") as Dictionary
		var description_rect := art.call("DebugGetDescriptionBand") as Dictionary
		var card_height := float(card_rect.get("height", 0.0))
		var card_y := float(card_rect.get("y", 0.0))
		var description_y := float(description_rect.get("y", 0.0))
		var description_height := float(description_rect.get("height", 0.0))
		if card_height <= 0.0:
			_fail("Choice art reported an invalid card rect")
			return
		var top_ratio := (description_y - card_y) / card_height
		var bottom_ratio := (description_y + description_height - card_y) / card_height
		if absf(top_ratio - 0.545) > 0.002 or absf(bottom_ratio - 0.725) > 0.002:
			_fail("Description layer must leave room for the rarity footer")
			return
		if int(art.call("DebugGetResolvedDescriptionFontSize")) < 10:
			_fail("Description font fell below the supported minimum")
			return

		var visual := art.call("DebugGetCardVisualState") as Dictionary
		if not _assert_flat_card(visual):
			return

	if visible_count != option_count:
		_fail("Visible option count does not match the offer")
		return
	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 0:
		_fail("Run already has an upgrade before selection")
		return
	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to select the first run-start upgrade")
		return
	if not await _wait_for_actionable_state(board):
		return
	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Run-start selection did not acquire exactly one upgrade")
		return

	board.queue_free()
	await process_frame
	print("ADVENTURE_UPGRADE_RUN_START_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)



func _assert_flat_card(visual: Dictionary) -> bool:
	if not bool(visual.get("surface_is_flat", false)) \
			or int(visual.get("thickness_layer_count", -1)) != 0 \
			or String(visual.get("surface_shader_path", "")) != "res://shaders/adventure_upgrade_card_cleanup.gdshader":
		_fail("Choice card must use a flat surface without perspective or thickness layers")
		return false
	if int(visual.get("surface_material_id", 0)) == 0 \
			or not bool(visual.get("glow_uses_outline_panel", false)) \
			or float(visual.get("description_panel_alpha", 0.0)) < 0.8:
		_fail("Flat card surface, outline glow, or description panel is incomplete")
		return false
	return true


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true
	_fail("Timed out waiting for run-start upgrade selection")
	return false


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not bool(board.call("DebugIsAdventureUpgradeSelectionVisible")) \
				and String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Timed out waiting for actionable state")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_RUN_START_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(8):
		await process_frame
