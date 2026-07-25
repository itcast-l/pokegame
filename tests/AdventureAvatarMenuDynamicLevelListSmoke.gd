extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const LEVEL_LIST_PATH := "SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/LevelListPanel/LevelListMargin/LevelListLayout/LevelScroll/LevelList"
const MAIN_ADVENTURE_BUTTON_PATH := "SafeArea/MenuCenter/AdventureMenuPanel/AdventureMenuMargin/Layout/Body/AdventureListPanel/AdventureListMargin/AdventureList/deep_tide_throneAdventureEntry/Button"
const HARNESS_SCRIPT := preload("res://tests/AdventureStageUnlockHarness.cs")

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var harness := HARNESS_SCRIPT.new() as Node
	if harness == null:
		_fail("Could not instantiate adventure stage unlock harness")
		return
	root.add_child(harness)
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
	_assert_level_entries(menu, tutorial_entry_ids, "tutorial")
	_assert_first_entry_focused(menu, "tutorial")
	if _failed:
		return

	var main_adventure_button := menu.get_node_or_null(MAIN_ADVENTURE_BUTTON_PATH) as Button
	if main_adventure_button == null:
		_fail("Missing main adventure selection button")
		return
	main_adventure_button.emit_signal("pressed")
	await process_frame
	await process_frame

	_assert_level_entries(menu, main_entry_ids, "main adventure")
	_assert_first_entry_focused(menu, "main adventure")
	if _failed:
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


func _assert_level_entries(menu: Control, expected_ids: Array, context: String) -> void:
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
		if entry.get_node_or_null("Button") is not Button:
			_fail("Generated entry %s is missing its button" % expected_node_name)
			return


func _assert_first_entry_focused(menu: Control, context: String) -> void:
	var level_list := menu.get_node_or_null(LEVEL_LIST_PATH) as VBoxContainer
	if level_list == null or level_list.get_child_count() == 0:
		_fail("No generated entry available to receive focus for %s" % context)
		return
	var button := level_list.get_child(0).get_node_or_null("Button") as Button
	if button == null or not button.has_focus():
		_fail("First generated entry did not receive focus for %s" % context)


func _catalog_entry_ids(harness: Node, campaign_id: String) -> Array:
	var entry_ids: Array = []
	for avatar_id in harness.call("GetCampaignEnemyAvatarIds", campaign_id):
		entry_ids.append(str(avatar_id))
	return entry_ids


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error("ADVENTURE_AVATAR_MENU_DYNAMIC_LEVEL_LIST_SMOKE_FAIL %s" % message)
	quit(1)
