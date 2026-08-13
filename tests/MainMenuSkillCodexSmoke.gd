extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const POPUP_PREVIEW_SCENE_PATH := "res://scenes/upgrade_choice_popup_asset_scene.tscn"
const SHARED_CARD_SCENE_PATH := "res://scenes/adventure_upgrade_catalog_card.tscn"
const ITEM_CARD_SCENE_PATH := "res://scenes/main_menu_item_codex_card.tscn"
const MENU_LAYOUT_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout"
const SKILL_BUTTON_PATH := MENU_LAYOUT_PATH + "/SkillCodexFrame/Button"
const OVERLAY_PATH := "MainMenuSkillCodexOverlay"
const EMPTY_PROFILE_SLOT := &"profile_0"
const UNLOCKED_PROFILE_SLOT := &"profile_1"
const VIEWPORT_SIZES := [Vector2i(960, 540), Vector2i(1280, 720), Vector2i(1920, 1080)]
const EXPECTED_ITEM_COUNT := 15
const CATALOG_CARD_SIZE := Vector2(196.0, 322.0)
const CATALOG_CARD_GAP := 16.0
const MINIMUM_ITEM_CARD_HEIGHT := 248.0
const MAXIMUM_VISIBLE_ITEM_ROWS := 3
const EXPECTED_COUNTS := {
	"hearts": 14,
	"diamonds": 13,
	"spades": 12,
	"clubs": 13,
}
const EXPECTED_TIERS := {
	"common": "common",
	"rare": "uncommon",
	"legendary": "rare",
}
const UNLOCKED_BY_SUIT := {
	"hearts": "hearts_thick_blood",
	"diamonds": "diamonds_question_mark_interest",
	"spades": "spades_dark_harbor_reveal",
	"clubs": "clubs_gap_rooting",
}
const SUIT_BUTTON_NAMES := {
	"hearts": "HeartsButton",
	"diamonds": "DiamondsButton",
	"spades": "SpadesButton",
	"clubs": "ClubsButton",
}
const SUIT_BUTTON_SYMBOLS := {
	"hearts": "♥",
	"diamonds": "♦",
	"spades": "♠",
	"clubs": "♣",
}
const SUIT_BORDER_COLORS := {
	"hearts": Color(0.76, 0.27, 0.43, 0.42),
	"diamonds": Color(0.35, 0.76, 0.84, 0.42),
	"spades": Color(0.57, 0.68, 0.82, 0.42),
	"clubs": Color(0.30, 0.72, 0.49, 0.42),
}

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	var save_manager := await _configure_profiles()
	if _failed or save_manager == null:
		return
	var menu := await _instantiate_scene(MAIN_MENU_SCENE_PATH)
	if _failed or menu == null:
		return
	var skill_button := menu.get_node_or_null(SKILL_BUTTON_PATH) as Button
	var overlay := menu.get_node_or_null(OVERLAY_PATH)
	if skill_button == null or overlay == null:
		_fail("Main menu skill codex integration nodes are missing")
		return
	if not bool(overlay.get("UnlockAllForTesting")):
		_fail("Skill codex testing unlock parameter is not enabled in the scene")
		return
	overlay.set("UnlockAllForTesting", false)
	var avatar_layer := menu.get_node_or_null("AvatarEntryLayer") as CanvasLayer
	var exit_layer := menu.get_node_or_null("ExitDialogLayer") as CanvasLayer
	if avatar_layer == null or exit_layer == null \
			or int(overlay.get("layer")) <= avatar_layer.layer \
			or int(overlay.get("layer")) >= exit_layer.layer:
		_fail("Skill codex layer is not between avatar and exit-confirmation layers")
		return
	if not _assert_menu_entry_order(menu):
		return

	var popup_regions := await _build_popup_region_map()
	if _failed:
		return

	# A new profile must expose all 52 fixed slots without revealing any skill.
	skill_button.emit_signal("pressed")
	if not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Skill codex did not open from the main-menu button")
		return
	if not bool(menu.call("DebugIsSkillCodexTransitioning")):
		_fail("Skill codex entrance did not acquire its transition lock")
		return
	if bool(menu.call("DebugIsMainMenuAvatarVisible")):
		_fail("Main-menu avatar remained visible during the skill codex entrance")
		return
	if not bool(menu.call("DebugAreMainMenuButtonsDisabled")):
		_fail("Main-menu buttons were not locked during the skill codex entrance")
		return
	if bool(overlay.get("follow_viewport_enabled")):
		_fail("Skill codex CanvasLayer must remain screen-space during the camera transition")
		return
	var push_camera := menu.get_node_or_null("MenuCameraPushIn")
	if push_camera == null or int(push_camera.call("get_priority")) != 20:
		_fail("Skill codex entrance did not activate the push-in camera")
		return
	if not await _assert_skill_codex_transition_geometry(menu, overlay):
		return
	if not await _wait_for_skill_transition(menu, true):
		return
	var transition_root := overlay.get_node("Root") as Control
	if not is_equal_approx(transition_root.modulate.a, 1.0):
		_fail("Skill codex did not finish fading in")
		return
	if String(overlay.call("DebugGetCurrentSuit")) != "hearts":
		_fail("Skill codex did not default to hearts")
		return
	if not await _assert_unified_visual_style(overlay):
		return
	if String(overlay.call("DebugGetCurrentCategory")) != "upgrades" \
			or not bool(overlay.call("DebugAreSuitTabsVisible")):
		_fail("Codex did not default to the upgrades category with suit filters")
		return
	if not bool(overlay.call("DebugSelectCategory", "items")):
		_fail("Failed to select the item category")
		return
	await _wait_frames(2)
	if String(overlay.call("DebugGetCurrentCategory")) != "items" \
			or bool(overlay.call("DebugAreSuitTabsVisible")):
		_fail("Item category did not replace the suit filter row")
		return
	if int(overlay.call("DebugGetTotalEnabledItemCardCount")) != EXPECTED_ITEM_COUNT \
			or int(overlay.call("DebugGetVisibleCardCount")) != EXPECTED_ITEM_COUNT:
		_fail("Item category did not expose all enabled item cards")
		return
	if not _assert_item_cards(overlay):
		return
	for viewport_size in VIEWPORT_SIZES:
		root.size = viewport_size
		await _wait_frames(3)
		if not _assert_responsive_layout(overlay, viewport_size):
			return
	if not await _assert_item_cards_scroll_into_view(overlay):
		return
	root.size = Vector2i(1280, 720)
	await _wait_frames(3)
	if not _capture_optional_item_screenshot():
		return
	if not bool(overlay.call("DebugActivateVisibleCard", 0)):
		_fail("Item card did not open its enlarged preview")
		return
	await process_frame
	var item_preview_state := overlay.call("DebugGetPreviewCardState") as Dictionary
	if not bool(overlay.call("DebugIsPreviewVisible")) \
			or String(item_preview_state["kind"]) != "items" \
			or bool(item_preview_state["interactive"]) \
			or not bool(item_preview_state["has_texture"]):
		_fail("Item preview did not preserve the card as a non-interactive enlarged view")
		return
	if not _capture_optional_item_preview_screenshot():
		return
	if not bool(menu.call("DebugHandleSkillCodexCancel")):
		_fail("Item preview did not handle the first cancel")
		return
	await process_frame
	if bool(overlay.call("DebugIsPreviewVisible")) \
			or String(overlay.call("DebugGetCurrentCategory")) != "items":
		_fail("Closing the item preview also closed or changed the codex category")
		return
	if not bool(overlay.call("DebugSelectCategory", "upgrades")):
		_fail("Failed to return to the upgrades category")
		return
	await _wait_frames(2)
	if not bool(overlay.call("DebugAreSuitTabsVisible")):
		_fail("Returning to upgrades did not restore the suit filters")
		return
	if int(overlay.call("DebugGetTotalEnabledCardCount")) != 52:
		_fail("Skill codex total enabled count is not 52")
		return
	if int(overlay.call("DebugGetTotalUnlockedCardCount")) != 0:
		_fail("New profile skill codex is not fully locked")
		return
	for suit in EXPECTED_COUNTS:
		if not bool(overlay.call("DebugSelectSuit", suit)):
			_fail("Failed to select suit tab %s" % suit)
			return
		await _wait_frames(2)
		if int(overlay.call("DebugGetVisibleCardCount")) != int(EXPECTED_COUNTS[suit]):
			_fail("Unexpected %s card count" % suit)
			return
		if int(overlay.call("DebugGetCurrentSuitUnlockedCardCount")) != 0:
			_fail("New profile unexpectedly unlocked a %s card" % suit)
			return
		if not _assert_locked_cards(overlay):
			return
		if root.gui_get_focus_owner() != overlay.get_node("Root/SafeMargin/Layout/TopBar/TopBarLayout/BackButton"):
			_fail("All-locked %s page did not focus the back button" % suit)
			return
	if bool(overlay.call("DebugActivateVisibleCard", 0)) \
			or bool(overlay.call("DebugIsPreviewVisible")):
		_fail("Locked skill card accepted activation or opened a preview")
		return
	if not bool(menu.call("DebugHandleSkillCodexCancel")) \
			or not await _wait_for_skill_transition(menu, false):
		return
	if root.gui_get_focus_owner() != skill_button:
		_fail("Focus did not return after closing the all-locked codex")
		return

	# Opening after an active-profile switch must reload the four persisted unlocks.
	if not _set_active_profile(save_manager, UNLOCKED_PROFILE_SLOT):
		return
	skill_button.emit_signal("pressed")
	if not await _wait_for_skill_transition(menu, true):
		return
	if int(overlay.call("DebugGetTotalUnlockedCardCount")) != UNLOCKED_BY_SUIT.size():
		_fail("Unlocked profile did not refresh all persisted codex entries")
		return
	if bool(overlay.call("DebugIsInputFocusVisible")):
		_fail("Pointer-opened skill codex showed keyboard focus before navigation input")
		return
	if not _assert_card_focus_highlight_state(overlay, false):
		return

	var navigation_press := InputEventKey.new()
	navigation_press.keycode = KEY_RIGHT
	navigation_press.pressed = true
	Input.parse_input_event(navigation_press)
	await _wait_frames(2)
	if not bool(overlay.call("DebugIsInputFocusVisible")):
		_fail("Keyboard navigation did not reveal skill codex focus feedback")
		return
	if not _assert_card_focus_highlight_state(overlay, true):
		return
	var navigation_release := InputEventKey.new()
	navigation_release.keycode = KEY_RIGHT
	navigation_release.pressed = false
	Input.parse_input_event(navigation_release)

	var pointer_motion := InputEventMouseMotion.new()
	pointer_motion.relative = Vector2(1.0, 0.0)
	Input.parse_input_event(pointer_motion)
	await _wait_frames(2)
	if bool(overlay.call("DebugIsInputFocusVisible")):
		_fail("Pointer input did not hide skill codex keyboard focus feedback")
		return
	if not _assert_card_focus_highlight_state(overlay, false):
		return
	for suit in EXPECTED_COUNTS:
		if not bool(overlay.call("DebugSelectSuit", suit)):
			_fail("Failed to select unlocked profile suit tab %s" % suit)
			return
		await _wait_frames(2)
		if int(overlay.call("DebugGetVisibleCardCount")) != int(EXPECTED_COUNTS[suit]):
			_fail("Unlocked profile changed the fixed %s card count" % suit)
			return
		if not _assert_mixed_cards(overlay, suit, String(UNLOCKED_BY_SUIT[suit]), popup_regions):
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

	var interest_index := _find_visible_card_index(overlay, "diamonds_question_mark_interest")
	if interest_index < 0 or not bool(overlay.call("DebugActivateVisibleCard", interest_index)):
		_fail("Failed to open enlarged card preview")
		return
	if not bool(overlay.call("DebugIsPreviewVisible")):
		_fail("Enlarged card preview is not visible")
		return
	var preview_state := overlay.call("DebugGetPreviewCardState") as Dictionary
	if bool(preview_state["interactive"]) or bool(preview_state["locked"]) \
			or not bool(preview_state["has_texture"]) \
			or not String(preview_state["rarity_label"]).is_empty():
		_fail("Enlarged preview did not preserve unlocked card content as non-interactive")
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
	await process_frame
	if not bool(menu.call("DebugIsSkillCodexTransitioning")) \
			or not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Skill codex exit did not remain visible under its transition lock")
		return
	if not bool(menu.call("DebugIsMainMenuAvatarVisible")):
		_fail("Main-menu avatar was not restored during the skill codex exit")
		return
	if bool(menu.call("DebugHandleSkillCodexCancel")):
		_fail("Repeated cancel was accepted during the skill codex exit")
		return
	var home_camera := menu.get_node_or_null("MenuCameraHome")
	if home_camera == null or int(home_camera.call("get_priority")) != 10 \
			or int(push_camera.call("get_priority")) != 0:
		_fail("Skill codex exit did not restore the main-menu camera")
		return
	if not await _wait_for_skill_transition(menu, false):
		return
	if root.gui_get_focus_owner() != skill_button:
		_fail("Focus did not return to the skill codex button")
		return
	if bool(menu.call("DebugAreMainMenuButtonsDisabled")):
		_fail("Main-menu buttons remained disabled after the skill codex exit")
		return

	# Keyboard open reloads the empty active profile.
	if not _set_active_profile(save_manager, EMPTY_PROFILE_SLOT):
		return
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
	if not await _wait_for_skill_transition(menu, true):
		return
	if not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Keyboard confirm did not open the codex")
		return
	if int(overlay.call("DebugGetTotalUnlockedCardCount")) != 0:
		_fail("Keyboard reopen did not refresh the empty active profile")
		return
	if not bool(overlay.call("DebugIsInputFocusVisible")):
		_fail("Keyboard-opened skill codex did not show focus feedback")
		return
	menu.call("DebugHandleSkillCodexCancel")
	if not await _wait_for_skill_transition(menu, false):
		return

	# Gamepad open reloads the unlocked active profile.
	if not _set_active_profile(save_manager, UNLOCKED_PROFILE_SLOT):
		return
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
	if not await _wait_for_skill_transition(menu, true):
		return
	if not bool(menu.call("DebugIsSkillCodexVisible")):
		_fail("Gamepad confirm did not open the codex")
		return
	if int(overlay.call("DebugGetTotalUnlockedCardCount")) != UNLOCKED_BY_SUIT.size():
		_fail("Gamepad reopen did not refresh the unlocked active profile")
		return
	if not bool(overlay.call("DebugIsInputFocusVisible")):
		_fail("Gamepad-opened skill codex did not show focus feedback")
		return

	for viewport_size in VIEWPORT_SIZES:
		root.size = viewport_size
		await _wait_frames(3)
		if not _assert_responsive_layout(overlay, viewport_size):
			return
		if viewport_size == Vector2i(1280, 720) and not _capture_optional_screenshot():
			return

	menu.call("DebugHandleSkillCodexCancel")
	if not await _wait_for_skill_transition(menu, false):
		return

	# The scene-level testing parameter reveals every enabled entry without changing save data.
	if not _set_active_profile(save_manager, EMPTY_PROFILE_SLOT):
		return
	overlay.set("UnlockAllForTesting", true)
	skill_button.emit_signal("pressed")
	if not await _wait_for_skill_transition(menu, true):
		return
	if int(overlay.call("DebugGetTotalUnlockedCardCount")) \
			!= int(overlay.call("DebugGetTotalEnabledCardCount")):
		_fail("Testing unlock parameter did not reveal every enabled codex entry")
		return
	for suit in EXPECTED_COUNTS:
		if not bool(overlay.call("DebugSelectSuit", suit)):
			_fail("Failed to select %s while testing unlock-all" % suit)
			return
		await _wait_frames(2)
		if int(overlay.call("DebugGetCurrentSuitUnlockedCardCount")) != int(EXPECTED_COUNTS[suit]):
			_fail("Testing unlock parameter left a locked %s entry" % suit)
			return
	if not bool(overlay.call("DebugActivateVisibleCard", 0)):
		_fail("Testing unlock parameter did not make codex cards interactive")
		return
	menu.call("DebugHandleSkillCodexCancel")
	menu.call("DebugHandleSkillCodexCancel")
	if not await _wait_for_skill_transition(menu, false):
		return
	menu.queue_free()
	menu = null
	overlay = null
	skill_button = null
	for _frame in 6:
		await process_frame
	var cleanup_script := load("res://tests/ManagedTestCleanupHarness.cs") as Script
	var cleanup_harness: RefCounted = cleanup_script.new() as RefCounted if cleanup_script != null else null
	if cleanup_harness == null:
		_fail("Managed test cleanup harness is unavailable")
		return
	cleanup_harness.call("DrainFinalizers")
	cleanup_harness = null
	cleanup_script = null
	for _frame in 3:
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


