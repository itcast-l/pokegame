extends SceneTree

const ROOM_NAME := "CodexSmokeBasicTurnSync"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 25.0
const POST_SYNC_SETTLE_SECONDS := 0.4
const LOBBY_STATE := 4
const LOADING_SCENE_STATE := 6
const IN_GAME_STATE := 7

var _network: Node
var _started_match := false
var _board_initialized := false
var _played_card := false
var _finished := false
var _state_sequence: Array[int] = []
var _loading_scene_count := 0
var _saw_loading_handshake := false
var _saw_lobby_after_loading := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
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

	for player in players:
		if not bool(player.get("is_ready", false)):
			return

	_started_match = true
	print("HOST_START_MATCH")
	_network.StartMatch()


func _on_state_changed(state: int) -> void:
	_state_sequence.append(state)
	print("HOST_STATE %d" % state)
	if state == LOADING_SCENE_STATE:
		_loading_scene_count += 1
		_saw_loading_handshake = bool(_network.call("DebugIsLoadHandshakeTimeoutActive"))
		print("HOST_LOAD_HANDSHAKE_ACTIVE %s" % _saw_loading_handshake)
		return

	if _loading_scene_count > 0 and state == LOBBY_STATE:
		_saw_lobby_after_loading = true


func _assert_match_start_reentrancy_invariants() -> bool:
	if _loading_scene_count != 1:
		_fail("LoadingScene was reached %d times; states=%s" % [_loading_scene_count, _state_sequence])
		return false

	if not _saw_loading_handshake:
		_fail("Load handshake timeout was inactive during LoadingScene")
		return false

	if _saw_lobby_after_loading:
		_fail("Lobby was reached after LoadingScene; states=%s" % _state_sequence)
		return false

	return true


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	if not _assert_match_start_reentrancy_invariants():
		return

	if _state_sequence.is_empty() or _state_sequence.back() != IN_GAME_STATE:
		_fail("Board initialized outside InGame; states=%s" % _state_sequence)
		return

	if bool(_network.call("DebugIsLoadHandshakeTimeoutActive")):
		_fail("Load handshake timeout remained active after InGame")
		return

	_board_initialized = true
	print("HOST_BOARD_INIT")
	print("HOST_REENTRANCY_GUARD_OK")
	_track_progress()


func _track_progress() -> void:
	var board: Node = current_scene
	if board == null:
		return

	if not _played_card:
		if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			return

		if not bool(board.call("DebugPlayFirstAvailableNetworkHostHandCard")):
			return

		_played_card = true
		print("HOST_PLAYED_CARD")
		return

	if int(board.call("DebugGetCurrentTurnPlayerIndex")) != 1:
		return

	print("HOST_SAW_CLIENT_TURN")
	_finish_after_settle()


func _finish_after_settle() -> void:
	await create_timer(POST_SYNC_SETTLE_SECONDS).timeout
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
