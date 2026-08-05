extends SceneTree

const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const IDLE_STATE := 0
const TIMEOUT_SECONDS := 60.0

var _network: Node
var _result_dir := ""
var _seat := -1
var _ready_sent := false
var _phase_one_recorded := false
var _phase_two_recorded := false
var _phase_one_key_fingerprint := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_result_dir = _read_arg("result-dir")
	_seat = int(_read_arg("seat"))
	if _result_dir.is_empty() or _seat not in [1, 2]:
		_fail("Missing or invalid client arguments")
		return
	_network = root.get_node("NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	await process_frame
	_network.call("JoinLan", "127.0.0.1", "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if _phase_two_recorded and FileAccess.file_exists("%s/close_%d.marker" % [_result_dir, _seat]):
			_network.call("LeaveSession")
			if not await _wait_until_idle():
				return
			_write_snapshot("phase3")
			print("RUNTIME_HOT_STANDBY_PRIVACY_CLIENT_OK seat=%d" % _seat)
			quit(0)
			return
		if int(_network.get("State")) == IN_GAME_STATE:
			var local_seat := int(_network.get("LocalSeatIndex"))
			if local_seat not in [1, 2]:
				continue
			_seat = local_seat
			if not _phase_one_recorded and FileAccess.file_exists("%s/phase1.marker" % _result_dir):
				_phase_one_key_fingerprint = int(_network.call("DebugGetLocalMigrationFingerprint"))
				_write_snapshot("phase1")
				_phase_one_recorded = true
			elif _phase_one_recorded and not _phase_two_recorded and FileAccess.file_exists("%s/phase2.marker" % _result_dir):
				_write_snapshot("phase2")
				_phase_two_recorded = true
	_fail("Hot-standby privacy client timed out")


func _on_state_changed(state: int) -> void:
	print("PRIVACY_CLIENT_STATE requested_seat=%d state=%d" % [_seat, state])
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.call("SetLocalReady", true)


func _write_snapshot(phase_name: String) -> void:
	var cache_count := int(_network.call("DebugGetLocalStandbyCheckpointCacheCount"))
	var checkpoint_id := int(_network.call("DebugGetLocalStandbyCheckpointId"))
	var checkpoint_fingerprint := int(_network.call("DebugGetLocalStandbyCheckpointFingerprint"))
	var key_fingerprint := int(_network.call("DebugGetLocalMigrationFingerprint"))
	var file := FileAccess.open("%s/%s_%d.result" % [_result_dir, phase_name, _seat], FileAccess.WRITE)
	if file == null:
		_fail("Could not write %s snapshot" % phase_name)
		return
	file.store_string("seat=%d\ncache_count=%d\ncheckpoint_id=%d\ncheckpoint_fingerprint=%d\nkey_fingerprint=%d\nphase1_key_fingerprint=%d\n" % [
		_seat,
		cache_count,
		checkpoint_id,
		checkpoint_fingerprint,
		key_fingerprint,
		_phase_one_key_fingerprint,
	])
	file.close()
	print("PRIVACY_SNAPSHOT phase=%s seat=%d cache=%d id=%d" % [
		phase_name,
		_seat,
		cache_count,
		checkpoint_id,
	])


func _wait_until_idle() -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + 0.5
	while Time.get_ticks_msec() / 1000.0 < deadline:
		if int(_network.get("State")) == IDLE_STATE:
			return true
		await process_frame
	_fail("Client close did not settle in Idle")
	return false


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
	print("RUNTIME_HOT_STANDBY_PRIVACY_CLIENT_FAIL seat=%d %s" % [_seat, message])
	quit(1)
