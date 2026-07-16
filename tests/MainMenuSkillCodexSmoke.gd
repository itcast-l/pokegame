extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const POPUP_PREVIEW_SCENE_PATH := "res://scenes/upgrade_choice_popup_asset_scene.tscn"
const SHARED_CARD_SCENE_PATH := "res://scenes/adventure_upgrade_catalog_card.tscn"
const MENU_LAYOUT_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout"
const SKILL_BUTTON_PATH := MENU_LAYOUT_PATH + "/SkillCodexFrame/Button"
const OVERLAY_PATH := "MainMenuSkillCodexOverlay"
const VIEWPORT_SIZES := [Vector2i(960, 540), Vector2i(1280, 720), Vector2i(1920, 1080)]
const EXPECTED_COUNTS := {
	"hearts": 14,
	"diamonds": 13,
	"spades": 12,
	"clubs": 14,
}
const EXPECTED_TIERS := {
	"common": "common",
	"rare": "uncommon",
	"legendary": "rare",
}

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	var menu := await _instantiate_scene(MAIN_MENU_SCENE_PATH)
	if _failed or menu == null:
		return
	var skill_button := menu.get_node_or_null(SKILL_BUTTON_PATH) as Button
	var overlay := menu.get_node_or_null(OVERLAY_PATH)
	if skill_button == null or overlay == null:
		_fail("Main menu skill codex integration nodes are missing")
		return
	var avatar_layer := menu.get_node_or_null("AvatarEntryLayer") as CanvasLayer
	var exit_layer := menu.get_node_or_null("ExitDialogLayer") as CanvasLayer
	if avatar_layer == null or exit_layer == null \
			or int(overlay.get("layer")) <= avatar_layer.layer \
			or int(overlay.get("layer")) >= exit_layer.layer:
		_fail("Skill codex layer is not between avatar and exit-confirmation layers")
		return
	if not _assert_menu_entry_order(menu):
		return

	# Mouse command path.
	skill_button.emit_signal("pressed")
	await _wait_frames(3)
	if not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Skill codex did not open from the main-menu button")
		return
	if String(overlay.call("DebugGetCurrentSuit")) != "hearts":
		_fail("Skill codex did not default to hearts")
		return
	if int(overlay.call("DebugGetTotalEnabledCardCount")) != 53:
		_fail("Skill codex total enabled count is not 53")
		return

	var popup_regions := await _build_popup_region_map()
	if _failed:
		return
	for suit in EXPECTED_COUNTS:
		if not bool(overlay.call("DebugSelectSuit", suit)):
			_fail("Failed to select suit tab %s" % suit)
			return
		await _wait_frames(2)
		if int(overlay.call("DebugGetVisibleCardCount")) != int(EXPECTED_COUNTS[suit]):
			_fail("Unexpected %s card count" % suit)
			return
		if not _assert_visible_cards(overlay, suit, popup_regions):
			return

	if not bool(overlay.call("DebugSelectSuit", "diamonds")):
		_fail("Failed to select diamonds for question-interest audit")
		return
	var diamonds_states := overlay.call("DebugGetVisibleCardStates") as Array
	var found_interest := false
	for state_variant in diamonds_states:
		var state := state_variant as Dictionary
		if String(state["id"]) == "diamonds_question_mark_interest":
			found_interest = true
			var description := String(state["description"])
			if description.contains("{current_item_count}") or description.contains("{"):
				_fail("Question-mark interest description retained a placeholder")
				return
	if not found_interest:
		_fail("Question-mark interest entry is missing from the diamonds page")
		return

	if not bool(overlay.call("DebugActivateVisibleCard", 0)):
		_fail("Failed to open enlarged card preview")
		return
	if not bool(overlay.call("DebugIsPreviewVisible")):
		_fail("Enlarged card preview is not visible")
		return
	var preview_state := overlay.call("DebugGetPreviewCardState") as Dictionary
	if bool(preview_state["interactive"]):
		_fail("Enlarged preview card must be non-interactive")
		return
	if not bool(menu.call("DebugHandleSkillCodexCancel")):
		_fail("First cancel was not handled by the enlarged preview")
		return
	await process_frame
	if bool(overlay.call("DebugIsPreviewVisible")) or not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("First cancel did not close only the enlarged preview")
		return
	if not bool(menu.call("DebugHandleSkillCodexCancel")):
		_fail("Second cancel was not handled by the codex")
		return
	await _wait_frames(2)
	if bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Second cancel did not close the codex")
		return
	if root.gui_get_focus_owner() != skill_button:
		_fail("Focus did not return to the skill codex button")
		return

	# Keyboard confirm path.
	skill_button.grab_focus()
	var key_press := InputEventKey.new()
	key_press.keycode = KEY_ENTER
	key_press.pressed = true
	Input.parse_input_event(key_press)
	await _wait_frames(2)
	var key_release := InputEventKey.new()
	key_release.keycode = KEY_ENTER
	key_release.pressed = false
	Input.parse_input_event(key_release)
	await process_frame
	if not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Keyboard confirm did not open the codex")
		return
	menu.call("DebugHandleSkillCodexCancel")
	await _wait_frames(2)

	# Gamepad confirm path.
	skill_button.grab_focus()
	var joy_press := InputEventJoypadButton.new()
	joy_press.button_index = JOY_BUTTON_A
	joy_press.pressed = true
	Input.parse_input_event(joy_press)
	await _wait_frames(2)
	var joy_release := InputEventJoypadButton.new()
	joy_release.button_index = JOY_BUTTON_A
	joy_release.pressed = false
	Input.parse_input_event(joy_release)
	await process_frame
	if not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Gamepad confirm did not open the codex")
		return

	for viewport_size in VIEWPORT_SIZES:
		root.size = viewport_size
		await _wait_frames(3)
		if not _assert_responsive_layout(overlay, viewport_size):
			return
		if viewport_size == Vector2i(1280, 720) and not _capture_optional_screenshot():
			return

	menu.call("DebugHandleSkillCodexCancel")
	menu.queue_free()
	await process_frame
	print("MAIN_MENU_SKILL_CODEX_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _instantiate_scene(path: String) -> Node:
	var packed_scene := load(path) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % path)
		return null
	var scene := packed_scene.instantiate()
	root.add_child(scene)
	current_scene = scene
	await _wait_frames(3)
	return scene


