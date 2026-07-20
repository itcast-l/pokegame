extends SceneTree

const ROOM_NAME := "CodexSmokeOwnTurnReconnect"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 35.0
const DISCONNECT_DELAY_SECONDS := 0.15
const POST_CONNECT_SETTLE_SECONDS := 1.0

var _network: Node
var _started_match := false
var _board_initialized := false
var _seeded_client_turn := false
var _disconnect_requested := false
var _saw_ai := false
var _saw_reclaim := false
var _saw_human_restored := false
var _advanced_after_reclaim := false
var _handoff_action_submitted := false
var _client_peer_id := 0
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
			_track_progress()

	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if _board_initialized:
		_track_progress()
		return

	if _started_match or players.size() != PLAYER_COUNT:
		return

	var everyone_ready := true
	for player in players:
		if not bool(player.get("is_ready", false)):
			everyone_ready = false
		if not bool(player.get("is_host", false)):
			_client_peer_id = int(player.get("peer_id", 0))

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
	_track_progress()


func _track_progress() -> void:
	var board: Node = current_scene
	if board == null:
		return

	if not _seeded_client_turn:
		if String(board.call("DebugGetGameLoopPhaseName")) != "OpeningDeal" and bool(board.call("DebugPlayFirstAvailableNetworkHostHandCard")):
			_seeded_client_turn = true
			print("HOST_ADVANCED_TO_CLIENT_TURN")
		return

	var current_turn := int(board.call("DebugGetCurrentTurnPlayerIndex"))
	if not _disconnect_requested and current_turn == 1:
		_schedule_disconnect()

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
		elif mode == "HumanConnected" and _saw_reclaim and not _saw_human_restored:
			_saw_human_restored = true
			print("HOST_SAW_HUMAN_CONNECTED")
			_finish_after_settle()
		break

	if _saw_reclaim and not _advanced_after_reclaim and current_turn == 0 and not _handoff_action_submitted:
		_handoff_action_submitted = bool(board.call("DebugPlayFirstNonCapturingNetworkHostHandCard"))
	elif _saw_reclaim and _handoff_action_submitted and not _advanced_after_reclaim and current_turn == 1:
		_advanced_after_reclaim = true
		print("HOST_ADVANCED_AFTER_RECLAIM")


func _schedule_disconnect() -> void:
	if _disconnect_requested:
		return

	_disconnect_requested = true
	await create_timer(DISCONNECT_DELAY_SECONDS).timeout
	if _client_peer_id <= 0:
		_fail("Missing client peer id for disconnect")
		return

	var peer := _network.multiplayer.multiplayer_peer
	if peer == null:
		_fail("Host multiplayer peer missing before disconnect")
		return

	print("HOST_FORCE_DISCONNECT")
	peer.disconnect_peer(_client_peer_id)


func _finish_after_settle() -> void:
	await create_timer(POST_CONNECT_SETTLE_SECONDS).timeout
	if _finished:
		return

	_finished = true
	_network.LeaveSession()
	print("HOST_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("HOST_FAIL %s" % message)
	quit(1)
