extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const SINGLE_PLAYER_BUTTON_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame/Button"
const SINGLE_PLAYER_FRAME_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SinglePlayerFrame"
const TIMEOUT_FRAMES := 120

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(1280, 720)
	Input.warp_mouse(Vector2(0.0, 0.0))
	var menu := await _instantiate_menu()
	if menu == null:
		return

	var single_player_button := menu.get_node_or_null(SINGLE_PLAYER_BUTTON_PATH) as Button
	var single_player_frame := menu.get_node_or_null(SINGLE_PLAYER_FRAME_PATH) as Control
	if single_player_button == null or single_player_frame == null:
		_fail("Main-menu single-player controls are missing")
		return
	if not single_player_button.has_focus():
		_fail("Single-player should retain the initial keyboard/gamepad focus target")
		return
	if bool(menu.call("DebugIsPrimaryMenuFocusHighlightVisible")):
		_fail("Main-menu focus highlight is visible before navigation input")
		return
	if not await _wait_for_glow(single_player_frame, 0.05, false):
		_fail("Single-player retained its selected glow in the initial pointer state")
		return

	var down_event := InputEventAction.new()
	down_event.action = "ui_down"
	down_event.pressed = true
	Input.parse_input_event(down_event)
	await process_frame
	down_event.pressed = false
	Input.parse_input_event(down_event)
	if not bool(menu.call("DebugIsPrimaryMenuFocusHighlightVisible")):
		_fail("Keyboard navigation did not reveal the main-menu focus highlight")
		return

	var focused_control := root.gui_get_focus_owner() as Control
	if focused_control == null or focused_control.get_parent() is not Control:
		_fail("Keyboard navigation did not retain a main-menu focus target")
		return
	var focused_frame := focused_control.get_parent() as Control
	if not await _wait_for_glow(focused_frame, 0.35, true):
		_fail("Focused main-menu row did not enter its keyboard highlight state")
		return

	Input.warp_mouse(Vector2(0.0, 0.0))
	var mouse_event := InputEventMouseMotion.new()
	mouse_event.position = Vector2(0.0, 0.0)
	mouse_event.global_position = Vector2(0.0, 0.0)
	Input.parse_input_event(mouse_event)
	await process_frame
	if bool(menu.call("DebugIsPrimaryMenuFocusHighlightVisible")):
		_fail("Pointer input did not hide the main-menu focus highlight")
		return
	if not await _wait_for_glow(focused_frame, 0.05, false):
		_fail("Focused main-menu row stayed selected after returning to pointer input")
		return

	print("MAIN_MENU_FOCUS_VISIBILITY_SMOKE_OK")
	menu.queue_free()
	await _wait_frames(8)
	quit(0)


func _instantiate_menu() -> Node:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load main menu scene")
		return null

	var menu := packed_scene.instantiate()
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(4)
	return menu


func _wait_for_glow(frame: Control, threshold: float, at_least: bool) -> bool:
	for _frame in range(TIMEOUT_FRAMES):
		var glow := float(frame.call("DebugGetGlow"))
		if (at_least and glow >= threshold) or (not at_least and glow <= threshold):
			return true
		await process_frame
	return false


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("MAIN_MENU_FOCUS_VISIBILITY_SMOKE_FAIL %s" % message)
	quit(1)
