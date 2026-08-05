extends SceneTree

const ROOM_NAME := "CodexSmokeHostEnded"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 25.0
const END_MATCH_DELAY_SECONDS := 0.5

var _network: Node
var _started_match := false
var _board_initialized := false
var _finished := false
var _close_started_at := 0
var _close_execution_baseline := 0
var _close_broadcast_baseline := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	_network.connect("SessionLeaveCompleted", Callable(self, "_on_session_leave_completed"))
	print("HOST_START")
	await process_frame
	_network.HostLan(PLAYER_COUNT, ITEM_MODE, WINNING_SCORE, ROOM_NAME, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if _started_match or players.size() != PLAYER_COUNT:
		return

	var everyone_ready := true
	for player in players:
		if not bool(player.get("is_ready", false)):
			everyone_ready = false

	if not everyone_ready:
		return

	_started_match = true
	print("HOST_START_MATCH")
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("HOST_BOARD_INIT")
	_end_match()


func _end_match() -> void:
	await create_timer(END_MATCH_DELAY_SECONDS).timeout
	if _finished:
		return

	print("HOST_END_MATCH")
	_close_started_at = Time.get_ticks_msec()
	_close_execution_baseline = int(_network.call("DebugGetSessionCloseExecutionCount"))
	_close_broadcast_baseline = int(_network.call("DebugGetHostEndedMatchCloseBroadcastCount"))
	_network.LeaveSessionAfterHostEndedMatch()
	_network.LeaveSessionAfterHostEndedMatch()


func _on_session_leave_completed(_reason: int) -> void:
	if _finished or _close_started_at <= 0:
		return
	var elapsed_seconds := (Time.get_ticks_msec() - _close_started_at) / 1000.0
	var execution_delta := int(_network.call("DebugGetSessionCloseExecutionCount")) - _close_execution_baseline
	var broadcast_delta := int(_network.call("DebugGetHostEndedMatchCloseBroadcastCount")) - _close_broadcast_baseline
	if elapsed_seconds < 0.15 or elapsed_seconds >= 0.5:
		_fail("Host close grace was outside [150ms, 500ms): %.3f" % elapsed_seconds)
		return
	if execution_delta != 1 or broadcast_delta != 1:
		_fail("Repeated close was not idempotent: executions=%d broadcasts=%d" % [execution_delta, broadcast_delta])
		return
	if int(_network.get("State")) != 0:
		_fail("Host close did not settle in Idle")
		return
	print("HOST_CLOSE_METRICS elapsed=%.3f executions=%d broadcasts=%d" % [
		elapsed_seconds,
		execution_delta,
		broadcast_delta,
	])
	_finished = true
	print("HOST_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("HOST_FAIL %s" % message)
	quit(1)
