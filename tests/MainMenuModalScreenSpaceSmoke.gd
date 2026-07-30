extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const SETTINGS_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SettingsFrame/Button"
const SINGLE_PLAYER_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button"
const EXIT_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/ExitFrame/Button"
const AVATAR_BUTTON_PATH := "AvatarEntryLayer/MainMenuAvatarEntry/Button"
const SETTINGS_LAYER_PATH := "SettingsDialogLayer"
const SETTINGS_ROOT_PATH := SETTINGS_LAYER_PATH + "/SettingsDialogRoot"
const CONTROLS_ROOT_PATH := SETTINGS_LAYER_PATH + "/ControlsDialogRoot"
const PUSH_CAMERA_PATH := "MenuCameraPushIn"
const AVATAR_LAYER_PATH := "AvatarEditDialogLayer"
const PLAYER_COUNT_LAYER_PATH := "PlayerCountDialogLayer"
const PLAYER_COUNT_ROOT_PATH := PLAYER_COUNT_LAYER_PATH + "/PlayerCountDialogRoot"
const EXIT_LAYER_PATH := "ExitDialogLayer"
const EXIT_ROOT_PATH := EXIT_LAYER_PATH + "/ExitDialogRoot"
const SCREEN_SPACE_SAMPLE_FRAMES := 10

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	var menu := await _instantiate_menu()
	if menu == null:
		return

	var settings_layer := menu.get_node_or_null(SETTINGS_LAYER_PATH) as CanvasLayer
	var settings_root := menu.get_node_or_null(SETTINGS_ROOT_PATH) as Control
	var controls_root := menu.get_node_or_null(CONTROLS_ROOT_PATH) as Control
	var settings_button := menu.get_node_or_null(SETTINGS_BUTTON_PATH) as Button
	var controls_button := settings_root.find_child("ControlsButton", true, false) as Button if settings_root != null else null
	var push_camera := menu.get_node_or_null(PUSH_CAMERA_PATH)
	if settings_layer == null or settings_root == null or controls_root == null \
			or settings_button == null or controls_button == null or push_camera == null:
		_fail("Main-menu settings modal nodes are missing")
		return
	if bool(settings_layer.get("follow_viewport_enabled")):
		_fail("Settings and controls layer must remain screen-space")
		return

	settings_button.emit_signal("pressed")
	if not await _assert_screen_space_during_push(settings_root, push_camera, "settings"):
		return

	controls_button.emit_signal("pressed")
	if not await _assert_screen_space_during_push(controls_root, push_camera, "controls"):
		return

	var controls_done_button := controls_root.find_child("DoneButton", true, false) as Button
	var settings_done_button := settings_root.find_child("DoneButton", true, false) as Button
	var settings_back_button := settings_root.find_child("BackButton", true, false) as Button
	if controls_done_button == null or settings_done_button == null:
		_fail("Main-menu modal close controls are missing")
		return
	if settings_back_button != null:
		_fail("Settings dialog still exposes the removed title-bar back control")
		return
	controls_done_button.emit_signal("pressed")
	settings_done_button.emit_signal("pressed")
	if not await _wait_for_hidden(settings_root, 120):
		_fail("Settings dialog did not close")
		return
	if not await _wait_for_menu_buttons_enabled(menu, 120):
		_fail("Main-menu camera transition lock did not release after closing settings")
		return

	var single_player_button := menu.get_node_or_null(SINGLE_PLAYER_BUTTON_PATH) as Button
	var player_count_layer := menu.get_node_or_null(PLAYER_COUNT_LAYER_PATH) as CanvasLayer
	var player_count_root := menu.get_node_or_null(PLAYER_COUNT_ROOT_PATH) as Control
	if single_player_button == null or player_count_layer == null or player_count_root == null:
		_fail("Single-player modal nodes are missing")
		return
	if bool(player_count_layer.get("follow_viewport_enabled")):
		_fail("Single-player layer must remain screen-space")
		return
	single_player_button.emit_signal("pressed")
	if not await _assert_screen_space_during_push(player_count_root, push_camera, "single-player"):
		return
	var player_count_back := player_count_root.find_child("BackButton", true, false) as Button
	if player_count_back == null:
		_fail("Single-player title-bar back control is missing")
		return
	player_count_back.emit_signal("pressed")
	if not await _wait_for_hidden(player_count_root, 120):
		_fail("Single-player dialog did not close")
		return
	if not await _wait_for_menu_buttons_enabled(menu, 120):
		_fail("Main-menu camera transition lock did not release after closing single-player")
		return

	var avatar_button := menu.get_node_or_null(AVATAR_BUTTON_PATH) as Button
	if avatar_button == null:
		_fail("Main-menu avatar button is missing")
		return
	avatar_button.emit_signal("pressed")
	await process_frame
	var avatar_layer := menu.get_node_or_null(AVATAR_LAYER_PATH) as CanvasLayer
	var avatar_root := avatar_layer.get_node_or_null("CharacterAvatarEditScene") as Control if avatar_layer != null else null
	if avatar_layer == null or avatar_root == null:
		_fail("Avatar editor screen-space layer is missing")
		return
	if bool(avatar_layer.get("follow_viewport_enabled")):
		_fail("Avatar editor layer must remain screen-space")
		return
	if not await _assert_screen_space_during_push(avatar_root, push_camera, "avatar editor"):
		return

	var avatar_back_button := avatar_root.find_child("BackButton", true, false) as Button
	if avatar_back_button == null:
		_fail("Avatar editor close control is missing")
		return
	avatar_back_button.emit_signal("pressed")
	if not await _wait_for_node_removed(menu, AVATAR_LAYER_PATH, 120):
		_fail("Avatar editor screen-space layer was not released on close")
		return
	if not await _wait_for_menu_buttons_enabled(menu, 120):
		_fail("Main-menu camera transition lock did not release after closing avatar editor")
		return

	var exit_button := menu.get_node_or_null(EXIT_BUTTON_PATH) as Button
	var exit_layer := menu.get_node_or_null(EXIT_LAYER_PATH) as CanvasLayer
	var exit_root := menu.get_node_or_null(EXIT_ROOT_PATH) as Control
	if exit_button == null or exit_layer == null or exit_root == null:
		_fail("Exit modal nodes are missing")
		return
	if bool(exit_layer.get("follow_viewport_enabled")):
		_fail("Exit layer must remain screen-space")
		return
	exit_button.emit_signal("pressed")
	if not await _assert_screen_space_during_push(exit_root, push_camera, "exit"):
		return
	var exit_back := exit_root.find_child("BackButton", true, false) as Button
	var exit_cancel := exit_root.find_child("CancelButton", true, false) as Button
	if exit_back != null:
		_fail("Exit dialog still exposes the removed title-bar back control")
		return
	if exit_cancel == null:
		_fail("Exit dialog continue control is missing")
		return
	exit_cancel.emit_signal("pressed")
	if not await _wait_for_hidden(exit_root, 120):
		_fail("Exit dialog did not close")
		return

	print("MAIN_MENU_MODAL_SCREEN_SPACE_SMOKE_OK")
	menu.queue_free()
	await process_frame
	await _dispose_audio_before_quit()
	quit(0)


