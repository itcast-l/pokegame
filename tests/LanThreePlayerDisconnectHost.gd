extends SceneTree

const ROOM_NAME := "CodexSmokeThreePlayer"
const PLAYER_COUNT := 3
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 25.0
const STABILIZE_AFTER_AI_SECONDS := 3.0

var _network: Node
var _started_match := false
var _board_initialized := false
var _saw_ai := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("HOST_START")
	await process_frame
	_network.HostLan(PLAYER_COUNT, ITEM_MODE, WINNING_SCORE, ROOM_NAME, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if _board_initialized:
			_track_seat_control_state()

	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if _started_match or players.size() != PLAYER_COUNT:
		return

	for player in players:
		if not bool(player.get("is_ready", false)):
			return

	_started_match = true
	print("HOST_START_MATCH")
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("HOST_BOARD_INIT")


func _track_seat_control_state() -> void:
	if _saw_ai:
		return

	var seat_states: Array = _network.GetSeatControlStatesVariant(true)
	for raw_state in seat_states:
		var state: Dictionary = raw_state
		if int(state.get("seat_index", -1)) != 2:
			continue

		if String(state.get("mode", "")) != "AiProxyReclaimable":
			return

		_saw_ai = true
		print("HOST_SAW_AI_SEAT_2")
		_finish_after_stabilize()
		return


func _finish_after_stabilize() -> void:
	await create_timer(STABILIZE_AFTER_AI_SECONDS).timeout
	if _finished:
		return

	_finished = true
	if _network != null:
		_network.LeaveSession()
	print("HOST_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("HOST_FAIL %s" % message)
	quit(1)
