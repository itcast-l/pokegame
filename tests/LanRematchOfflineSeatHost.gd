extends SceneTree

const ROOM_NAME := "CodexOffline"
const PLAYER_COUNT := 2
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 55.0
const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const LOBBY_SCENE_SUFFIX := "scenes/multiplayer_lobby_scene.tscn"

var _network: Node
var _first_match_id := ""
var _board_initialize_count := 0
var _first_match_started := false
var _offline_player_seen := false
var _forced_winner := false
var _return_requested := false
var _offline_roster_removed := false
var _replacement_seen_unready := false
var _unready_start_rejected := false
var _second_match_started := false
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
		_track_progress()

	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if not _first_match_started and players.size() == PLAYER_COUNT:
		for player in players:
			if not bool(player.get("is_ready", false)):
				return
		_first_match_started = true
		print("HOST_START_FIRST_MATCH")
		_network.StartMatch()
		return

	if not _offline_roster_removed or _second_match_started or players.size() != PLAYER_COUNT:
		return
	var all_ready := true
	for player in players:
		if not bool(player.get("is_ready", false)):
			all_ready = false
	if not all_ready:
		if not _replacement_seen_unready:
			_replacement_seen_unready = true
			print("HOST_REPLACEMENT_UNREADY")
		return
	if not _unready_start_rejected:
		_fail("Replacement became ready before the unready start gate was tested")
		return
	_second_match_started = true
	print("HOST_START_SECOND_MATCH")
	_network.StartMatch()


func _on_board_initialize(payload: Dictionary) -> void:
	_board_initialize_count += 1
	if _board_initialize_count == 1:
		_first_match_id = String(payload.get("match_id", ""))
		print("HOST_FIRST_BOARD_INIT")
		return
	if _board_initialize_count != 2:
		_fail("Unexpected board initialize count")
		return
	var second_match_id := String(payload.get("match_id", ""))
	if second_match_id.is_empty() or second_match_id == _first_match_id:
		_fail("Replacement match did not receive a new MatchId")
		return
	print("HOST_SECOND_BOARD_INIT")
	_finish_after_settle()


func _track_progress() -> void:
	if _finished:
		return
	if _network.State == IN_GAME_STATE and _board_initialize_count == 1:
		_track_first_match()
		return
	if _network.State != LOBBY_STATE or not _return_requested:
		return

	var players: Array = _network.GetPlayersVariant()
	if not _offline_roster_removed:
		if players.size() != 1:
			_fail("Offline player remained in returned lobby roster")
			return
		var host: Dictionary = players[0]
		if not bool(host.get("is_host", false)) \
				or not bool(host.get("is_connected", false)) \
				or not bool(host.get("is_ready", false)):
			_fail("Returned host roster state was not preserved")
			return
		if current_scene == null \
				or not String(current_scene.scene_file_path).ends_with(LOBBY_SCENE_SUFFIX):
			return
		_offline_roster_removed = true
		print("HOST_OFFLINE_ROSTER_REMOVED")
		print("HOST_WAITING_REPLACEMENT")
		return

	if _replacement_seen_unready and not _unready_start_rejected:
		_network.StartMatch()
		if _network.State != LOBBY_STATE \
				or not String(_network.GetCurrentConfigVariant().get("match_id", "")).is_empty():
			_fail("Unready replacement was allowed to start")
			return
		_unready_start_rejected = true
		print("HOST_UNREADY_START_REJECTED")


func _track_first_match() -> void:
	var players: Array = _network.GetPlayersVariant()
	if not _offline_player_seen:
		for player in players:
			if not bool(player.get("is_host", false)) and not bool(player.get("is_connected", true)):
				_offline_player_seen = true
				print("HOST_SAW_OFFLINE_PLAYER")
				break
		if not _offline_player_seen:
			return
	if current_scene == null or String(current_scene.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return
	if not _forced_winner:
		if not bool(current_scene.call("DebugAddScoreAndCheckWinner", 0, WINNING_SCORE)):
			_fail("Failed to force winner after disconnect")
			return
		_forced_winner = true
		print("HOST_FORCED_WINNER")
		return
	if int(current_scene.call("DebugGetWinnerPlayerIndex")) < 0:
		return
	if not _return_requested:
		if not bool(_network.RequestReturnToLobbyAfterMatch()):
			_fail("Host return-to-lobby request was rejected")
			return
		_return_requested = true
		print("HOST_REQUESTED_RETURN")


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