func _configure_profiles() -> Node:
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return null
	save_manager.call("set_value", &"migration.settings_cfg.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v1", true)
	save_manager.call("set_value", &"migration.progress_profile.v2", true)
	if not _seed_profile(save_manager, EMPTY_PROFILE_SLOT, ""):
		return null
	var unlocked_ids := "|".join([
		UNLOCKED_BY_SUIT["hearts"],
		UNLOCKED_BY_SUIT["diamonds"],
		UNLOCKED_BY_SUIT["spades"],
		UNLOCKED_BY_SUIT["clubs"],
		"temporarily_retired_history_id",
	])
	if not _seed_profile(save_manager, UNLOCKED_PROFILE_SLOT, unlocked_ids):
		return null
	return save_manager if _set_active_profile(save_manager, EMPTY_PROFILE_SLOT) else null


func _seed_profile(save_manager: Node, slot_id: StringName, unlocked_ids: String) -> bool:
	save_manager.call("ensure_slot_for_file_base", slot_id, String(slot_id))
	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 3,
		"highest_completed_main_adventure_stage": 0,
		"completed_adventure_tutorial_ids_csv": "",
		"selected_avatar_id": "fox",
		"player_name": "Codex Smoke",
		"pending_avatar_unlock_queue": "",
		"pending_adventure_run_json": "",
		"unlocked_skill_codex_upgrade_ids_csv": unlocked_ids,
		"playtime_seconds": 0.0,
		"created_unix_time": now,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", slot_id, profile)) != OK:
		_fail("Failed to seed skill codex profile %s" % slot_id)
		return false
	return true


