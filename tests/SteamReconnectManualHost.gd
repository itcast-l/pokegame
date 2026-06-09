extends SceneTree

const ROOM_NAME := "CodexSteamReconnect"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 120.0
const LOBBY_STATE := 4
const POST_RECLAIM_SETTLE_SECONDS := 1.5

var _network: Node
var _started_match := false
var _board_initialized := false
var _printed_lobby_id := false
var _saw_ai := false
var _saw_reclaim := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	if not bool(_network.get("SteamAvailable")):
		_fail("Steam is not available")
		return

	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("HOST_START")
	await process_frame
	_network.HostSteam(PLAYER_COUNT, ITEM_MODE, WINNING_SCORE, ROOM_NAME, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_maybe_print_lobby_id()
		if _board_initialized:
			_track_seat_control_transitions()

	if not _finished:
		_fail("Host timed out")


func _on_state_changed(state: int) -> void:
	print("HOST_STATE %d" % state)
	if state == LOBBY_STATE:
		_maybe_print_lobby_id()


func _on_player_list_updated(players: Array) -> void:
	if _board_initialized:
		_track_seat_control_transitions()
		return

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
	_track_seat_control_transitions()


func _maybe_print_lobby_id() -> void:
	if _printed_lobby_id or not _network.has_method("DebugGetCurrentSteamLobbyId"):
		return

	var lobby_id := int(_network.call("DebugGetCurrentSteamLobbyId"))
	if lobby_id == 0:
		return

	_printed_lobby_id = true
	print("HOST_STEAM_LOBBY_ID %d" % lobby_id)


func _track_seat_control_transitions() -> void:
	var seat_states: Array = _network.GetSeatControlStatesVariant(true)
	for raw_state in seat_states:
		var state: Dictionary = raw_state
		if int(state.get("seat_index", -1)) != 1:
			continue

		var mode := String(state.get("mode", ""))
		if mode == "AiProxyReclaimable" and not _saw_ai:
			_saw_ai = true
			print("HOST_SAW_AI")
		elif mode == "HumanReclaimPending" and not _saw_reclaim:
			_saw_reclaim = true
			print("HOST_SAW_RECLAIM")
			_finish_after_settle()
		return


func _finish_after_settle() -> void:
	await create_timer(POST_RECLAIM_SETTLE_SECONDS).timeout
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
