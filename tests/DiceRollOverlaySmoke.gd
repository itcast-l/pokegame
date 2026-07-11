extends SceneTree

const TIMEOUT_SECONDS := 8.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var overlay: Node = load("res://scripts/DiceRollOverlay.cs").new()
	root.add_child(overlay)
	await process_frame

	if not overlay.has_method("DebugStartPlayRollAwaitProbe"):
		_fail("Missing dice playback await probe")
		return

	await _assert_completed_roll(overlay, 4, "first")
	if _failed:
		return
	await _assert_completed_roll(overlay, 2, "second")
	if _failed:
		return

	var first := {}
	var second := {}
	if not bool(overlay.call("DebugStartPlayRollAwaitProbe", 1, first)):
		_fail("Failed to start replacement roll 1")
		return
	await process_frame
	if not bool(overlay.call("DebugStartPlayRollAwaitProbe", 6, second)):
		_fail("Failed to replace roll 1 with roll 6")
		return
	if not await _wait_for_completed(first, "replacement roll 1"):
		return
	if int(first.get("result", -1)) != 1:
		_fail("Replacement roll 1 completed with wrong result")
		return
	if not await _wait_for_completed(second, "replacement roll 6"):
		return
	if int(second.get("result", -1)) != 6:
		_fail("Replacement roll 6 completed with wrong result")
		return
	if overlay.visible or bool(overlay.get("IsPlaying")):
		_fail("Overlay should be hidden and idle after replacement roll completion")
		return

	overlay.queue_free()
	print("DICE_ROLL_OVERLAY_SMOKE_OK")
	quit(0)


func _assert_completed_roll(overlay: Node, result: int, label: String) -> void:
	var probe := {}
	if not bool(overlay.call("DebugStartPlayRollAwaitProbe", result, probe)):
		_fail("Failed to start %s roll" % label)
		return
	if not overlay.visible or not bool(overlay.get("IsPlaying")):
		_fail("%s roll did not become visible and playing immediately" % label)
		return
	if not await _wait_for_completed(probe, label + " roll"):
		return
	if int(probe.get("result", -1)) != result:
		_fail("%s roll completed with wrong result" % label)
		return
	if bool(probe.get("visible_when_completed", true)) or bool(probe.get("playing_when_completed", true)):
		_fail("%s roll completion observed a visible or playing overlay" % label)
		return
	if overlay.visible or bool(overlay.get("IsPlaying")):
		_fail("%s roll did not hide and stop after completion" % label)


func _wait_for_completed(probe: Dictionary, label: String) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		if bool(probe.get("completed", false)):
			if int(probe.get("completion_count", 0)) != 1:
				_fail("%s completed more than once" % label)
				return false
			return true
		await process_frame
	_fail("%s task remained suspended" % label)
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("DICE_ROLL_OVERLAY_SMOKE_FAIL %s" % message)
	quit(1)
