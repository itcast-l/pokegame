extends SceneTree

const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const TIMEOUT_SECONDS := 180.0
const ACTION_INTERVAL_SECONDS := 0.1
const SETTLE_SECONDS := 1.0
const DEFAULT_HOST_ADDRESS := "127.0.0.1"

var _network: Node
var _role := "client"
var _seat := -1
var _player_count := 2
var _item_mode := false
var _result_dir := ""
var _host_address := DEFAULT_HOST_ADDRESS
var _ready_sent := false
var _started_match := false
var _board_initialized := false
var _finished := false
var _finish_started_at := -1.0
var _winner_seen := -1
var _host_results_written := false
var _host_results_written_at := -1.0
var _next_action_at := 0.0
var _next_status_at := 0.0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	if _seat < 0 or _seat >= _player_count or _result_dir.is_empty():
		_fail("invalid peer arguments")
		return

	Engine.time_scale = 8.0
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	_network.connect("ErrorRaised", Callable(self, "_on_error"))
	print("FULL_MATCH_%s_START seat=%d players=%d items=%s" % [_role.to_upper(), _seat, _player_count, _item_mode])
	await process_frame

	if _role == "host":
		_network.HostLan(_player_count, _item_mode, 50, "CodexFullMatch", "")
	else:
		_network.JoinLan(_host_address, "")

	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if _board_initialized:
			_drive_local_action()
			_track_finished()

	if not _finished:
		_fail("full match timed out")


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--role="):
			_role = argument.trim_prefix("--role=")
		elif argument.begins_with("--seat="):
			_seat = int(argument.trim_prefix("--seat="))
		elif argument.begins_with("--players="):
			_player_count = int(argument.trim_prefix("--players="))
		elif argument.begins_with("--item-mode="):
			_item_mode = argument.trim_prefix("--item-mode=") == "1"
		elif argument.begins_with("--result-dir="):
			_result_dir = argument.trim_prefix("--result-dir=")
		elif argument.begins_with("--host="):
			_host_address = argument.trim_prefix("--host=")


func _on_state_changed(state: int) -> void:
	print("FULL_MATCH_%s_STATE %d" % [_role.to_upper(), state])
	if _role != "host" and state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)


func _on_player_list_updated(players: Array) -> void:
	if _role != "host" or _started_match or players.size() != _player_count:
		return

	for player in players:
		if not bool(player.get("is_ready", false)):
			return

	_started_match = true
	print("FULL_MATCH_HOST_START_MATCH")
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("FULL_MATCH_%s_BOARD_INIT" % _role.to_upper())


func _on_error(message: String) -> void:
	print("FULL_MATCH_%s_ERROR %s" % [_role.to_upper(), message])


func _drive_local_action() -> void:
	var now := Time.get_ticks_msec() / 1000.0
	if now < _next_action_at:
		return

	_next_action_at = now + ACTION_INTERVAL_SECONDS
	var board: Node = current_scene
	if board == null or not board.has_method("DebugGetFirstLegalNetworkAction"):
		return
	_sync_local_seat(board)

	var action: Dictionary = board.call("DebugGetFirstLegalNetworkAction")
	if String(action.get("kind", "None")) == "None":
		return

	if bool(board.call("DebugSubmitFirstLegalNetworkAction")):
		print("FULL_MATCH_%s_ACTION kind=%s turn=%d generation=%d snapshot=%d" % [_role.to_upper(), String(action.get("kind", "")), int(board.call("DebugGetCurrentTurnPlayerIndex")), int(action.get("turn_sequence_generation", -1)), int(board.call("DebugGetNetworkSnapshotId"))])


