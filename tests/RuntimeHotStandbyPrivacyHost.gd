extends SceneTree

const PLAYER_COUNT := 3
const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const IDLE_STATE := 0
const TIMEOUT_SECONDS := 60.0

var _network: Node
var _result_dir := ""
var _started_match := false
var _board_initialized := false
var _first_candidate := -1
var _second_candidate := -1
var _phase_one_published := false
var _rotation_requested := false
var _phase_two_published := false
var _close_published := false
var _second_close_published := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_result_dir = _read_arg("result-dir")
	if _result_dir.is_empty():
		_fail("Missing --result-dir")
		return
	DirAccess.make_dir_recursive_absolute(_result_dir)
	_network = root.get_node("NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	_network.call("HostLan", PLAYER_COUNT, false, 50, "StandbyPrivacySmoke", "")
	if int(_network.get("State")) != LOBBY_STATE or not bool(_network.get("IsHost")):
		_fail("Host did not finish listening in the lobby state")
		return
	await process_frame
	if int(_network.get("State")) != LOBBY_STATE or not bool(_network.get("IsHost")):
		_fail("Host left the lobby while completing network initialization")
		return
	if not _write_text("host.ready", "ready\n"):
		return
	print("PRIVACY_HOST_READY")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not _board_initialized or int(_network.get("State")) != IN_GAME_STATE:
			continue
		if not _phase_one_published and bool(_network.call("DebugCanRequestActiveHostMigration")):
			_first_candidate = int(_network.call("DebugGetHotStandbySeat"))
			if _first_candidate <= 0:
				continue
			_write_text("phase1.marker", str(_first_candidate))
			_phase_one_published = true
			print("PRIVACY_PHASE1 candidate=%d" % _first_candidate)
		elif _phase_one_published and not _rotation_requested and _client_results_exist("phase1"):
			if not bool(_network.call("DebugRotateHotStandbyCandidate")):
				_fail("Could not rotate the hot-standby candidate")
				return
			_rotation_requested = true
			print("PRIVACY_ROTATION_REQUESTED former=%d" % _first_candidate)
		elif _rotation_requested and not _phase_two_published and bool(_network.call("DebugCanRequestActiveHostMigration")):
			_second_candidate = int(_network.call("DebugGetHotStandbySeat"))
			if _second_candidate <= 0 or _second_candidate == _first_candidate:
				continue
			_write_text("phase2.marker", str(_second_candidate))
			_phase_two_published = true
			print("PRIVACY_PHASE2 candidate=%d" % _second_candidate)
		elif _phase_two_published and not _close_published and _client_results_exist("phase2"):
			_write_text("close_1.marker", "close")
			_close_published = true
			print("PRIVACY_CLOSE_REQUESTED seat=1")
		elif _close_published and not _second_close_published \
				and FileAccess.file_exists("%s/phase3_1.result" % _result_dir):
			_write_text("close_2.marker", "close")
			_second_close_published = true
			print("PRIVACY_CLOSE_REQUESTED seat=2")
		elif _second_close_published and _client_results_exist("phase3"):
			_network.call("LeaveSession")
			if not await _wait_until_idle():
				return
			var artifacts := bool(_network.call("DebugHasMigrationArtifacts"))
			_write_text("host.result", "first_candidate=%d\nsecond_candidate=%d\nartifacts=%s\n" % [
				_first_candidate,
				_second_candidate,
				"true" if artifacts else "false",
			])
			if artifacts:
				_fail("Host retained migration artifacts after close")
				return
			print("RUNTIME_HOT_STANDBY_PRIVACY_HOST_OK")
			quit(0)
			return
	_fail("Hot-standby privacy host timed out")


func _on_player_list_updated(players: Array) -> void:
	var ready_count := 0
	for player in players:
		if bool(player.get("is_ready", false)):
			ready_count += 1
	print("PRIVACY_HOST_ROSTER players=%d ready=%d state=%d" % [
		players.size(),
		ready_count,
		int(_network.get("State")),
	])
	if _started_match or players.size() != PLAYER_COUNT:
		return
	for player in players:
		if not bool(player.get("is_ready", false)):
			return
	_started_match = true
	_network.call("StartMatch")


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _client_results_exist(phase_name: String) -> bool:
	return FileAccess.file_exists("%s/%s_1.result" % [_result_dir, phase_name]) and FileAccess.file_exists("%s/%s_2.result" % [_result_dir, phase_name])


func _wait_until_idle() -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + 0.5
	while Time.get_ticks_msec() / 1000.0 < deadline:
		if int(_network.get("State")) == IDLE_STATE:
			return true
		await process_frame
	_fail("Host close did not settle in Idle")
	return false


func _write_text(file_name: String, contents: String) -> bool:
	var file := FileAccess.open("%s/%s" % [_result_dir, file_name], FileAccess.WRITE)
	if file == null:
		_fail("Could not write %s" % file_name)
		return false
	file.store_string(contents)
	file.close()
	return true


func _read_arg(key: String) -> String:
	var prefix := "--%s=" % key
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with(prefix):
			return argument.trim_prefix(prefix)
	return ""


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.call("LeaveSession")
	print("RUNTIME_HOT_STANDBY_PRIVACY_HOST_FAIL %s" % message)
	quit(1)
