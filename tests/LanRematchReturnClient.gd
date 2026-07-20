extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 45.0
const LOBBY_STATE := 4
const LOBBY_SCENE_SUFFIX := "scenes/multiplayer_lobby_scene.tscn"

var _network: Node
var _initial_session_id := ""
var _first_match_id := ""
var _ready_sent := false
var _board_initialize_count := 0
var _return_requested := false
var _returned_lobby_verified := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("CLIENT_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "rematch-secret")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_progress()

	if not _finished:
		_fail("Client timed out")


func _on_state_changed(state: int) -> void:
	print("CLIENT_STATE %d" % state)
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		print("CLIENT_READY")
		_network.SetLocalReady(true)


func _on_board_initialize(payload: Dictionary) -> void:
	_board_initialize_count += 1
	if _board_initialize_count == 1:
		_initial_session_id = String(payload.get("session_id", ""))
		_first_match_id = String(payload.get("match_id", ""))
		print("CLIENT_FIRST_BOARD_INIT match=%s" % _first_match_id)
		return

	if _board_initialize_count == 2:
		var second_match_id := String(payload.get("match_id", ""))
		if second_match_id.is_empty() or second_match_id == _first_match_id:
			_fail("Client did not receive a new second MatchId")
			return
		if String(payload.get("session_id", "")) != _initial_session_id:
			_fail("Client SessionId changed before second match")
			return
		print("CLIENT_SECOND_BOARD_INIT match=%s" % second_match_id)


func _track_progress() -> void:
	if _finished:
		return
	if _network.State == LOBBY_STATE and _return_requested:
		_track_returned_lobby()
		return
	if _board_initialize_count == 2:
		_track_second_match()
		return
	if _board_initialize_count != 1 or _return_requested or current_scene == null:
		return
	if int(current_scene.call("DebugGetWinnerPlayerIndex")) < 0 \
			or not bool(current_scene.call("DebugIsWinnerBannerVisible")):
		return
	if not bool(current_scene.call("DebugPressWinnerPrimaryButton")):
		_fail("Client rematch button was disabled or rejected")
		return
	_return_requested = true
	print("CLIENT_REQUESTED_RETURN")


func _track_returned_lobby() -> void:
	if not _returned_lobby_verified:
		var config: Dictionary = _network.GetCurrentConfigVariant()
		if String(config.get("session_id", "")) != _initial_session_id \
				or not String(config.get("match_id", "")).is_empty():
			_fail("Client match context was not preserved and cleared")
			return
		if int(config.get("player_count", 0)) != PLAYER_COUNT \
				or bool(config.get("item_mode", false)) != ITEM_MODE \
				or int(config.get("winning_score", 0)) != WINNING_SCORE:
			_fail("Client room settings changed after return")
			return
		var players: Array = _network.GetPlayersVariant()
		if players.size() != PLAYER_COUNT:
			_fail("Client returned roster size changed")
			return
		for player in players:
			if not bool(player.get("is_connected", false)) or not bool(player.get("is_ready", false)):
				_fail("Client returned roster lost ready state")
				return
		_returned_lobby_verified = true
		print("CLIENT_RETURNED_LOBBY_OK")


func _track_second_match() -> void:
	if current_scene == null:
		return
	if String(current_scene.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return
	if int(current_scene.call("DebugGetWinnerPlayerIndex")) != -1:
		_fail("Client second match retained the first winner")
		return
	for player_index in range(PLAYER_COUNT):
		if int(current_scene.call("DebugGetPlayerScoreValue", player_index)) != 0:
			_fail("Client second match retained a player score")
			return
	print("CLIENT_SECOND_MATCH_RESET_OK")
	_finished = true
	_network.LeaveSession()
	print("CLIENT_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_FAIL %s" % message)
	quit(1)
