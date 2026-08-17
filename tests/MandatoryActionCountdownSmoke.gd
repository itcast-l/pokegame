extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_FRAMES := 240

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load board scene")
		return

	var board := GameBoardSmokeFactory.create_solo(packed_scene)
	root.add_child(board)
	await process_frame

	for method in [
		"DebugStartMandatoryActionCountdownProbe",
		"DebugStopMandatoryActionCountdownProbe",
		"DebugGetMandatoryActionCountdownState",
		"DebugIsBoardFrameProcessing",
	]:
		if not board.has_method(method):
			_fail("Missing countdown debug hook: %s" % method)
			return

	if not bool(board.call("DebugStartMandatoryActionCountdownProbe", 1.0)):
		_fail("Could not start countdown probe")
		return
	var initial_state := board.call("DebugGetMandatoryActionCountdownState") as Dictionary
	if int(initial_state.get("display_seconds", -1)) != 1 \
			or String(initial_state.get("rendered_text", "")) != "1":
		_fail("Countdown label did not use the requested duration: %s" % str(initial_state))
		return

	var reached_timeout := false
	for _frame in TIMEOUT_FRAMES:
		await process_frame
		var state := board.call("DebugGetMandatoryActionCountdownState") as Dictionary
		if int(state.get("display_seconds", -1)) == 0 and bool(state.get("urgent", false)):
			reached_timeout = true
			break
	if not reached_timeout:
		_fail("Countdown did not cross the urgent threshold and reach zero")
		return

	var timed_out_state := board.call("DebugGetMandatoryActionCountdownState") as Dictionary
	if int(timed_out_state.get("text_write_count", -1)) != 1 \
			or int(timed_out_state.get("urgent_state_write_count", -1)) != 1 \
			or int(timed_out_state.get("max_stable_tick_allocated_bytes", -1)) != 0:
		_fail("Countdown wrote duplicate state or allocated on stable frames: %s" % str(timed_out_state))
		return

	for _frame in 12:
		await process_frame
	var stable_state := board.call("DebugGetMandatoryActionCountdownState") as Dictionary
	if int(stable_state.get("text_write_count", -1)) != 1 \
			or int(stable_state.get("urgent_state_write_count", -1)) != 1 \
			or int(stable_state.get("last_tick_allocated_bytes", -1)) != 0:
		_fail("Countdown rewrote unchanged state after timeout: %s" % str(stable_state))
		return

	board.call("DebugStopMandatoryActionCountdownProbe")
	await process_frame
	var stopped_state := board.call("DebugGetMandatoryActionCountdownState") as Dictionary
	if bool(stopped_state.get("active", true)) or bool(board.call("DebugIsBoardFrameProcessing")):
		_fail("Countdown cleanup left the board frame loop active")
		return

	if not bool(board.call("DebugStartMandatoryActionCountdownProbe", 1.0)):
		_fail("Could not restart countdown before scene exit")
		return
	board.queue_free()
	for _frame in 8:
		await process_frame

	print("MANDATORY_ACTION_COUNTDOWN_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("MANDATORY_ACTION_COUNTDOWN_SMOKE_FAIL %s" % message)
	quit(1)
