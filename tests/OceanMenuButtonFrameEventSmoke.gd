extends SceneTree

const FRAME_SCRIPT_PATH := "res://scripts/OceanMenuButtonFrame.cs"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	Engine.max_fps = 60
	var frame_script := load(FRAME_SCRIPT_PATH)
	if frame_script == null:
		_fail("OceanMenuButtonFrame script is unavailable")
		return
	var frame := frame_script.new() as Control
	var button := Button.new()
	button.name = "Button"
	var line_edit := LineEdit.new()
	line_edit.name = "LineEdit"
	frame.add_child(button)
	frame.add_child(line_edit)
	root.add_child(frame)
	await _wait_frames(45)
	if int(frame.call("DebugGetBoundInteractiveControlCount")) != 2:
		_fail("Initial interactive descendant binding count is incorrect")
		return
	if not await _assert_stable(frame):
		return

	button.emit_signal("mouse_entered")
	line_edit.emit_signal("mouse_entered")
	if not await _wait_for_glow(frame, 0.95):
		return
	button.emit_signal("mouse_exited")
	await _wait_frames(3)
	if float(frame.call("DebugGetGlow")) < 0.95:
		_fail("One descendant cleared another descendant's hover state")
		return
	line_edit.emit_signal("mouse_exited")
	if not await _wait_for_glow(frame, 0.05):
		return

	line_edit.grab_focus()
	if not await _wait_for_glow(frame, 0.95):
		return
	line_edit.release_focus()
	if not await _wait_for_glow(frame, 0.05):
		return

	button.emit_signal("button_down")
	if not await _wait_for_glow(frame, 0.60):
		return
	button.emit_signal("button_up")
	if not await _wait_for_glow(frame, 0.05):
		return

	frame.set("Selected", true)
	if not await _wait_for_glow(frame, 0.54):
		return
	frame.visible = false
	await _wait_frames(3)
	if frame.is_processing():
		_fail("Hidden button frame kept processing")
		return
	frame.visible = true
	await _wait_frames(3)

	var dynamic_button := Button.new()
	frame.add_child(dynamic_button)
	await _wait_frames(4)
	if int(frame.call("DebugGetBoundInteractiveControlCount")) != 3:
		_fail("Dynamic descendant was not bound")
		return
	dynamic_button.queue_free()
	await _wait_frames(4)
	if int(frame.call("DebugGetBoundInteractiveControlCount")) != 2:
		_fail("Deleted descendant remained bound")
		return
	if not await _assert_stable(frame):
		return

	frame.queue_free()
	await process_frame
	print("OCEAN_MENU_BUTTON_FRAME_EVENT_SMOKE_OK")
	quit(0)


func _wait_for_glow(frame: Control, threshold: float) -> bool:
	for _frame in range(90):
		var glow := float(frame.call("DebugGetGlow"))
		if (threshold > 0.5 and glow >= threshold) or (threshold <= 0.5 and glow <= threshold):
			return true
		await process_frame
	_fail("Glow did not reach expected threshold %.2f (actual %.3f)" % [
		threshold,
		float(frame.call("DebugGetGlow")),
	])
	return false


func _assert_stable(frame: Control) -> bool:
	await _wait_frames(20)
	var process_count := int(frame.call("DebugGetProcessCallCount"))
	await _wait_frames(300)
	if int(frame.call("DebugGetProcessCallCount")) != process_count:
		_fail("Stable button frame continued processing")
		return false
	return true


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	push_error(message)
	print("OCEAN_MENU_BUTTON_FRAME_EVENT_SMOKE_FAIL %s" % message)
	quit(1)
