extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const TEST_SAVE_ROOT := "user://settings_deferred_persistence_smoke"
const DEBOUNCE_SECONDS := 0.45
const PROFILE_SAVE_DELAY_MSEC := 600
const PROFILE_PROBE_PLAYER_NAME_A := "ProbeAlpha"
const PROFILE_PROBE_PLAYER_NAME_B := "ProbeBeta"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return

	save_manager.set("save_root", TEST_SAVE_ROOT)
	save_manager.set("use_json", true)
	save_manager.set("backup_on_commit", false)
	save_manager.set("auto_save_debounce_sec", DEBOUNCE_SECONDS)
	save_manager.call("configure_async_write_debug", 0, 0)
	save_manager.call("clear_kv_cache")
	if DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(TEST_SAVE_ROOT)) != OK:
		_fail("Failed to create settings smoke save root")
		return

	var menu := await _instantiate_menu()
	if menu == null:
		return
	var slider := menu.find_child("MasterVolumeSlider", true, false) as HSlider
	var settings_root := menu.find_child("SettingsDialogRoot", true, false)
	var done_button := settings_root.find_child("DoneButton", true, false) as Button \
		if settings_root != null else null
	if slider == null or done_button == null:
		_fail("Main menu settings controls are unavailable")
		return

	await _wait_until_idle(save_manager)
	var writes_before := int(save_manager.call("get_async_physical_write_count_debug"))
	var final_slider_value := slider.value
	for offset in range(1, 11):
		final_slider_value = fmod(slider.value + 7.0, 101.0)
		slider.value = final_slider_value
		await process_frame

	await create_timer(0.15).timeout
	if int(save_manager.call("get_async_physical_write_count_debug")) != writes_before:
		_fail("Continuous slider changes wrote before the debounce silence window")
		return
	if not await _wait_for_write_count(save_manager, writes_before + 1):
		return
	await _wait_until_idle(save_manager)
	var writes_after_debounce := int(save_manager.call("get_async_physical_write_count_debug"))
	if writes_after_debounce != writes_before + 1:
		_fail("Debounced slider changes did not produce exactly one physical write")
		return

	var serialization_before := int(menu.call("DebugGetInputBindingsSerializationCount"))
	if bool(menu.call("DebugResetControlsToDefaultsForPersistenceProbe")):
		_fail("Resetting already-default controls serialized bindings")
		return
	if bool(menu.call("DebugRebindKeyboardForPersistenceProbe", "move_up", KEY_UP)):
		_fail("Rebinding to the existing key was treated as a change")
		return
	if int(menu.call("DebugGetInputBindingsSerializationCount")) != serialization_before:
		_fail("Unchanged input bindings were serialized")
		return
	if not bool(menu.call("DebugRebindKeyboardForPersistenceProbe", "move_up", KEY_W)):
		_fail("A changed input binding was not persisted")
		return
	if int(menu.call("DebugGetInputBindingsSerializationCount")) != serialization_before + 1:
		_fail("A changed input binding was not serialized exactly once")
		return
	if int(save_manager.call("get_async_physical_write_count_debug")) != writes_after_debounce:
		_fail("Changed input binding wrote before the close flush")
		return

	done_button.emit_signal("pressed")
	if not await _wait_for_write_count(save_manager, writes_after_debounce + 1):
		return
	await _wait_until_idle(save_manager)
	var writes_after_close := int(save_manager.call("get_async_physical_write_count_debug"))
	if writes_after_close != writes_after_debounce + 1:
		_fail("Settings close did not flush exactly one pending write")
		return
	await create_timer(DEBOUNCE_SECONDS + 0.25).timeout
	await _wait_until_idle(save_manager)
	if int(save_manager.call("get_async_physical_write_count_debug")) != writes_after_close:
		_fail("The stopped debounce timer wrote again after the close flush")
		return

	var persisted := save_manager.call("load_from_slot_sync", &"slot_0") as Dictionary
	if not persisted.has("settings.input.bindings"):
		_fail("The changed input binding was not present in the flushed KV snapshot")
		return
	var expected_master := float(final_slider_value) / 100.0
	if not is_equal_approx(float(persisted.get("settings.audio.master_volume", -1.0)), expected_master):
		_fail("The final slider value was not the value persisted after coalescing")
		return
	if not await _test_adventure_waits_for_profile_save(menu, save_manager):
		return

	print("SETTINGS_DEFERRED_PERSISTENCE_SMOKE_OK")
	menu.queue_free()
	await process_frame
	quit(0)