func _set_active_profile(save_manager: Node, slot_id: StringName) -> bool:
	save_manager.call("set_value", &"progress.active_profile_slot", String(slot_id))
	if int(save_manager.call("persist")) != OK:
		_fail("Failed to activate skill codex profile %s" % slot_id)
		return false
	return true


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
		if bool(state["interactive"]) or bool(state["locked"]) \
				or not bool(state["has_texture"]) \
				or String(state["rarity_label"]).is_empty():
			_fail("Popup preview cards must be complete non-interactive unlocked cards")
			return {}
	preview.queue_free()
	await process_frame
	return regions


func _assert_locked_cards(overlay: Node) -> bool:
	var states := overlay.call("DebugGetVisibleCardStates") as Array
	var previous_sort_order := -2147483648
	for state_variant in states:
		var state := state_variant as Dictionary
		var sort_order := int(state["sort_order"])
		if sort_order < previous_sort_order:
			_fail("Codex cards are not sorted by SortOrder")
			return false
		previous_sort_order = sort_order
		if not _assert_locked_state(overlay, state):
			return false
	return true


func _assert_mixed_cards(overlay: Node, suit: String, unlocked_id: String, popup_regions: Dictionary) -> bool:
	var states := overlay.call("DebugGetVisibleCardStates") as Array
	var previous_sort_order := -2147483648
	var unlocked_count := 0
	for state_variant in states:
		var state := state_variant as Dictionary
		var sort_order := int(state["sort_order"])
		if sort_order < previous_sort_order:
			_fail("Codex cards are not sorted by SortOrder")
			return false
		previous_sort_order = sort_order
		if String(state["scene_file_path"]) != SHARED_CARD_SCENE_PATH:
			_fail("Codex entry is not an instance of the shared card scene")
			return false
		var card := overlay.get_node("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid/UpgradeCard_%s" % state["id"]) as Control
		if String(state["id"]) != unlocked_id:
			if not _assert_locked_state(overlay, state):
				return false
			continue

		unlocked_count += 1
		var rarity := String(state["rarity"])
		var description := String(state["description"])
		if bool(state["locked"]) or not bool(state["interactive"]) \
				or not bool(state["has_texture"]) or String(state["suit"]) != suit \
				or not EXPECTED_TIERS.has(rarity) \
				or String(state["visual_tier"]) != String(EXPECTED_TIERS[rarity]) \
				or not String(state["rarity_label"]).is_empty() \
				or description.is_empty() or description.contains("{") \
				or description.begins_with("UPGRADE_DESC_"):
			_fail("Unlocked codex card content is invalid for %s" % state["id"])
			return false
		var popup_key := "%s::%s" % [suit, state["visual_tier"]]
		if not popup_regions.has(popup_key) \
				or not (state["atlas_region"] as Rect2).is_equal_approx(popup_regions[popup_key] as Rect2):
			_fail("Codex and popup AtlasTexture regions differ for %s" % popup_key)
			return false
		var region := state["atlas_region"] as Rect2
		var description_layer := card.get_node_or_null("DescriptionLayer") as Control
		var description_label := card.get_node_or_null(
			"DescriptionLayer/DescriptionMargin/DescriptionLabel") as Label
		if card.focus_mode != Control.FOCUS_ALL \
				or region.size.y <= 0.0 \
				or absf(card.size.x / card.size.y - region.size.x / region.size.y) > 0.005 \
				or description_layer == null \
				or description_label == null \
				or absf(description_layer.position.y / card.size.y - 0.43) > 0.005 \
				or absf((description_layer.position.y + description_layer.size.y) / card.size.y - 0.895) > 0.005 \
				or description_label.vertical_alignment != VERTICAL_ALIGNMENT_TOP:
			_fail("Unlocked catalog card layout is invalid for %s" % state["id"])
			return false
	if unlocked_count != 1 or int(overlay.call("DebugGetCurrentSuitUnlockedCardCount")) != 1:
		_fail("Expected exactly one unlocked %s card" % suit)
		return false
	var focused := root.gui_get_focus_owner()
	if focused == null or String(focused.name) != "UpgradeCard_%s" % unlocked_id:
		_fail("Suit tab did not focus its first unlocked card: expected=%s actual=%s" % [
			unlocked_id,
			"<none>" if focused == null else String(focused.name),
		])
		return false
	return true


