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

	await _assert_game_board_responds_to_escape()
	if _failed:
		return

	print("ESC_INPUT_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _assert_main_menu_responds_to_escape() -> void:
	var main_menu := await _instantiate_scene(MAIN_MENU_SCENE_PATH)
	if main_menu == null:
		return

	var exit_dialog := main_menu.get_node_or_null("ExitDialogRoot")
	if exit_dialog == null:
		_fail("Main menu is missing ExitDialogRoot")
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


func _assert_game_board_responds_to_escape() -> void:
	var board := await _instantiate_scene(GAME_BOARD_SCENE_PATH)
	if board == null:
		return

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", false)

	await process_frame
	await process_frame

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

	board.queue_free()
	await process_frame


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