func _instantiate_menu() -> Node:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load the main menu scene")
		return null
	var menu := packed_scene.instantiate()
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(3)
	return menu


func _test_adventure_waits_for_profile_save(menu: Node, save_manager: Node) -> bool:
	if not menu.has_method("DebugSavePlayerProfileForPersistenceProbe") \
			or not menu.has_method("DebugIsPlayerProfileSavePending"):
		return _fail("Main menu profile persistence probes are unavailable")
	var adventure_menu := await _wait_for_adventure_profile_probe(menu)
	if adventure_menu == null:
		return false
	var adventure_button := menu.get_node_or_null(
		"MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/AdventureFrame/Button"
	) as Button
	if adventure_button == null:
		return _fail("Main menu adventure button is unavailable")
	var initial_player_name := String(adventure_menu.call("DebugGetProfilePlayerName"))
	var profile_probe_player_name := PROFILE_PROBE_PLAYER_NAME_A \
		if initial_player_name != PROFILE_PROBE_PLAYER_NAME_A \
		else PROFILE_PROBE_PLAYER_NAME_B

	save_manager.call("configure_async_write_debug", PROFILE_SAVE_DELAY_MSEC, 0)
	var profile_writes_before := int(save_manager.call("get_async_physical_write_count_debug"))
	menu.call("DebugSavePlayerProfileForPersistenceProbe", profile_probe_player_name)
	if not bool(menu.call("DebugIsPlayerProfileSavePending")):
		return _fail(
			"Player profile save completed before the delayed persistence probe " \
			+ "(initial=%s target=%s result=%d writes=%d)" % [
				initial_player_name,
				profile_probe_player_name,
				int(menu.call("DebugGetPlayerProfileSaveResult")),
				int(save_manager.call("get_async_physical_write_count_debug")) - profile_writes_before,
			]
		)
	adventure_button.emit_signal("pressed")
	await _wait_frames(3)
	if not bool(menu.call("DebugIsPlayerProfileSavePending")):
		return _fail("Delayed player profile save did not remain pending during adventure entry")
	if String(adventure_menu.call("DebugGetProfilePlayerName")) == profile_probe_player_name:
		return _fail("Adventure profile snapshot refreshed before the player profile save committed")

	for _frame in range(600):
		if not bool(menu.call("DebugIsPlayerProfileSavePending")) \
				and String(adventure_menu.call("DebugGetProfilePlayerName")) == profile_probe_player_name:
			return true
		await process_frame
	return _fail("Adventure profile snapshot did not refresh after the player profile save committed")


func _wait_for_adventure_profile_probe(menu: Node) -> Node:
	for _frame in range(240):
		var probe := _find_adventure_profile_probe(menu)
		if probe != null:
			return probe
		await process_frame
	_fail("Prepared adventure menu profile probe is unavailable")
	return null


func _find_adventure_profile_probe(node: Node) -> Node:
	if node.has_method("DebugGetProfilePlayerName"):
		return node
	for child in node.get_children():
		var found := _find_adventure_profile_probe(child)
		if found != null:
			return found
	return null


func _wait_for_write_count(save_manager: Node, expected_count: int) -> bool:
	for _frame in range(600):
		if int(save_manager.call("get_async_physical_write_count_debug")) >= expected_count:
			return true
		await process_frame
	return _fail("Timed out waiting for settings physical write %d" % expected_count)


func _wait_until_idle(save_manager: Node) -> bool:
	for _frame in range(600):
		if bool(save_manager.call("is_async_write_idle")):
			return true
		await process_frame
	return _fail("Timed out waiting for the settings save queue to become idle")


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> bool:
	if not _failed:
		_failed = true
		push_error(message)
		print("SETTINGS_DEFERRED_PERSISTENCE_SMOKE_FAIL %s" % message)
		quit(1)
	return false
