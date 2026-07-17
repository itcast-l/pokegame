extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const SENTINEL_SIZE := Vector2i(777, 444)

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var menu := await _instantiate_menu()
	if menu == null:
		return

	var window_mode := menu.find_child("WindowModeOption", true, false) as OptionButton
	var resolution := menu.find_child("ResolutionOption", true, false) as OptionButton
	var master_volume := menu.find_child("MasterVolumeSlider", true, false) as HSlider
	if window_mode == null or resolution == null or master_volume == null:
		_fail("Main menu settings controls are unavailable")
		return

	# A stale windowed continuation must not resize after a same-frame fullscreen request.
	resolution.emit_signal("item_selected", 0)
	window_mode.emit_signal("item_selected", 1)
	root.get_window().size = SENTINEL_SIZE
	await _wait_frames(3)
	if root.get_window().size != SENTINEL_SIZE:
		_fail("Stale windowed resolution was applied after fullscreen")
		return

	# Of two windowed requests, only the latest resolution may finish.
	window_mode.emit_signal("item_selected", 0)
	resolution.emit_signal("item_selected", 0)
	resolution.emit_signal("item_selected", 1)
	root.get_window().size = SENTINEL_SIZE
	await _wait_frames(3)
	if root.get_window().size != Vector2i(1024, 576):
		_fail("Latest windowed resolution did not win")
		return

	# A settings-only update must not invalidate an already pending resize.
	resolution.emit_signal("item_selected", 0)
	root.get_window().size = SENTINEL_SIZE
	master_volume.value = clampf(master_volume.value + 1.0, master_volume.min_value, master_volume.max_value)
	await _wait_frames(3)
	if root.get_window().size != Vector2i(960, 540):
		_fail("Volume-only update canceled the pending windowed resize")
		return

	# Leaving fullscreen for windowed mode applies and centers the current resolution.
	window_mode.emit_signal("item_selected", 1)
	window_mode.emit_signal("item_selected", 0)
	await _wait_frames(3)
	if root.get_window().size != Vector2i(960, 540):
		_fail("Fullscreen-to-windowed transition did not apply the selected resolution")
		return
	var usable_rect := DisplayServer.screen_get_usable_rect(DisplayServer.window_get_current_screen())
	if usable_rect.size.x > 0 and usable_rect.size.y > 0:
		var expected_position := Vector2i(
			usable_rect.position.x + maxi((usable_rect.size.x - 960) / 2, 0),
			usable_rect.position.y + maxi((usable_rect.size.y - 540) / 2, 0))
		if root.get_window().position != expected_position:
			_fail("Fullscreen-to-windowed transition did not center the selected resolution")
			return

	# Recreating the menu after a native maximize must not restore the saved windowed size.
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_MAXIMIZED)
	await process_frame
	if DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_MAXIMIZED:
		menu.queue_free()
		await process_frame
		menu = await _instantiate_menu()
		if menu == null:
			return
		await _wait_frames(3)
		if DisplayServer.window_get_mode() != DisplayServer.WINDOW_MODE_MAXIMIZED:
			_fail("Recreated main menu discarded the native maximized state")
			return

	print("MAIN_MENU_WINDOW_SETTINGS_TOKEN_SMOKE_OK")
	menu.queue_free()
	await process_frame
	quit(0)


func _instantiate_menu() -> Node:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load main menu scene")
		return null

	var menu := packed_scene.instantiate()
	if menu == null:
		_fail("Failed to instantiate main menu scene")
		return null

	root.add_child(menu)
	current_scene = menu
	await _wait_frames(2)
	return menu


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("MAIN_MENU_WINDOW_SETTINGS_TOKEN_SMOKE_FAIL %s" % message)
	quit(1)