func _track_finished() -> void:
	var board: Node = current_scene
	if board == null or not board.has_method("DebugGetWinnerPlayerIndex"):
		return
	_sync_local_seat(board)

	var winner := int(board.call("DebugGetWinnerPlayerIndex"))
	var action_state: Dictionary = {}
	if board.has_method("DebugGetNetworkActionState"):
		action_state = board.call("DebugGetNetworkActionState")
	var now := Time.get_ticks_msec() / 1000.0
	if now >= _next_status_at:
		_next_status_at = now + 1.0
		print("FULL_MATCH_%s_STATUS phase=%s turn=%d winner=%d snapshot=%d hand=%d items=%d action=%s seats=%s" % [_role.to_upper(), String(board.call("DebugGetGameLoopPhaseName")), int(board.call("DebugGetCurrentTurnPlayerIndex")), winner, int(board.call("DebugGetNetworkSnapshotId")), int(board.call("DebugGetTotalHandCardCountForPlayer", _seat)), int(board.call("DebugGetTotalItemHandCardCountForPlayer", _seat)), str(action_state), str(_network.GetSeatControlStatesVariant(true))])
	if winner < 0:
		return

	if _winner_seen < 0:
		_winner_seen = winner
		print("FULL_MATCH_%s_WINNER %d" % [_role.to_upper(), winner])

	if _role != "host":
		var expected_snapshot_id := _read_expected_host_snapshot_id()
		if expected_snapshot_id < 0 or int(board.call("DebugGetNetworkSnapshotId")) < expected_snapshot_id:
			return

		if bool(action_state.get("visual_processing", false)) or int(action_state.get("visual_pending_events", 0)) > 0:
			return

	if _finish_started_at < 0.0:
		_finish_started_at = Time.get_ticks_msec() / 1000.0
		return

	if Time.get_ticks_msec() / 1000.0 - _finish_started_at < SETTLE_SECONDS:
		return

	if _role == "host":
		if not _host_results_written:
			for expected_seat in range(_player_count):
				_write_result(board, winner, expected_seat)
			_host_results_written = true
			_host_results_written_at = Time.get_ticks_msec() / 1000.0
			print("FULL_MATCH_HOST_RESULTS_WRITTEN snapshot=%d" % int(board.call("DebugGetNetworkSnapshotId")))
			return

		for client_seat in range(1, _player_count):
			if not FileAccess.file_exists("%s/client_%d.result" % [_result_dir, client_seat]):
				return

		if Time.get_ticks_msec() / 1000.0 - _host_results_written_at < SETTLE_SECONDS:
			return
	else:
		_write_result(board, winner, _seat)
	_finished = true
	_network.LeaveSession()
	print("FULL_MATCH_%s_DONE" % _role.to_upper())
	quit(0)


func _read_expected_host_snapshot_id() -> int:
	var path := "%s/expected_0.result" % _result_dir
	if not FileAccess.file_exists(path):
		return -1

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return -1

	while not file.eof_reached():
		var line := file.get_line()
		if line.begins_with("snapshot_id="):
			return int(line.trim_prefix("snapshot_id="))

	return -1


func _write_result(board: Node, winner: int, result_seat: int) -> void:
	var fingerprint := ""
	var snapshot_id := -1
	var private_leak := true
	if _role == "host":
		fingerprint = String(board.call("DebugGetNetworkBoardViewFingerprintForSeat", result_seat))
		private_leak = bool(board.call("DebugNetworkBoardViewHasPrivateLeak", result_seat))
		snapshot_id = int(board.call("DebugGetNetworkSnapshotId"))
	else:
		fingerprint = String(board.call("DebugGetAppliedNetworkBoardViewFingerprint"))
		private_leak = bool(board.call("DebugNetworkBoardViewHasPrivateLeak", _seat))
		snapshot_id = int(board.call("DebugGetNetworkSnapshotId"))

	var lines := PackedStringArray()
	lines.append("role=%s" % _role)
	lines.append("seat=%d" % result_seat)
	lines.append("winner=%d" % winner)
	lines.append("fingerprint=%s" % fingerprint)
	lines.append("snapshot_id=%d" % snapshot_id)
	lines.append("private_leak=%s" % ("true" if private_leak else "false"))
	lines.append("table_count=%d" % int(board.call("DebugGetTableCardCount")))
	for player_index in range(_player_count):
		lines.append("score_%d=%d" % [player_index, int(board.call("DebugGetPlayerScoreValue", player_index))])
		lines.append("deck_%d=%d" % [player_index, int(board.call("DebugGetDeckCardCountForPlayer", player_index))])
		lines.append("hand_%d=%d" % [player_index, int(board.call("DebugGetTotalHandCardCountForPlayer", player_index))])
		lines.append("item_hand_%d=%d" % [player_index, int(board.call("DebugGetTotalItemHandCardCountForPlayer", player_index))])

	var path := "%s/%s_%d.result" % [_result_dir, "expected" if _role == "host" else "client", result_seat]
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		_fail("could not write result: %s" % path)
		return

	file.store_string("\n".join(lines) + "\n")
	file.close()


func _sync_local_seat(board: Node) -> void:
	if board.has_method("DebugGetLocalPlayerIndex"):
		_seat = int(board.call("DebugGetLocalPlayerIndex"))


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("FULL_MATCH_%s_FAIL %s" % [_role.to_upper(), message])
	quit(1)
