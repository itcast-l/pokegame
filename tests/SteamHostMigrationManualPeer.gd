extends SceneTree

const PLAYER_COUNT := 3
const ITEM_MODE := true
const WINNING_SCORE := 50
const ROOM_NAME := "DecklandSteamMigrationReleaseGate"
const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const FAILED_STATE := 9
const MIGRATING_HOST_STATE := 10
const DEFAULT_TIMEOUT_SECONDS := 900.0
const ACTION_INTERVAL_SECONDS := 0.2
const FINISH_SETTLE_SECONDS := 1.0

var _network: Node
var _steam: Object
var _role := ""
var _scenario := ""
var _peer_label := ""
var _expected_outcome := ""
var _build_id := ""
var _result_path := ""
var _lobby_id := 0
var _local_steam_id := 0
var _local_seat := -1
var _last_state := -1
var _last_owner_id := -1
var _winner := -1
var _started_at := 0.0
var _ready_sent := false
var _match_started := false
var _board_initialized := false
var _migration_seen := false
var _became_host := false
var _request_sent := false
var _source_kill_ready_logged := false
var _secondary_owner_requested := false
var _finished := false
var _next_action_at := 0.0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_started_at = Time.get_ticks_msec() / 1000.0
	if not _read_environment():
		return

	_network = root.get_node("/root/NetworkManager")
	if not bool(_network.get("SteamAvailable")) or not Engine.has_singleton("Steam"):
		_fail("Steam is not available")
		return
	_steam = Engine.get_singleton("Steam")
	_local_steam_id = int(_steam.call("getSteamID"))
	if _local_steam_id <= 0:
		_fail("Steam ID is unavailable")
		return

	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	_network.connect("ErrorRaised", Callable(self, "_on_error"))
	_event("START", "steam_id=%d build=%s expected=%s" % [_local_steam_id, _build_id, _expected_outcome])
	await process_frame

	if _role == "host":
		_network.HostSteam(PLAYER_COUNT, ITEM_MODE, WINNING_SCORE, ROOM_NAME, "")
	else:
		_event("JOIN", "lobby=%d" % _lobby_id)
		_network.JoinSteamLobby(_lobby_id, "")

	var timeout_seconds := float(OS.get_environment("CODEX_STEAM_MIGRATION_TIMEOUT_SECONDS"))
	if timeout_seconds <= 0.0:
		timeout_seconds = DEFAULT_TIMEOUT_SECONDS
	var deadline := _started_at + timeout_seconds
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_lobby_id()
		_track_owner()
		_track_local_authority()
		_drive_scenario()
		_drive_action()
		_track_winner()

	if not _finished:
		_fail("Timed out before a finite outcome")


func _read_environment() -> bool:
	_role = OS.get_environment("CODEX_STEAM_MIGRATION_ROLE").strip_edges()
	_scenario = OS.get_environment("CODEX_STEAM_MIGRATION_SCENARIO").strip_edges()
	_peer_label = OS.get_environment("CODEX_STEAM_MIGRATION_PEER_LABEL").strip_edges()
	_expected_outcome = OS.get_environment("CODEX_STEAM_MIGRATION_EXPECTED_OUTCOME").strip_edges()
	_build_id = OS.get_environment("CODEX_BUILD_ID").strip_edges()
	_result_path = OS.get_environment("CODEX_STEAM_MIGRATION_RESULT").strip_edges()
	if _role != "host" and _role != "peer":
		_fail("CODEX_STEAM_MIGRATION_ROLE must be host or peer")
		return false
	if not _scenario in ["active_owner_transfer", "source_crash", "automatic_owner_second_transfer", "owner_timeout", "candidate_exit"]:
		_fail("Unknown Steam migration scenario")
		return false
	if _peer_label.is_empty() or _build_id.is_empty() or _result_path.is_empty():
		_fail("Missing peer label, build ID, or result path")
		return false
	if not _expected_outcome in ["continue", "safe_end", "source_exit"]:
		_fail("Expected outcome must be continue, safe_end, or source_exit")
		return false
	if _role == "peer":
		var lobby_text := OS.get_environment("CODEX_STEAM_LOBBY_ID").strip_edges()
		_lobby_id = int(lobby_text)
		if _lobby_id <= 0:
			_fail("Missing or invalid CODEX_STEAM_LOBBY_ID")
			return false
	return true


