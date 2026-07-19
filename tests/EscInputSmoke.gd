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
