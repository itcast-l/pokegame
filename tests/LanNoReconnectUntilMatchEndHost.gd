extends SceneTree

const ROOM_NAME := "CodexSmokeNoReconnectEnd"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 30.0
const DISCONNECT_DELAY_SECONDS := 0.2
const HOST_FINISH_SCORE_POINTS := 100

var _network: Node
var _started_match := false
var _board_initialized := false
var _disconnect_requested := false
var _saw_ai := false
var _host_advanced_to_ai_turn := false
var _ai_turn_completed := false
var _forced_match_end := false
var _saw_game_over := false
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


func _track_progress() -> void:
	var board: Node = current_scene
	if board == null:
		return

	if not _disconnect_requested:
		if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			return

		_schedule_disconnect()
		return

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

	if not _saw_ai:
		return

	var current_turn := int(board.call("DebugGetCurrentTurnPlayerIndex"))
	if not _host_advanced_to_ai_turn and current_turn == 0:
		if bool(board.call("DebugPlayFirstAvailableNetworkHostHandCard")):
			_host_advanced_to_ai_turn = true
			print("HOST_ADVANCED_TO_AI_TURN")
		return

	if _host_advanced_to_ai_turn and not _ai_turn_completed and current_turn == 0:
		_ai_turn_completed = true
		print("HOST_AI_TURN_COMPLETED")

	if not _ai_turn_completed:
		return

	if not _forced_match_end:
		if not board.has_method("DebugAddScoreAndCheckWinner"):
			_fail("Missing DebugAddScoreAndCheckWinner")
			return

		if not bool(board.call("DebugAddScoreAndCheckWinner", 0, HOST_FINISH_SCORE_POINTS)):
			_fail("Failed to force match end after AI takeover")
			return

		_forced_match_end = true
		print("HOST_FORCED_MATCH_END")
		return

	if not _saw_game_over and String(board.call("DebugGetGameLoopPhaseName")) == "GameOver":
		_saw_game_over = true
		print("HOST_SAW_GAME_OVER")
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