func _on_state_changed(state: int) -> void:
	_last_state = state
	_event("STATE", "value=%d name=%s" % [state, _state_name(state)])
	if _role == "peer" and state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)
		_event("READY", "value=true")
	if state == MIGRATING_HOST_STATE:
		_migration_seen = true
		_event("MIGRATION_ENTERED", "epoch=%d" % int(_network.call("DebugGetAuthorityEpoch")))
	elif state == IN_GAME_STATE and _migration_seen:
		_event("MIGRATION_RECOVERED", "epoch=%d" % int(_network.call("DebugGetAuthorityEpoch")))
	elif state == FAILED_STATE:
		_event("SAFE_END", "artifacts=%s" % str(bool(_network.call("DebugHasMigrationArtifacts"))).to_lower())
		if _expected_outcome in ["safe_end", "source_exit"]:
			_finish(_expected_outcome)
		else:
			_fail("Session ended but continuation was required")


func _on_player_list_updated(players: Array) -> void:
	_resolve_local_seat(players)
	if _role != "host" or _match_started or players.size() != PLAYER_COUNT:
		return
	for raw_player in players:
		var player: Dictionary = raw_player
		if not bool(player.get("is_ready", false)) or not bool(player.get("credentials_acknowledged", false)):
			return
	_match_started = true
	_event("START_MATCH", "players=%d" % players.size())
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return
	_board_initialized = true
	_resolve_local_seat(_network.GetPlayersVariant())
	_event("BOARD_INITIALIZED", "seat=%d" % _local_seat)


func _on_error(message: String) -> void:
	_event("ERROR", "message=%s" % message.replace("\n", " ").substr(0, 160))


func _resolve_local_seat(players: Array) -> void:
	for raw_player in players:
		var player: Dictionary = raw_player
		if bool(player.get("is_local", false)):
			var next_seat := int(player.get("seat_index", -1))
			if next_seat != _local_seat:
				_local_seat = next_seat
				_event("LOCAL_SEAT", "value=%d" % _local_seat)
			return


func _track_lobby_id() -> void:
	if _lobby_id > 0 or not _network.has_method("DebugGetCurrentSteamLobbyId"):
		return
	_lobby_id = int(_network.call("DebugGetCurrentSteamLobbyId"))
	if _lobby_id > 0:
		_event("LOBBY", "id=%d" % _lobby_id)


func _track_owner() -> void:
	if _steam == null or _lobby_id <= 0 or not _steam.has_method("getLobbyOwner"):
		return
	var owner_id := int(_steam.call("getLobbyOwner", _lobby_id))
	if owner_id > 0 and owner_id != _last_owner_id:
		_last_owner_id = owner_id
		_event("LOBBY_OWNER", "steam_id=%d" % owner_id)


func _track_local_authority() -> void:
	if _network == null or not _network.has_method("DebugGetIsHost"):
		return
	var is_host := bool(_network.call("DebugGetIsHost"))
	if is_host and _role == "peer" and not _became_host:
		_became_host = true
		_event("BECAME_HOST", "seat=%d epoch=%d" % [_local_seat, int(_network.call("DebugGetAuthorityEpoch"))])


func _drive_scenario() -> void:
	if _role != "host" or _last_state != IN_GAME_STATE or not _board_initialized:
		return
	if not bool(_network.call("DebugCanRequestActiveHostMigration")):
		return

	match _scenario:
		"active_owner_transfer", "owner_timeout":
			if _request_sent:
				return
			_request_sent = true
			_event("ACTIVE_REQUEST", "scenario=%s" % _scenario)
			if not bool(_network.call("RequestActiveHostMigration")):
				_fail("Active host migration request was rejected")
		"automatic_owner_second_transfer":
			_request_secondary_owner_transfer()
		"source_crash", "candidate_exit":
			_log_source_kill_ready()


func _request_secondary_owner_transfer() -> void:
	if _secondary_owner_requested:
		if _last_owner_id == int(OS.get_environment("CODEX_STEAM_SECONDARY_OWNER_ID")):
			_log_source_kill_ready()
		return
	var target_owner := int(OS.get_environment("CODEX_STEAM_SECONDARY_OWNER_ID"))
	if target_owner <= 0 or target_owner == _local_steam_id:
		_fail("Missing non-source CODEX_STEAM_SECONDARY_OWNER_ID")
		return
	_secondary_owner_requested = true
	var transferred := bool(_steam.call("setLobbyOwner", _lobby_id, target_owner))
	_event("SECONDARY_OWNER_REQUEST", "target=%d accepted=%s" % [target_owner, str(transferred).to_lower()])
	if not transferred:
		_fail("Steam rejected the secondary owner transfer")


