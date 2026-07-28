extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const GAME_BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 10.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	await _assert_main_menu_responds_to_escape()
	if _failed:
		return

	await _assert_offline_game_board_pause_semantics()
	if _failed:
		return

	await _assert_network_game_board_pause_semantics()
	if _failed:
		return

	print("ESC_INPUT_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _assert_main_menu_responds_to_escape() -> void:
	var main_menu := await _instantiate_scene(MAIN_MENU_SCENE_PATH)
	if main_menu == null:
		return

	var exit_dialog := main_menu.get_node_or_null("ExitDialogLayer/ExitDialogRoot")
	if exit_dialog == null:
		_fail("Main menu is missing ExitDialogLayer/ExitDialogRoot")
		return

	if exit_dialog.visible:
		_fail("Main menu exit dialog should start hidden")
		return

	_press_escape()
	await process_frame
	await process_frame

	if not exit_dialog.visible:
		_fail("Main menu did not open exit dialog after Escape")
		return

	_press_escape()
	if not await _wait_for_hidden(exit_dialog, 120):
		_fail("Main menu exit dialog did not close after the second Escape")
		return
	if not await _wait_for_menu_buttons_enabled(main_menu, 180):
		_fail("Main menu camera lock did not release after closing exit dialog")
		return

	var settings_button := main_menu.get_node_or_null("MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SettingsFrame/Button") as Button
	var settings_root := main_menu.get_node_or_null("SettingsDialogLayer/SettingsDialogRoot") as Control
	var controls_root := main_menu.get_node_or_null("SettingsDialogLayer/ControlsDialogRoot") as Control
	if settings_button == null or settings_root == null or controls_root == null:
		_fail("Main menu settings/controls nodes are missing")
		return
	settings_button.emit_signal("pressed")
	await process_frame
	var controls_button := settings_root.find_child("ControlsButton", true, false) as Button
	if controls_button == null:
		_fail("Settings controls button is missing")
		return
	controls_button.emit_signal("pressed")
	await process_frame
	var binding_button := controls_root.find_child("KeyboardButton", true, false) as Button
	if binding_button == null:
		_fail("Controls binding button is missing")
		return
	var original_binding_text := binding_button.text
	binding_button.emit_signal("pressed")
	await process_frame
	await process_frame
	_press_escape()
	await process_frame
	await process_frame
	if not controls_root.visible:
		_fail("Escape closed controls instead of cancelling active binding capture")
		return
	if binding_button.text != original_binding_text:
		_fail("Escape was saved as a binding instead of cancelling capture")
		return

	_press_escape()
	if not await _wait_for_hidden(controls_root, 120):
		_fail("Second Escape did not return from controls to settings")
		return
	if not settings_root.visible:
		_fail("Settings closed while returning from controls")
		return
	_press_escape()
	if not await _wait_for_hidden(settings_root, 120):
		_fail("Escape did not close settings after returning from controls")
		return

	main_menu.queue_free()
	await process_frame


func _assert_offline_game_board_pause_semantics() -> void:
	var board := await _instantiate_game_board("")
	if board == null:
		return

	if board.get_node_or_null("PauseMenuLayer") != null:
		_fail("Pause menu should not exist before Escape")
		return

	_press_escape()
	await process_frame
	await process_frame

	var pause_menu_layer := board.get_node_or_null("PauseMenuLayer")
	if pause_menu_layer == null:
		_fail("Game board did not create pause menu after Escape")
		return

	var pause_menu_root := pause_menu_layer.get_node_or_null("PauseMenuRoot")
	if pause_menu_root == null or not pause_menu_root.visible:
		_fail("Game board pause menu did not become visible after Escape")
		return
	if not paused:
		_fail("Offline game board did not pause the scene tree after Escape")
		return

	var quit_button := pause_menu_root.find_child("QuitButton", true, false) as Button
	var quit_confirm := pause_menu_root.get_node_or_null("PauseQuitConfirmRoot") as Control
	if quit_button == null or quit_confirm == null:
		_fail("Offline pause quit confirmation nodes are missing")
		return
	quit_button.emit_signal("pressed")
	await process_frame
	if not quit_confirm.visible:
		_fail("Offline pause quit confirmation did not open")
		return
	_press_escape()
	await process_frame
	await process_frame
	if quit_confirm.visible:
		_fail("Escape did not close pause quit confirmation first")
		return
	if not pause_menu_root.visible or not paused:
		_fail("Closing pause quit confirmation also closed or resumed the pause menu")
		return

	_press_escape()
	await process_frame
	await process_frame
	if paused:
		_fail("Offline game board did not resume the scene tree after closing the pause menu")
		return
	if pause_menu_root.visible:
		_fail("Offline game board pause menu remained visible after closing")
		return

	board.queue_free()
	await process_frame


func _assert_network_game_board_pause_semantics() -> void:
	var board := await _instantiate_game_board("esc-input-network-session")
	if board == null:
		return

	_press_escape()
	await process_frame
	await process_frame

	var pause_menu_layer := board.get_node_or_null("PauseMenuLayer")
	var pause_menu_root := pause_menu_layer.get_node_or_null("PauseMenuRoot") if pause_menu_layer != null else null
	if pause_menu_root == null or not pause_menu_root.visible:
		_fail("Network game board pause menu did not become visible after Escape")
		return
	if paused:
		_fail("Network game board pause menu unexpectedly paused the scene tree")
		return

	_press_escape()
	await process_frame
	await process_frame
	if paused:
		_fail("Closing the network pause menu changed the scene tree to paused")
		return

	board.queue_free()
	await process_frame


func _instantiate_game_board(network_session_id: String) -> Node:
	var packed_scene := load(GAME_BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % GAME_BOARD_SCENE_PATH)
		return null

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % GAME_BOARD_SCENE_PATH)
		return null

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", false)
	board.set("network_session_id", network_session_id)
	root.add_child(board)
	current_scene = board
	await process_frame
	await process_frame
	return board


func _instantiate_scene(scene_path: String) -> Node:
	var packed_scene := load(scene_path) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % scene_path)
		return null

	var instance := packed_scene.instantiate()
	if instance == null:
		_fail("Failed to instantiate scene: %s" % scene_path)
		return null

	root.add_child(instance)
	current_scene = instance
	await process_frame
	return instance


func _press_escape() -> void:
	var press := InputEventKey.new()
	press.keycode = KEY_ESCAPE
	press.physical_keycode = KEY_ESCAPE
	press.pressed = true
	press.echo = false
	root.push_input(press, true)

	var release := InputEventKey.new()
	release.keycode = KEY_ESCAPE
	release.physical_keycode = KEY_ESCAPE
	release.pressed = false
	release.echo = false
	root.push_input(release, true)


func _wait_for_hidden(control: Control, max_frames: int) -> bool:
	for _frame in max_frames:
		if not control.visible:
			return true
		await process_frame
	return not control.visible


func _wait_for_menu_buttons_enabled(menu: Node, max_frames: int) -> bool:
	for _frame in max_frames:
		if not bool(menu.call("DebugAreMainMenuButtonsDisabled")):
			return true
		await process_frame
	return not bool(menu.call("DebugAreMainMenuButtonsDisabled"))


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ESC_INPUT_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