func _assert_locked_state(overlay: Node, state: Dictionary) -> bool:
	if not bool(state["locked"]) or bool(state["interactive"]) \
			or bool(state["has_texture"]) or bool(state["feedback_visible"]) \
			or bool(state["missing_texture_placeholder_visible"]) \
			or not String(state["suit"]).is_empty() \
			or not String(state["rarity"]).is_empty() \
			or not String(state["visual_tier"]).is_empty() \
			or not String(state["description"]).is_empty() \
			or not String(state["rarity_label"]).is_empty() \
			or String(state["locked_label"]).is_empty() \
			or String(state["locked_label"]) == "SKILL_CODEX_LOCKED":
		_fail("Locked card leaked content or interaction for %s" % state["id"])
		return false
	if String(state["scene_file_path"]) != SHARED_CARD_SCENE_PATH:
		_fail("Locked entry is not an instance of the shared card scene")
		return false
	var card := overlay.get_node("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid/UpgradeCard_%s" % state["id"]) as Button
	if card.focus_mode != Control.FOCUS_NONE or not card.disabled \
			or absf(card.size.x / card.size.y - 196.0 / 322.0) > 0.005:
		_fail("Locked card focus or fixed aspect contract failed for %s" % state["id"])
		return false
	return true


func _assert_item_cards(overlay: Node) -> bool:
	var states := overlay.call("DebugGetVisibleItemCardStates") as Array
	if states.size() != EXPECTED_ITEM_COUNT:
		_fail("Unexpected enabled item state count")
		return false
	var seen_ids := {}
	var previous_sort_order := -1
	for state_variant in states:
		var state := state_variant as Dictionary
		var item_id := String(state["id"])
		var sort_order := int(state["sort_order"])
		if item_id.is_empty() or seen_ids.has(item_id) or item_id in ["J", "Q", "K"]:
			_fail("Item category contains a duplicate, empty, or disabled legacy entry")
			return false
		seen_ids[item_id] = true
		if sort_order < previous_sort_order:
			_fail("Item category does not follow catalog sort order")
			return false
		previous_sort_order = sort_order
		if not bool(state["interactive"]) or not bool(state["has_texture"]) \
				or String(state["title"]).is_empty() \
				or String(state["description"]).is_empty() \
				or String(state["title"]).begins_with("ITEM_NAME_") \
				or String(state["description"]).begins_with("ITEM_DESC_") \
				or String(state["scene_file_path"]) != ITEM_CARD_SCENE_PATH \
				or not String(state["card_texture_path"]).ends_with("frame-001.png"):
			_fail("Item codex card content is incomplete for %s" % item_id)
			return false
		var card := overlay.get_node(
				"Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid/ItemCard_%s" \
				% _to_pascal_case(item_id)) as Button
		if card == null or card.focus_mode != Control.FOCUS_ALL \
				or absf(card.size.x / card.size.y - 196.0 / 322.0) > 0.005:
			_fail("Item codex card layout is invalid for %s" % item_id)
			return false
	var focused := root.gui_get_focus_owner()
	if focused == null or focused != overlay.get_node(
			"Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid/ItemCard_Question_mark"):
		_fail("Item category did not focus its first card")
		return false
	return true


