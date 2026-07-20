extends SceneTree

const ROOM_NAME := "CodexRematch"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 45.0
const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const LOBBY_SCENE_SUFFIX := "scenes/multiplayer_lobby_scene.tscn"

var _network: Node
var _initial_session_id := ""
var _first_match_id := ""
var _first_credentials_by_seat: Dictionary = {}
var _board_initialize_count := 0
var _forced_first_winner := false
var _returned_to_lobby := false
var _started_second_match := false
var _second_match_verified := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("HOST_START")
	await process_frame
	_network.HostLan(PLAYER_COUNT, ITEM_MODE, WINNING_SCORE, ROOM_NAME, "rematch-secret")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_progress()

	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if _board_initialize_count > 0 or _started_second_match or players.size() != PLAYER_COUNT:
		return
	for player in players:
		if not bool(player.get("is_ready", false)):
			return
	print("HOST_START_FIRST_MATCH")
	_network.StartMatch()


func _on_board_initialize(payload: Dictionary) -> void:
	_board_initialize_count += 1
	if _board_initialize_count == 1:
		_initial_session_id = String(payload.get("session_id", ""))
		_first_match_id = String(payload.get("match_id", ""))
		if _initial_session_id.is_empty() or _first_match_id.is_empty():
			_fail("First match context was empty")
			return
		for raw_state in _network.GetSeatControlStatesVariant(true):
			var state: Dictionary = raw_state
			_first_credentials_by_seat[int(state.get("seat_index", -1))] = {
				"player_session_id": String(state.get("player_session_id", "")),
				"credential_verifier": String(state.get("credential_verifier", "")),
				"credential_generation": int(state.get("credential_generation", 0)),
			}
		print("HOST_FIRST_BOARD_INIT match=%s" % _first_match_id)
		return

	if _board_initialize_count == 2:
		var second_match_id := String(payload.get("match_id", ""))
		if second_match_id.is_empty() or second_match_id == _first_match_id:
			_fail("Second match did not receive a new MatchId")
			return
		if String(payload.get("session_id", "")) != _initial_session_id:
			_fail("SessionId changed before second match")
			return
		print("HOST_SECOND_BOARD_INIT match=%s" % second_match_id)


func _track_progress() -> void:
	if _finished:
		return
	if _network.State == LOBBY_STATE and _board_initialize_count == 1:
		_track_returned_lobby()
		return
	if _network.State != IN_GAME_STATE or current_scene == null:
		return

	if _board_initialize_count == 1 and not _forced_first_winner:
		if String(current_scene.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			return
		if not bool(current_scene.call("DebugAddScoreAndCheckWinner", 0, WINNING_SCORE)):
			_fail("Failed to force the first winner")
			return
		_forced_first_winner = true
		print("HOST_FORCED_FIRST_WINNER")
		return

	if _board_initialize_count == 2 and not _second_match_verified:
		if String(current_scene.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			return
		if int(current_scene.call("DebugGetWinnerPlayerIndex")) != -1:
			_fail("Second match retained the first winner")
			return
		for player_index in range(PLAYER_COUNT):
			if int(current_scene.call("DebugGetPlayerScoreValue", player_index)) != 0:
				_fail("Second match retained a player score")
				return
			var card_count := int(current_scene.call("DebugGetDeckCardCountForPlayer", player_index)) \
				+ int(current_scene.call("DebugGetTotalHandCardCountForPlayer", player_index))
			if card_count <= 0:
				_fail("Second match did not rebuild player cards")
				return
		_second_match_verified = true
		print("HOST_SECOND_MATCH_RESET_OK")
		_finish_after_settle()


func _track_returned_lobby() -> void:
	if _returned_to_lobby:
		if not _started_second_match and _is_lobby_scene_ready():
			_started_second_match = true
			print("HOST_START_SECOND_MATCH")
			_network.StartMatch()
		return

	var config: Dictionary = _network.GetCurrentConfigVariant()
	if String(config.get("session_id", "")) != _initial_session_id:
		_fail("SessionId changed after return")
		return
	if not String(config.get("match_id", "")).is_empty() or int(config.get("match_seed", -1)) != 0:
		_fail("Completed match context was not cleared")
		return
	if String(config.get("room_name", "")) != ROOM_NAME \
			or int(config.get("player_count", 0)) != PLAYER_COUNT \
			or bool(config.get("item_mode", false)) != ITEM_MODE \
			or int(config.get("winning_score", 0)) != WINNING_SCORE:
		_fail("Room settings changed after return")
		return
	var players: Array = _network.GetPlayersVariant()
	if players.size() != PLAYER_COUNT:
		_fail("Returned lobby roster size changed")
		return
	for player in players:
		if not bool(player.get("is_connected", false)) \
				or not bool(player.get("is_ready", false)) \
				or not bool(player.get("credentials_acknowledged", false)):
			_fail("Returned lobby did not preserve connected ready players")
			return
	var seat_states: Array = _network.GetSeatControlStatesVariant(true)
	if seat_states.size() != PLAYER_COUNT or not bool(_network.call("DebugHasMigrationArtifacts")):
		_fail("Returned lobby did not rebuild seat control and migration state")
		return
	for raw_state in seat_states:
		var state: Dictionary = raw_state
		var seat_index := int(state.get("seat_index", -1))
		var first: Dictionary = _first_credentials_by_seat.get(seat_index, {})
		if String(state.get("mode", "")) != "HumanConnected" \
				or String(state.get("player_session_id", "")) != String(first.get("player_session_id", "")) \
				or String(state.get("credential_verifier", "")).is_empty() \
				or String(state.get("credential_verifier", "")) != String(first.get("credential_verifier", "")) \
				or int(state.get("credential_generation", 0)) != int(first.get("credential_generation", -1)):
			_fail("Returned lobby did not preserve committed seat credentials")
			return
	_returned_to_lobby = true
	print("HOST_RETURNED_LOBBY_OK")


func _is_lobby_scene_ready() -> bool:
	return current_scene != null \
		and String(current_scene.scene_file_path).ends_with(LOBBY_SCENE_SUFFIX)


func _finish_after_settle() -> void:
	await create_timer(0.8).timeout
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
