extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const LEVEL_LIST_PATH := "SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/LevelListPanel/LevelListMargin/LevelListLayout/LevelScroll/LevelList"
const MAIN_ADVENTURE_BUTTON_PATH := "SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/AdventureListPanel/AdventureListMargin/AdventureList/deep_tide_throneAdventureEntry/Button"
const HARNESS_SCRIPT := preload("res://tests/AdventureStageUnlockHarness.cs")
const LOCKED_NAME_PLACEHOLDER := "???"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var harness := HARNESS_SCRIPT.new() as Node
	if harness == null:
		_fail("Could not instantiate adventure stage unlock harness")
		return
	root.add_child(harness)
	if not bool(harness.call("ResetProgress")):
		_fail("Could not reset adventure progress before level visibility checks")
		return
	var tutorial_entry_ids := _catalog_entry_ids(harness, "tutorial")
	var main_entry_ids := _catalog_entry_ids(harness, "deep_tide_throne")
	if tutorial_entry_ids.is_empty() or main_entry_ids.is_empty():
		_fail("Adventure campaign catalog returned no menu entries")
		return

	var menu := await _instantiate_menu()
	if menu == null:
		return

	await process_frame
	await process_frame
	_assert_level_entries(menu, tutorial_entry_ids, tutorial_entry_ids.size(), "tutorial")
	_assert_entry_focused(menu, tutorial_entry_ids[0], "tutorial")
	if _failed:
		return

	if not await _select_main_adventure(menu):
		return

	_assert_level_entries(menu, main_entry_ids, 1, "new main adventure")
	_assert_entry_focused(menu, main_entry_ids[0], "new main adventure")
	if _failed:
		return

	menu.queue_free()
	await process_frame
	await process_frame
	if not bool(harness.call("SetHighestCompletedStage", 1)):
		_fail("Could not seed completed adventure progress")
		return

	menu = await _instantiate_menu()
	if menu == null or not await _select_main_adventure(menu):
		return
	_assert_level_entries(menu, main_entry_ids, 2, "progressed main adventure")
	_assert_entry_focused(menu, main_entry_ids[1], "progressed main adventure")
	if _failed:
		return
	if not await _capture_optional_screenshot("progressed-main-adventure"):
		return

	if not bool(harness.call("ResetProgress")):
		_fail("Could not restore adventure progress after level visibility checks")
		return
	print("ADVENTURE_AVATAR_MENU_DYNAMIC_LEVEL_LIST_SMOKE_OK")
	menu.queue_free()
	harness.queue_free()
	quit(0)


func _instantiate_menu() -> Control:
	var packed_scene := load(MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load adventure avatar menu")
		return null
	var menu := packed_scene.instantiate() as Control
	if menu == null:
		_fail("Failed to instantiate adventure avatar menu")
		return null
	root.add_child(menu)
	current_scene = menu
	await process_frame
	return menu


func _select_main_adventure(menu: Control) -> bool:
	var main_adventure_button := menu.get_node_or_null(MAIN_ADVENTURE_BUTTON_PATH) as Button
	if main_adventure_button == null:
		_fail("Missing main adventure selection button")
		return false
	main_adventure_button.emit_signal("pressed")
	await process_frame
	await process_frame
	return true


func _assert_level_entries(menu: Control, expected_ids: Array, revealed_count: int, context: String) -> void:
	var level_list := menu.get_node_or_null(LEVEL_LIST_PATH) as VBoxContainer
	if level_list == null:
		_fail("Missing level list for %s" % context)
		return
	if level_list.get_child_count() != expected_ids.size():
		_fail("Expected %d generated entries for %s, found %d" % [expected_ids.size(), context, level_list.get_child_count()])
		return

	for index in range(expected_ids.size()):
		var expected_node_name := "%sLevelEntry" % expected_ids[index]
		var entry := level_list.get_child(index) as Control
		if entry == null or entry.name != expected_node_name:
			_fail("Unexpected entry at index %d for %s: expected %s" % [index, context, expected_node_name])
			return
		var button := entry.get_node_or_null("Button") as Button
		var avatar := entry.get_node_or_null("EntryContent/EntryRow/EntryAvatarAnchor/EntryAvatar") as Control
		var name_label := entry.get_node_or_null("EntryContent/EntryRow/EntryName") as Label
		var status_label := entry.get_node_or_null("EntryContent/EntryRow/EntryStatus") as Label
		if button == null or avatar == null or name_label == null or status_label == null:
			_fail("Generated entry %s is missing its button" % expected_node_name)
			return
		if int(entry.get("FrameStyle")) != 11:
			_fail("Generated entry %s does not use the unified tool-row frame" % expected_node_name)
			return
		var should_reveal := index < revealed_count
		if should_reveal and (button.disabled or not avatar.visible \
				or name_label.text.is_empty() or name_label.text == LOCKED_NAME_PLACEHOLDER):
			_fail("Unlocked entry %s did not reveal its character information" % expected_node_name)
			return
		if not should_reveal and (not button.disabled or avatar.visible \
				or name_label.text != LOCKED_NAME_PLACEHOLDER \
				or button.tooltip_text != status_label.text):
			_fail("Locked entry %s leaked its character information" % expected_node_name)
			return


func _assert_entry_focused(menu: Control, avatar_id: String, context: String) -> void:
	var level_list := menu.get_node_or_null(LEVEL_LIST_PATH) as VBoxContainer
	var entry := level_list.get_node_or_null("%sLevelEntry" % avatar_id) as Control if level_list != null else null
	var button := entry.get_node_or_null("Button") as Button if entry != null else null
	if button == null or not button.has_focus():
		_fail("Expected visible entry %s to receive focus for %s" % [avatar_id, context])


func _catalog_entry_ids(harness: Node, campaign_id: String) -> Array:
	var entry_ids: Array = []
	for avatar_id in harness.call("GetCampaignEnemyAvatarIds", campaign_id):
		entry_ids.append(str(avatar_id))
	return entry_ids


func _capture_optional_screenshot(state_name: String) -> bool:
	var output_dir := OS.get_environment("ADVENTURE_AVATAR_MENU_LEVEL_VISIBILITY_SCREENSHOT_DIR").strip_edges()
	if output_dir.is_empty():
		return true
	var make_dir_error := DirAccess.make_dir_recursive_absolute(output_dir)
	if make_dir_error != OK:
		_fail("Could not create level visibility screenshot directory")
		return false
	for _frame in 3:
		await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	if image == null or image.is_empty():
		_fail("Level visibility screenshot was empty")
		return false
	var output_path := output_dir.path_join("adventure-level-visibility-%s.png" % state_name)
	var save_error := image.save_png(output_path)
	if save_error != OK:
		_fail("Could not save level visibility screenshot: %s" % error_string(save_error))
		return false
	return true


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error("ADVENTURE_AVATAR_MENU_DYNAMIC_LEVEL_LIST_SMOKE_FAIL %s" % message)
	quit(1)