func _to_pascal_case(value: String) -> String:
	return value.left(1).to_upper() + value.substr(1)


func _find_visible_card_index(overlay: Node, upgrade_id: String) -> int:
	var states := overlay.call("DebugGetVisibleCardStates") as Array
	for index in states.size():
		if String((states[index] as Dictionary)["id"]) == upgrade_id:
			return index
	return -1


func _assert_card_focus_highlight_state(overlay: Node, expected: bool) -> bool:
	var states := overlay.call("DebugGetVisibleCardStates") as Array
	for state_variant in states:
		var state := state_variant as Dictionary
		if bool(state["focus_highlight_enabled"]) != expected:
			_fail("Card focus visibility did not match input mode for %s" % state["id"])
			return false
		if not expected and bool(state["feedback_visible"]):
			_fail("Card retained focus feedback before keyboard or gamepad navigation: %s" % state["id"])
			return false
	return true


func _assert_unified_visual_style(overlay: Node) -> bool:
	if not overlay.has_meta("secondary_menu_visual_profile") \
			or str(overlay.get_meta("secondary_menu_visual_profile")) != "secondary-menu-unified":
		_fail("Skill codex does not expose the unified secondary-menu visual profile")
		return false
	for path in ["Root/Backdrop", "Root/PreviewLayer/Scrim"]:
		var veil := overlay.get_node_or_null(path) as ColorRect
		var veil_material := veil.material as ShaderMaterial if veil != null else null
		if DisplayServer.get_name().to_lower() == "headless":
			if veil == null or veil_material != null or veil.color.a <= 0.0:
				_fail("Skill codex headless veil fallback is missing at %s" % path)
				return false
		elif veil_material == null or veil_material.shader == null \
				or veil_material.shader.resource_path != "res://shaders/menu_blur_scrim.gdshader":
			_fail("Skill codex blur veil is missing at %s" % path)
			return false
	var preview_surface := overlay.get_node_or_null(
		"Root/PreviewLayer/PreviewCenter/PreviewGlassSurface") as ColorRect
	var preview_frame := overlay.get_node_or_null(
		"Root/PreviewLayer/PreviewCenter/PreviewGlassFrame") as Panel
	var preview_material := preview_surface.material as ShaderMaterial if preview_surface != null else null
	if preview_material == null or preview_material.shader == null \
			or preview_material.shader.resource_path != "res://shaders/adventure_menu_glass.gdshader" \
			or preview_frame == null:
		_fail("Skill codex preview is missing its glass focus layer")
		return false

	var card := overlay.get_node_or_null(
		"Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid/UpgradeCard_hearts_thick_blood") as Control
	var hover_border := card.get_node_or_null("HoverBorder") as Panel if card != null else null
	var focus_border := card.get_node_or_null("FeedbackBorder") as Panel if card != null else null
	var hover_style := hover_border.get_theme_stylebox("panel") as StyleBoxFlat \
			if hover_border != null else null
	var focus_style := focus_border.get_theme_stylebox("panel") as StyleBoxFlat \
			if focus_border != null else null
	if hover_style == null or focus_style == null \
			or hover_style.bg_color.a > 0.01 or focus_style.bg_color.a > 0.01 \
			or hover_style.border_width_left != 1 \
			or focus_style.border_width_left != 2 \
			or hover_style.border_color.a >= focus_style.border_color.a \
			or hover_style.shadow_size >= focus_style.shadow_size \
			or focus_style.shadow_size > 4:
		_fail("Skill codex card hover and focus feedback are not visually tiered")
		return false
	if not await _assert_category_tab_hover_consistency(overlay):
		return false

	for suit in SUIT_BUTTON_NAMES:
		if not bool(overlay.call("DebugSelectSuit", suit)):
			_fail("Failed to select %s while auditing suit theme" % suit)
			return false
		await _wait_frames(2)
		var button := overlay.get_node(
			"Root/SafeMargin/Layout/SuitTabs/%s" % SUIT_BUTTON_NAMES[suit]) as Button
		var style := button.get_theme_stylebox("normal") as StyleBoxFlat
		if button.text != SUIT_BUTTON_SYMBOLS[suit]:
			_fail("%s tab should display only its suit symbol" % suit)
			return false
		if not button.button_pressed or style == null \
				or not style.border_color.is_equal_approx(SUIT_BORDER_COLORS[suit]):
			_fail("Selected %s tab does not use its individual suit theme" % suit)
			return false
	if not bool(overlay.call("DebugSelectSuit", "hearts")):
		_fail("Failed to restore hearts after suit theme audit")
		return false
	await _wait_frames(2)
	return true