func _assert_menu_entry_order(menu: Node) -> bool:
	var layout := menu.get_node(MENU_LAYOUT_PATH)
	var names: Array[String] = []
	for child in layout.get_children():
		names.append(child.name)
	var multiplayer_index := names.find("MultiplayerFrame")
	var codex_index := names.find("SkillCodexFrame")
	var settings_index := names.find("SettingsFrame")
	if multiplayer_index < 0 or codex_index != multiplayer_index + 1 or settings_index != codex_index + 1:
		_fail("Skill codex entry is not between Multiplayer and Settings")
		return false
	var frame := layout.get_node("SkillCodexFrame") as Control
	if not frame.custom_minimum_size.is_equal_approx(Vector2(374, 64)):
		_fail("Skill codex frame does not preserve the 374x64 menu size")
		return false
	return true


func _build_popup_region_map() -> Dictionary:
	var packed_scene := load(POPUP_PREVIEW_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load popup preview scene")
		return {}
	var preview := packed_scene.instantiate()
	root.add_child(preview)
	await _wait_frames(2)
	var regions := {}
	var cards: Array[Node] = []
	for candidate in preview.find_children("*", "Button", true, false):
		if candidate.has_method("DebugGetCardState"):
			cards.append(candidate)
	if cards.size() != 12:
		_fail("Popup preview does not contain 12 shared card instances")
		return {}
	for card in cards:
		var state := card.call("DebugGetCardState") as Dictionary
		var key := "%s::%s" % [state["suit"], state["visual_tier"]]
		regions[key] = state["atlas_region"]
		if bool(state["interactive"]):
			_fail("Popup preview cards must be non-interactive")
			return {}
	preview.queue_free()
	await process_frame
	return regions


func _assert_visible_cards(overlay: Node, suit: String, popup_regions: Dictionary) -> bool:
	var states := overlay.call("DebugGetVisibleCardStates") as Array
	var previous_sort_order := -2147483648
	for state_variant in states:
		var state := state_variant as Dictionary
		if String(state["suit"]) != suit:
			_fail("Codex page contains a card from the wrong suit")
			return false
		var sort_order := int(state["sort_order"])
		if sort_order < previous_sort_order:
			_fail("Codex cards are not sorted by SortOrder")
			return false
		previous_sort_order = sort_order
		var rarity := String(state["rarity"])
		if not EXPECTED_TIERS.has(rarity) or String(state["visual_tier"]) != String(EXPECTED_TIERS[rarity]):
			_fail("Rarity-to-visual-tier mapping is incorrect for %s" % state["id"])
			return false
		if String(state["rarity_label"]).begins_with("SKILL_CODEX_"):
			_fail("Rarity label was not localized for %s" % state["id"])
			return false
		var description := String(state["description"])
		if description.is_empty() or description.contains("{") or description.begins_with("UPGRADE_DESC_"):
			_fail("Invalid formatted description for %s: %s" % [state["id"], description])
			return false
		if String(state["scene_file_path"]) != SHARED_CARD_SCENE_PATH:
			_fail("Codex entry is not an instance of the shared card scene")
			return false
		var popup_key := "%s::%s" % [suit, state["visual_tier"]]
		if not popup_regions.has(popup_key) or not (state["atlas_region"] as Rect2).is_equal_approx(popup_regions[popup_key] as Rect2):
			_fail("Codex and popup AtlasTexture regions differ for %s" % popup_key)
			return false
		var region := state["atlas_region"] as Rect2
		var card := overlay.get_node("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid/UpgradeCard_%s" % state["id"]) as Control
		if region.size.y <= 0.0 or absf(card.size.x / card.size.y - region.size.x / region.size.y) > 0.005:
			_fail("Catalog card does not preserve AtlasTexture aspect ratio for %s card=%s region=%s" % [state["id"], card.size, region.size])
			return false
	return true


func _assert_responsive_layout(overlay: Node, viewport_size: Vector2i) -> bool:
	var scroll := overlay.get_node("Root/SafeMargin/Layout/CardScroll") as ScrollContainer
	var grid := overlay.get_node("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid") as GridContainer
	var available_width := maxf(0.0, scroll.size.x - 48.0)
	var expected_columns := clampi(floori((available_width + 16.0) / 212.0), 2, 4)
	if grid.columns != expected_columns:
		_fail("Grid column formula mismatch at %s" % viewport_size)
		return false
	if viewport_size == Vector2i(1280, 720) and grid.columns != 4:
		_fail("1280x720 skill codex must use four columns")
		return false
	if grid.size.x > scroll.size.x + 1.5:
		_fail("Skill codex grid causes horizontal overflow at %s" % viewport_size)
		return false
	if scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED:
		_fail("Skill codex horizontal scrolling is enabled")
		return false
	return true


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _capture_optional_screenshot() -> bool:
	var output_path := OS.get_environment("SKILL_CODEX_SCREENSHOT_PATH").strip_edges()
	if output_path.is_empty():
		return true
	var image := root.get_texture().get_image()
	if image == null or image.is_empty() or image.get_used_rect().size == Vector2i.ZERO:
		_fail("Skill codex viewport screenshot is empty")
		return false
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save skill codex screenshot: %s" % error_string(save_error))
		return false
	return true


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("MAIN_MENU_SKILL_CODEX_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
