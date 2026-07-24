extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const MULTIPLAYER_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/MultiplayerFrame/Button"
const LOBBY_BACK_BUTTON_PATH := "SafeArea/MainLayout/Header/BackButton"
const SUPPRESS_PRESSED_SFX_META := "game_audio_ui_suppress_pressed_sfx"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	var menu := await _instantiate_scene(MAIN_MENU_SCENE_PATH)
	if _failed or menu == null:
		return
	var multiplayer_button := menu.get_node_or_null(MULTIPLAYER_BUTTON_PATH) as Button
	if multiplayer_button == null:
		_fail("Main-menu multiplayer button is missing")
		return
	if not multiplayer_button.has_meta(SUPPRESS_PRESSED_SFX_META) \
			or not bool(multiplayer_button.get_meta(SUPPRESS_PRESSED_SFX_META)):
		_fail("Main-menu multiplayer button did not suppress its default pressed sound")
		return
	if not bool(menu.call("DebugRejectMissingMultiplayerLobbyResource")) \
			or bool(menu.call("DebugAreMainMenuButtonsDisabled")) \
			or not _get_transition_nodes().is_empty():
		_fail("Missing multiplayer lobby resource incorrectly locked or started the entry transition")
		return
	if multiplayer_button.focus_mode == Control.FOCUS_NONE:
		_fail("Main-menu multiplayer button is not keyboard or gamepad focusable")
		return

	multiplayer_button.grab_focus()
	await _press_keyboard_confirm()
	multiplayer_button.emit_signal("pressed")
	await process_frame
	if current_scene != menu:
		_fail("Multiplayer lobby swapped before the circular mask closed")
		return
	if _get_transition_nodes().size() != 1:
		_fail("Rapid multiplayer activation did not create exactly one circular transition")
		return
	if not multiplayer_button.disabled:
		_fail("Multiplayer entry was not locked after its resource loaded")
		return

	var lobby := await _wait_for_scene_change(menu)
	if _failed or lobby == null:
		return
	if not lobby.has_method("DebugIsReturningToMainMenu"):
		_fail("Circular transition did not swap to the multiplayer lobby")
		return
	if not _assert_single_covered_transition("entering the multiplayer lobby"):
		return
	if not await _wait_for_transition_count(0):
		return

	var back_button := lobby.get_node_or_null(LOBBY_BACK_BUTTON_PATH) as Button
	if back_button == null:
		_fail("Multiplayer lobby back button is missing")
		return
	if not back_button.has_meta(SUPPRESS_PRESSED_SFX_META) \
			or not bool(back_button.get_meta(SUPPRESS_PRESSED_SFX_META)):
		_fail("Multiplayer lobby back button did not suppress its default pressed sound")
		return

	back_button.grab_focus()
	await _press_gamepad_confirm()
	back_button.emit_signal("pressed")
	await process_frame
	if current_scene != lobby:
		_fail("Main menu swapped before the lobby exit mask closed")
		return
	if not bool(lobby.call("DebugIsReturningToMainMenu")) or not back_button.disabled:
		_fail("Multiplayer lobby return did not acquire its single-transition lock")
		return
	if _get_transition_nodes().size() != 1:
		_fail("Repeated lobby return input did not preserve a single circular transition")
		return

	var returned_menu := await _wait_for_scene_change(lobby)
	if _failed or returned_menu == null:
		return
	if not returned_menu.has_method("DebugOpenSkillCodex"):
		_fail("Lobby circular transition did not return to the main menu")
		return
	if not _assert_single_covered_transition("returning to the main menu"):
		return
	if not await _wait_for_transition_count(0):
		return

	returned_menu.queue_free()
	await process_frame
	print("MAIN_MENU_MULTIPLAYER_TRANSITION_SMOKE_OK")
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


func _wait_for_scene_change(previous_scene: Node) -> Node:
	var deadline := Time.get_ticks_msec() + 3000
	while current_scene == previous_scene and Time.get_ticks_msec() < deadline:
		await process_frame
	if current_scene == previous_scene or current_scene == null:
		_fail("Timed out waiting for circular scene swap")
		return null
	return current_scene


func _wait_for_transition_count(expected_count: int) -> bool:
	var deadline := Time.get_ticks_msec() + 3000
	while _get_transition_nodes().size() != expected_count and Time.get_ticks_msec() < deadline:
		await process_frame
	if _get_transition_nodes().size() != expected_count:
		_fail("Circular transition count did not settle to %d" % expected_count)
		return false
	return true


func _press_keyboard_confirm() -> void:
	var key_press := InputEventKey.new()
	key_press.keycode = KEY_ENTER
	key_press.pressed = true
	Input.parse_input_event(key_press)
	await process_frame
	var key_release := InputEventKey.new()
	key_release.keycode = KEY_ENTER
	key_release.pressed = false
	Input.parse_input_event(key_release)
	await process_frame


func _press_gamepad_confirm() -> void:
	var joy_press := InputEventJoypadButton.new()
	joy_press.button_index = JOY_BUTTON_A
	joy_press.pressed = true
	Input.parse_input_event(joy_press)
	await process_frame
	var joy_release := InputEventJoypadButton.new()
	joy_release.button_index = JOY_BUTTON_A
	joy_release.pressed = false
	Input.parse_input_event(joy_release)
	await process_frame


func _assert_single_covered_transition(context: String) -> bool:
	var transitions := _get_transition_nodes()
	if transitions.size() != 1:
		_fail("Expected one circular transition while %s" % context)
		return false
	var mask := transitions[0].get_node_or_null("CircularMask") as ColorRect
	var material := mask.material as ShaderMaterial if mask != null else null
	if material == null:
		_fail("Circular transition mask material is missing while %s" % context)
		return false
	var radius := float(material.get_shader_parameter("radius"))
	if radius > 0.001:
		_fail("Scene swapped before the circular mask fully closed while %s: %f" % [context, radius])
		return false
	return true


func _get_transition_nodes() -> Array[Node]:
	var transitions: Array[Node] = []
	for child in root.get_children():
		if child.get_node_or_null("CircularMask") != null:
			transitions.append(child)
	return transitions


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("MAIN_MENU_MULTIPLAYER_TRANSITION_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