func _assert_category_tab_hover_consistency(overlay: Node) -> bool:
	for category in ["upgrades", "items"]:
		if not bool(overlay.call("DebugSelectCategory", category)):
			_fail("Failed to select %s while auditing category hover theme" % category)
			return false
		await _wait_frames(2)
		var upgrades_button := overlay.get_node(
				"Root/SafeMargin/Layout/CategoryTabs/UpgradesButton") as Button
		var items_button := overlay.get_node(
				"Root/SafeMargin/Layout/CategoryTabs/ItemsButton") as Button
		var upgrades_hover := upgrades_button.get_theme_stylebox("hover") as StyleBoxFlat
		var items_hover := items_button.get_theme_stylebox("hover") as StyleBoxFlat
		if upgrades_hover == null or items_hover == null \
				or not upgrades_hover.bg_color.is_equal_approx(items_hover.bg_color) \
				or not upgrades_hover.border_color.is_equal_approx(items_hover.border_color):
			_fail("Skill codex category tabs use inconsistent hover backgrounds")
			return false
	if not bool(overlay.call("DebugSelectCategory", "upgrades")):
		_fail("Failed to restore upgrades after category hover theme audit")
		return false
	await _wait_frames(2)
	return true


func _assert_responsive_layout(overlay: Node, viewport_size: Vector2i) -> bool:
	var transition_root := overlay.get_node("Root") as Control
	var scroll := overlay.get_node("Root/SafeMargin/Layout/CardScroll") as ScrollContainer
	var grid := overlay.get_node("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid") as GridContainer
	var available_width := maxf(0.0, scroll.size.x - 48.0)
	var expected_card_width := CATALOG_CARD_SIZE.x
	if String(overlay.call("DebugGetCurrentCategory")) == "items":
		var available_height := maxf(1.0, scroll.size.y)
		var visible_rows := clampi(
				floori((available_height + CATALOG_CARD_GAP) \
						/ (MINIMUM_ITEM_CARD_HEIGHT + CATALOG_CARD_GAP)),
				1,
				MAXIMUM_VISIBLE_ITEM_ROWS)
		var expected_card_height := minf(
				CATALOG_CARD_SIZE.y,
				floorf((available_height - ((visible_rows - 1) * CATALOG_CARD_GAP)) \
						/ visible_rows))
		expected_card_width = roundf(expected_card_height \
				* CATALOG_CARD_SIZE.x / CATALOG_CARD_SIZE.y)
		if not _assert_item_cards_fill_complete_rows(scroll, grid, Vector2(
				expected_card_width,
				expected_card_height), viewport_size):
			return false
	var expected_columns := clampi(floori(
			(available_width + CATALOG_CARD_GAP) \
					/ (expected_card_width + CATALOG_CARD_GAP)), 2, 4)
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
	var canvas_transform := transition_root.get_global_transform_with_canvas()
	var screen_top_left := canvas_transform * Vector2.ZERO
	var screen_bottom_right := canvas_transform * transition_root.size
	var visible_viewport_size := transition_root.get_viewport_rect().size
	if screen_top_left.distance_to(Vector2.ZERO) > 2.0 \
			or screen_bottom_right.distance_to(visible_viewport_size) > 2.0:
		_fail("Skill codex transition root does not cover the viewport at %s: %s -> %s" \
				% [viewport_size, screen_top_left, screen_bottom_right])
		return false
	return true


