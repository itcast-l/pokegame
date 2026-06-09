extends SceneTree

const ROOM_NAME := "CodexSmokeReclaimPending"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 35.0
const FIRST_DISCONNECT_DELAY_SECONDS := 0.35
const SECOND_DISCONNECT_DELAY_SECONDS := 0.75
const RECONNECT_SETTLE_SECONDS := 2.0

var _network: Node
var _started_match := false
var _board_initialized := false
var _disconnect_count := 0
var _seat_mode_transition_count_by_name := {}
var _last_seat_mode := ""
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
			_track_seat_control_transitions()

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
	_track_seat_control_transitions()
	_schedule_disconnect(FIRST_DISCONNECT_DELAY_SECONDS)


func _schedule_disconnect(delay_seconds: float) -> void:
	await create_timer(delay_seconds).timeout
	if _finished:
		return

	var peer_id := _get_current_client_peer_id()
	if peer_id <= 0:
		_fail("Missing client peer id for disconnect")
		return

	var peer := _network.multiplayer.multiplayer_peer
	if peer == null:
		_fail("Host multiplayer peer missing before disconnect")
		return

	_disconnect_count += 1
	print("HOST_FORCE_DISCONNECT_%d" % _disconnect_count)
	peer.disconnect_peer(peer_id)


func _get_current_client_peer_id() -> int:
	var players: Array = _network.GetPlayersVariant()
	for raw_player in players:
		var player: Dictionary = raw_player
		if bool(player.get("is_host", false)):
			continue

		return int(player.get("peer_id", 0))

	return 0


func _track_seat_control_transitions() -> void:
	var seat_states: Array = _network.GetSeatControlStatesVariant(true)
	for raw_state in seat_states:
		var state: Dictionary = raw_state
		if int(state.get("seat_index", -1)) != 1:
			continue

		var mode := String(state.get("mode", ""))
		if mode == _last_seat_mode:
			return

		_last_seat_mode = mode
		var transition_count := int(_seat_mode_transition_count_by_name.get(mode, 0)) + 1
		_seat_mode_transition_count_by_name[mode] = transition_count
		if mode == "AiProxyReclaimable":
			print("HOST_SAW_AI_%d" % transition_count)
		elif mode == "HumanReclaimPending":
			print("HOST_SAW_RECLAIM_%d" % transition_count)
			if transition_count == 1:
				_schedule_disconnect(SECOND_DISCONNECT_DELAY_SECONDS)
			elif transition_count == 2:
				_finish_after_settle()

		return


func _finish_after_settle() -> void:
	await create_timer(RECONNECT_SETTLE_SECONDS).timeout
	if _finished:
		return

	_finish()


func _finish() -> void:
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
