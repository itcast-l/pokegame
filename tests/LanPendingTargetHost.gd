extends SceneTree

const ROOM_NAME := "CodexSmokePendingTarget"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 25.0
const DISCONNECT_DELAY_SECONDS := 0.35

var _network: Node
var _started_match := false
var _board_initialized := false
var _selection_started := false
var _disconnect_requested := false
var _saw_ai := false
var _saw_target_hand := false
var _owner_hand_count_before := -1
var _owner_hand_count_after_start := -1
var _target_hand_count_before := -1
var _finished := false
var _force_ai_fallback := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_force_ai_fallback = OS.get_environment("CODEX_FORCE_AI_FALLBACK") == "1"
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
	_track_progress()


func _schedule_disconnect() -> void:
	if _disconnect_requested:
		return

	_disconnect_requested = true
	await create_timer(DISCONNECT_DELAY_SECONDS).timeout
	var peer_id := _get_current_client_peer_id()
	if peer_id <= 0:
		_fail("Missing client peer id for disconnect")
		return

	var peer := _network.multiplayer.multiplayer_peer
	if peer == null:
		_fail("Host multiplayer peer missing before disconnect")
		return

	print("HOST_FORCE_DISCONNECT")
	peer.disconnect_peer(peer_id)


func _get_current_client_peer_id() -> int:
	var players: Array = _network.GetPlayersVariant()
	for raw_player in players:
		var player: Dictionary = raw_player
		if bool(player.get("is_host", false)):
			continue

		return int(player.get("peer_id", 0))

	return 0


func _track_progress() -> void:
	var board: Node = current_scene
	if board == null:
		return

	if not _selection_started:
		if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			return

		_owner_hand_count_before = int(board.call("DebugGetTotalHandCardCountForPlayer", 1))
		_target_hand_count_before = int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
		if _owner_hand_count_before <= 0 or _target_hand_count_before <= 0:
			return

		if not bool(board.call("DebugStartPendingTargetHandSelectionForSeat", 1)):
			_fail("Failed to start pending target-hand selection")
			return

		_owner_hand_count_after_start = int(board.call("DebugGetTotalHandCardCountForPlayer", 1))
		if _owner_hand_count_after_start != _owner_hand_count_before + 1:
			_fail("Seat 1 hand count did not include pending target item")
			return

		_selection_started = true
		print("HOST_STARTED_PENDING_TARGET")
		if _force_ai_fallback:
			board.call("DebugForceNextPendingTargetSelectionAiFallback")
			print("HOST_FORCED_TARGET_FALLBACK")
		_schedule_disconnect()

	var pending_mode := String(board.call("DebugGetPendingItemSelectionModeName"))
	if not _saw_target_hand and pending_mode == "TargetHandCard":
		_saw_target_hand = true
		print("HOST_SAW_TARGET_HAND")

	if not _saw_ai:
		var seat_states: Array = _network.GetSeatControlStatesVariant(true)
		for raw_state in seat_states:
			var state: Dictionary = raw_state
			if int(state.get("seat_index", -1)) != 1:
				continue

			if String(state.get("mode", "")) == "AiProxyReclaimable":
				_saw_ai = true
				print("HOST_SAW_AI")
			break

	if not _selection_started or not _saw_ai or pending_mode != "None":
		return

	if not _saw_target_hand:
		_fail("Pending target selection never advanced to target-hand mode")
		return

	if String(board.call("DebugGetGameLoopPhaseName")) != "AwaitingMainPlayerAction":
		return

	if int(board.call("DebugGetTotalHandCardCountForPlayer", 1)) != _owner_hand_count_before + 1:
		_fail("Seat 1 hand count did not increase after target selection resolved")
		return

	if int(board.call("DebugGetTotalHandCardCountForPlayer", 0)) != _target_hand_count_before - 1:
		_fail("Seat 0 hand count did not decrease after target selection resolved")
		return

	print("HOST_TARGET_RESOLVED")
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