func _assert_item_cards_fill_complete_rows(
		scroll: ScrollContainer,
		grid: GridContainer,
		expected_card_size: Vector2,
		viewport_size: Vector2i) -> bool:
	var scroll_rect := scroll.get_global_rect()
	var visible_card_count := 0
	for child in grid.get_children():
		var card := child as Control
		if card == null:
			continue
		if not card.size.is_equal_approx(expected_card_size):
			_fail("Item card size mismatch at %s: expected %s, got %s" \
					% [viewport_size, expected_card_size, card.size])
			return false
		var card_rect := card.get_global_rect()
		var intersection := scroll_rect.intersection(card_rect)
		if not intersection.has_area():
			continue
		visible_card_count += 1
		if absf(intersection.size.x - card_rect.size.x) > 1.5 \
				or absf(intersection.size.y - card_rect.size.y) > 1.5:
			_fail("Item card is partially clipped at %s: %s inside %s" \
					% [viewport_size, card_rect, scroll_rect])
			return false
	if visible_card_count == 0:
		_fail("Item codex has no fully visible cards at %s" % viewport_size)
		return false
	return true


func _assert_item_cards_scroll_into_view(overlay: Node) -> bool:
	var scroll := overlay.get_node("Root/SafeMargin/Layout/CardScroll") as ScrollContainer
	var grid := overlay.get_node("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid") as GridContainer
	var scroll_rect := scroll.get_global_rect()
	for child in grid.get_children():
		var card := child as Control
		if card == null:
			continue
		scroll.ensure_control_visible(card)
		await _wait_frames(2)
		var card_rect := card.get_global_rect()
		if card_rect.position.y < scroll_rect.position.y - 1.5 \
				or card_rect.end.y > scroll_rect.end.y + 1.5:
			_fail("Item card could not be scrolled fully into view: %s inside %s" \
					% [card_rect, scroll_rect])
			return false
	scroll.scroll_vertical = 0
	await _wait_frames(2)
	return true