func _assert_screen_space_during_push(dialog_root: Control, push_camera: Node, dialog_name: String) -> bool:
	await process_frame
	if not dialog_root.visible or not dialog_root.position.is_zero_approx() \
			or not dialog_root.scale.is_equal_approx(Vector2.ONE):
		_fail("%s root did not retain its default screen-space transform" % dialog_name)
		return false
	if int(push_camera.call("get_priority")) != 20:
		_fail("%s did not begin while the main-menu push camera was active" % dialog_name)
		return false

	var root_rect := _get_canvas_rect(dialog_root)
	var panel := dialog_root.get_node_or_null("DialogCenter") as Control
	var panel_rect := _get_canvas_rect(panel) if panel != null else Rect2()
	for _frame in SCREEN_SPACE_SAMPLE_FRAMES:
		await process_frame
		if not dialog_root.position.is_zero_approx() \
				or not dialog_root.scale.is_equal_approx(Vector2.ONE) \
				or not _rect_is_equal_approx(root_rect, _get_canvas_rect(dialog_root)):
			_fail("%s root geometry changed during the background camera push" % dialog_name)
			return false
		if panel != null and not _rect_is_equal_approx(panel_rect, _get_canvas_rect(panel)):
			_fail("%s panel layout changed during the background camera push" % dialog_name)
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


func _wait_for_hidden(control: Control, max_frames: int) -> bool:
	for _frame in max_frames:
		if not control.visible:
			return true
		await process_frame
	return not control.visible


func _wait_for_node_removed(parent: Node, path: NodePath, max_frames: int) -> bool:
	for _frame in max_frames:
		if parent.get_node_or_null(path) == null:
			return true
		await process_frame
	return parent.get_node_or_null(path) == null


func _wait_for_menu_buttons_enabled(menu: Node, max_frames: int) -> bool:
	for _frame in max_frames:
		if not bool(menu.call("DebugAreMainMenuButtonsDisabled")):
			return true
		await process_frame
	return not bool(menu.call("DebugAreMainMenuButtonsDisabled"))


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
	print("MAIN_MENU_MODAL_SCREEN_SPACE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
