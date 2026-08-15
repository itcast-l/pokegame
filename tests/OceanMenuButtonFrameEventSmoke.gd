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
	if int(frame.call("DebugGetObservedSubtreeNodeCount")) != 3:
		_fail("Initial observed subtree node count is incorrect")
		return
	if int(frame.call("DebugGetPrimaryButtonInstanceId")) != button.get_instance_id():
		_fail("Named direct Button was not selected as the primary button")
		return
	if not await _assert_stable(frame):
		return

	var unrelated_root := Control.new()
	root.add_child(unrelated_root)
	await process_frame
	var unrelated_callback_baseline := int(frame.call("DebugGetSubtreeChangeCallbackCount"))
	for index in range(100):
		var unrelated_node := Control.new()
		unrelated_node.name = "Unrelated%d" % index
		unrelated_root.add_child(unrelated_node)
	await _wait_frames(2)
	if int(frame.call("DebugGetSubtreeChangeCallbackCount")) != unrelated_callback_baseline:
		_fail("Unrelated scene-tree additions reached the frame subtree observer")
		return
	for unrelated_node in unrelated_root.get_children():
		unrelated_node.queue_free()
	await _wait_frames(3)
	if int(frame.call("DebugGetSubtreeChangeCallbackCount")) != unrelated_callback_baseline:
		_fail("Unrelated scene-tree removals reached the frame subtree observer")
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

	var nested_container := VBoxContainer.new()
	var dynamic_button := Button.new()
	var dynamic_range := HSlider.new()
	nested_container.add_child(dynamic_button)
	nested_container.add_child(dynamic_range)
	frame.add_child(nested_container)
	await _wait_frames(4)
	if int(frame.call("DebugGetBoundInteractiveControlCount")) != 4:
		_fail("Nested dynamic interactive descendants were not bound")
		return
	if int(frame.call("DebugGetObservedSubtreeNodeCount")) != 6:
		_fail("Nested dynamic subtree was not observed incrementally")
		return
	dynamic_button.emit_signal("mouse_entered")
	dynamic_range.emit_signal("focus_entered")
	if not await _wait_for_glow(frame, 0.95):
		return
	dynamic_button.emit_signal("mouse_exited")
	await _wait_frames(3)
	if float(frame.call("DebugGetGlow")) < 0.95:
		_fail("Nested focus state was overwritten by another control's hover exit")
		return
	dynamic_range.emit_signal("focus_exited")

	nested_container.queue_free()
	await _wait_frames(4)
	if int(frame.call("DebugGetBoundInteractiveControlCount")) != 2:
		_fail("Removed nested subtree remained bound")
		return
	if int(frame.call("DebugGetObservedSubtreeNodeCount")) != 3:
		_fail("Removed nested subtree remained observed")
		return
	if not await _assert_stable(frame):
		return

	var fallback_button := Button.new()
	fallback_button.name = "Fallback"
	frame.add_child(fallback_button)
	await _wait_frames(2)
	if int(frame.call("DebugGetPrimaryButtonInstanceId")) != button.get_instance_id():
		_fail("Fallback button replaced the preferred named Button")
		return
	button.queue_free()
	await _wait_frames(3)
	if int(frame.call("DebugGetPrimaryButtonInstanceId")) != fallback_button.get_instance_id():
		_fail("Primary button did not switch immediately when the named Button exited")
		return
	var replacement_button := Button.new()
	replacement_button.name = "Button"
	frame.add_child(replacement_button)
	await _wait_frames(2)
	if int(frame.call("DebugGetPrimaryButtonInstanceId")) != replacement_button.get_instance_id():
		_fail("New named Button did not replace the fallback primary button")
		return

	frame.set("Selected", false)
	if not await _wait_for_glow(frame, 0.05):
		return
	var replacement_bound_signal_counts := {
		"mouse_entered": replacement_button.get_signal_connection_list("mouse_entered").size(),
		"focus_entered": replacement_button.get_signal_connection_list("focus_entered").size(),
		"button_down": replacement_button.get_signal_connection_list("button_down").size(),
	}
	root.remove_child(frame)
	await process_frame
	if int(frame.call("DebugGetObservedSubtreeNodeCount")) != 0:
		_fail("Frame retained subtree observers after exiting the tree")
		return
	if int(frame.call("DebugGetBoundInteractiveControlCount")) != 0:
		_fail("Frame retained interactive bindings after exiting the tree")
		return
	for signal_name in replacement_bound_signal_counts:
		var before_exit := int(replacement_bound_signal_counts[signal_name])
		var after_exit := replacement_button.get_signal_connection_list(signal_name).size()
		if after_exit != before_exit - 1:
			_fail("Frame exit did not remove exactly one interactive signal binding for %s (%d -> %d)" % [
				signal_name,
				before_exit,
				after_exit,
			])
			return
	frame.free()
	unrelated_root.queue_free()
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