func _assert_skill_codex_transition_geometry(menu: Node, overlay: Node) -> bool:
	var transition_root := overlay.get_node("Root") as Control
	var grid := overlay.get_node("Root/SafeMargin/Layout/CardScroll/GridCenter/CardGrid") as GridContainer
	if transition_root == null or grid == null:
		_fail("Skill codex transition geometry nodes are missing")
		return false
	if not transition_root.position.is_zero_approx() \
			or not transition_root.scale.is_equal_approx(Vector2.ONE):
		_fail("Skill codex transition root must use its default screen-space transform")
		return false

	await _wait_frames(2)
	if grid.get_child_count() == 0:
		_fail("Skill codex did not build cards before its entrance transition")
		return false
	var first_card := grid.get_child(0) as Control
	if first_card == null:
		_fail("Skill codex first card is not a control")
		return false

	var root_rect := _get_canvas_rect(transition_root)
	var card_rect := _get_canvas_rect(first_card)
	while bool(menu.call("DebugIsSkillCodexTransitioning")):
		await process_frame
		if not _rect_is_equal_approx(root_rect, _get_canvas_rect(transition_root)) \
				or not _rect_is_equal_approx(card_rect, _get_canvas_rect(first_card)):
			_fail("Skill codex geometry changed while only the background camera was moving")
			return false
	return true


func _get_canvas_rect(control: Control) -> Rect2:
	var canvas_transform := control.get_global_transform_with_canvas()
	var top_left := canvas_transform * Vector2.ZERO
	var bottom_right := canvas_transform * control.size
	return Rect2(top_left, bottom_right - top_left)


func _rect_is_equal_approx(left: Rect2, right: Rect2) -> bool:
	return left.position.is_equal_approx(right.position) \
			and left.size.is_equal_approx(right.size)


func _wait_for_skill_transition(menu: Node, expected_visible: bool) -> bool:
	var deadline := Time.get_ticks_msec() + 3000
	if expected_visible:
		while not bool(menu.call("DebugIsSkillCodexVisible")) \
				and not bool(menu.call("DebugIsSkillCodexTransitioning")) \
				and Time.get_ticks_msec() < deadline:
			await process_frame
	while bool(menu.call("DebugIsSkillCodexTransitioning")) and Time.get_ticks_msec() < deadline:
		await process_frame
	if bool(menu.call("DebugIsSkillCodexTransitioning")):
		_fail("Skill codex transition timed out")
		return false
	if bool(menu.call("DebugIsSkillCodexVisible")) != expected_visible:
		_fail("Skill codex visibility did not settle to %s" % expected_visible)
		return false
	await process_frame
	return true


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _capture_optional_screenshot() -> bool:
	return _capture_optional_screenshot_from_env(
			"SKILL_CODEX_SCREENSHOT_PATH", "Skill codex")


func _capture_optional_item_screenshot() -> bool:
	return _capture_optional_screenshot_from_env(
			"SKILL_CODEX_ITEM_SCREENSHOT_PATH", "Item codex")


func _capture_optional_item_preview_screenshot() -> bool:
	return _capture_optional_screenshot_from_env(
			"SKILL_CODEX_ITEM_PREVIEW_SCREENSHOT_PATH", "Item codex preview")


func _capture_optional_screenshot_from_env(environment_key: String, label: String) -> bool:
	var output_path := OS.get_environment(environment_key).strip_edges()
	if output_path.is_empty():
		return true
	var image := root.get_texture().get_image()
	if image == null or image.is_empty() or image.get_used_rect().size == Vector2i.ZERO:
		_fail("%s viewport screenshot is empty" % label)
		return false
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Failed to save %s screenshot: %s" % [label, error_string(save_error)])
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