func _log_source_kill_ready() -> void:
	if _source_kill_ready_logged:
		return
	_source_kill_ready_logged = true
	_event("SOURCE_KILL_READY", "lobby=%d" % _lobby_id)


func _drive_action() -> void:
	if not _board_initialized or _last_state != IN_GAME_STATE:
		return
	var now := Time.get_ticks_msec() / 1000.0
	if now < _next_action_at:
		return
	_next_action_at = now + ACTION_INTERVAL_SECONDS
	var board: Node = current_scene
	if board == null or not board.has_method("DebugSubmitFirstLegalNetworkAction"):
		return
	if bool(board.call("DebugSubmitFirstLegalNetworkAction")):
		_event("ACTION", "seat=%d snapshot=%d" % [_local_seat, int(board.call("DebugGetNetworkSnapshotId"))])


func _track_winner() -> void:
	if not _board_initialized or _last_state != IN_GAME_STATE:
		return
	var board: Node = current_scene
	if board == null or not board.has_method("DebugGetWinnerPlayerIndex"):
		return
	var winner := int(board.call("DebugGetWinnerPlayerIndex"))
	if winner < 0 or _winner >= 0:
		return
	_winner = winner
	_event("WINNER", "seat=%d snapshot=%d" % [winner, int(board.call("DebugGetNetworkSnapshotId"))])
	if _expected_outcome == "continue":
		_finish_after_settle()
	else:
		_fail("Match continued when a safe end was required")


func _finish_after_settle() -> void:
	await create_timer(FINISH_SETTLE_SECONDS).timeout
	if not _finished:
		_finish("continue")


func _finish(outcome: String) -> void:
	if _finished:
		return
	_finished = true
	_write_result(outcome, "")
	_event("DONE", "outcome=%s winner=%d" % [outcome, _winner])
	if _network != null:
		_network.LeaveSession()
	quit(0)


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	_write_result("harness_failure", message)
	_event("HARNESS_FAIL", "message=%s" % message.replace("\n", " ").substr(0, 160))
	if _network != null:
		_network.LeaveSession()
	quit(1)


func _write_result(outcome: String, failure: String) -> void:
	if _result_path.is_empty():
		return
	var file := FileAccess.open(_result_path, FileAccess.WRITE)
	if file == null:
		push_error("Could not write Steam migration result")
		return
	var result := {
		"schema_version": 1,
		"build_id": _build_id,
		"scenario": _scenario,
		"peer_label": _peer_label,
		"role": _role,
		"steam_id": str(_local_steam_id),
		"lobby_id": str(_lobby_id),
		"seat": _local_seat,
		"expected_outcome": _expected_outcome,
		"outcome": outcome,
		"failure": failure,
		"final_state": _last_state,
		"authority_epoch": int(_network.call("DebugGetAuthorityEpoch")) if _network != null else -1,
		"lobby_owner_steam_id": str(_last_owner_id),
		"migration_seen": _migration_seen,
		"became_host": _became_host,
		"winner": _winner,
		"elapsed_seconds": Time.get_ticks_msec() / 1000.0 - _started_at,
		"completed_at_utc": Time.get_datetime_string_from_system(true, true),
	}
	file.store_string(JSON.stringify(result, "\t") + "\n")
	file.close()


func _event(kind: String, details: String) -> void:
	var elapsed := Time.get_ticks_msec() / 1000.0 - _started_at
	print("STEAM_MIGRATION_EVENT t=%.3f utc=%s peer=%s role=%s scenario=%s kind=%s %s" % [
		elapsed,
		Time.get_datetime_string_from_system(true, true),
		_peer_label,
		_role,
		_scenario,
		kind,
		details])


func _state_name(state: int) -> String:
	match state:
		0: return "Idle"
		1: return "Searching"
		2: return "Hosting"
		3: return "Joining"
		4: return "Lobby"
		5: return "Starting"
		6: return "LoadingScene"
		7: return "InGame"
		8: return "Reconnecting"
		9: return "Failed"
		10: return "MigratingHost"
		_: return "Unknown"
