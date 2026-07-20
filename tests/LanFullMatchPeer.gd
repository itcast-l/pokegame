extends SceneTree

const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const FAILED_STATE := 9
const MIGRATING_HOST_STATE := 10
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
var _allow_client_result_without_host := false
var _require_all_migration_peers := false
var _migration_fixture := ""
var _kill_when := ""
var _active_lobby_migration := false
var _active_lobby_keep_open := false
var _expect_migration_failure := false
var _paused_maintenance := false
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
var _last_state := -1
var _migration_started_seen := false
var _migration_ui_logged := false
var _migration_failure_seen := false
var _migration_fixture_started := false
var _migration_pending_logged := false
var _migration_visual_started := false
var _migration_kill_ready := false
var _active_lobby_recovery_logged := false
var _active_lobby_request_sent := false
var _migration_failure_ui_logged := false
var _paused_maintenance_apply_pending := false
var _paused_maintenance_release_pending := false
var _paused_maintenance_checkpoint_requested := false
var _paused_maintenance_plan_ready_logged := false


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
		_track_migration_state()
		_apply_paused_maintenance_after_opening_deal()
		_track_paused_maintenance_plan()
		_release_paused_maintenance_after_recovery()
		_try_start_active_lobby_migration()
		_track_active_lobby_recovery()
		if _board_initialized:
			_start_migration_fixture_if_needed()
			_drive_local_action()
			_track_finished()
		if _expect_migration_failure and _migration_failure_seen:
			await process_frame
			await process_frame
			_log_migration_failure_ui()
			_finished = true
			quit(0)

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
		elif argument == "--allow-client-result-without-host":
			_allow_client_result_without_host = true
		elif argument == "--require-all-migration-peers":
			_require_all_migration_peers = true
		elif argument.begins_with("--migration-fixture="):
			_migration_fixture = argument.trim_prefix("--migration-fixture=")
		elif argument.begins_with("--kill-when="):
			_kill_when = argument.trim_prefix("--kill-when=")
		elif argument == "--active-lobby-migration":
			_active_lobby_migration = true
		elif argument == "--active-lobby-keep-open":
			_active_lobby_keep_open = true
		elif argument == "--expect-migration-failure":
			_expect_migration_failure = true
		elif argument == "--paused-maintenance":
			_paused_maintenance = true


func _on_state_changed(state: int) -> void:
	_last_state = state
	print("FULL_MATCH_%s_STATE %d" % [_role.to_upper(), state])
	if state == MIGRATING_HOST_STATE:
		if _paused_maintenance and _role == "client":
			if not paused:
				_fail("Fixture-owned pause was cleared before host migration")
				return
			print("PAUSED_MAINTENANCE_MIGRATING")
		_migration_started_seen = true
		_migration_ui_logged = false
		print("MIGRATION_STATE_ENTERED role=%s" % _role)
	elif state == FAILED_STATE:
		_migration_failure_seen = true
		print("MIGRATION_FAILURE_STATE role=%s" % _role)
		_log_migration_failure_ui()
	elif state == IN_GAME_STATE and _paused_maintenance and _role == "client" and _migration_started_seen:
		if not paused:
			_fail("Fixture-owned pause was cleared during host migration")
			return
		_paused_maintenance_release_pending = true
		print("PAUSED_MAINTENANCE_INGAME")
	if _role != "host" and state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)


func _log_migration_failure_ui() -> void:
	if _migration_failure_ui_logged:
		return

	var failure_scene: Node = current_scene
	if failure_scene == null:
		return

	if failure_scene.has_method("DebugIsNetworkReconnectNoticeVisible"):
		_migration_failure_ui_logged = true
		print("MIGRATION_FAILURE_UI notice_visible=%s notice_text=%s artifacts=%s" % [
			str(bool(failure_scene.call("DebugIsNetworkReconnectNoticeVisible"))).to_lower(),
			String(failure_scene.call("DebugGetNetworkReconnectNoticeText")),
			str(bool(_network.call("DebugHasMigrationArtifacts"))).to_lower()])
	elif failure_scene.has_method("DebugIsMigrationFailureToastVisible"):
		_migration_failure_ui_logged = true
		print("MIGRATION_FAILURE_UI toast_visible=%s toast_text=%s artifacts=%s" % [
			str(bool(failure_scene.call("DebugIsMigrationFailureToastVisible"))).to_lower(),
			String(failure_scene.call("DebugGetMigrationFailureToastText")),
			str(bool(_network.call("DebugHasMigrationArtifacts"))).to_lower()])


func _track_migration_state() -> void:
	if not _migration_started_seen or _last_state != MIGRATING_HOST_STATE or _migration_ui_logged:
		return

	var board: Node = current_scene
	if board == null or not board.has_method("DebugIsNetworkReconnectOverlayVisible"):
		return

	_migration_ui_logged = true
	print("MIGRATION_UI_STATE visible=%s text=%s" % [
		str(bool(board.call("DebugIsNetworkReconnectOverlayVisible"))).to_lower(),
		String(board.call("DebugGetNetworkReconnectOverlayText"))])


func _release_paused_maintenance_after_recovery() -> void:
	if not _paused_maintenance_release_pending or _last_state != IN_GAME_STATE:
		return
	if bool(_network.call("DebugHasMigrationPlan")):
		return
	if not paused:
		_fail("Fixture-owned pause was cleared before migration readiness completed")
		return

	_paused_maintenance_release_pending = false
	paused = false
	print("PAUSED_MAINTENANCE_RECOVERED")


func _apply_paused_maintenance_after_opening_deal() -> void:
	if not _paused_maintenance_apply_pending:
		return
	var board: Node = current_scene
	if board == null or String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return
	var client_ready_path := "%s/paused_client.ready" % _result_dir
	if _role == "host" and not FileAccess.file_exists(client_ready_path):
		return

	_paused_maintenance_apply_pending = false
	paused = true
	print("PAUSED_MAINTENANCE_APPLIED role=%s" % _role)
	if _role == "client":
		var ready_file := FileAccess.open(client_ready_path, FileAccess.WRITE)
		if ready_file == null:
			_fail("Could not create paused maintenance client marker")
			return
		ready_file.store_string("ready\n")
		ready_file.close()
	else:
		var snapshot_id := int(board.call("DebugGetNetworkSnapshotId")) + 1
		_network.call("SendBoardSnapshotsFromHost", board, snapshot_id)
		_paused_maintenance_checkpoint_requested = true
		print("PAUSED_MAINTENANCE_CHECKPOINT_REQUESTED snapshot=%d" % snapshot_id)


func _track_paused_maintenance_plan() -> void:
	if _role != "host" or not _paused_maintenance_checkpoint_requested or _paused_maintenance_plan_ready_logged:
		return
	if not bool(_network.call("DebugHasMigrationPlan")):
		return

	_paused_maintenance_plan_ready_logged = true
	print("PAUSED_MAINTENANCE_PLAN_READY")


func _try_start_active_lobby_migration() -> void:
	if not _active_lobby_migration or _role != "host" or _migration_started_seen:
		return
	if _last_state != LOBBY_STATE or _active_lobby_recovery_logged or _active_lobby_request_sent:
		return
	if int(_network.call("DebugGetAuthorityEpoch")) > 0:
		return
	if not bool(_network.call("DebugHasMigrationPlan")):
		return
	var players: Array = _network.GetPlayersVariant()
	if players.size() < _player_count:
		return
	for raw_player in players:
		var player: Dictionary = raw_player
		if not bool(player.get("is_connected", false)) or not bool(player.get("is_ready", false)):
			return

	if bool(_network.call("RequestActiveHostMigration")):
		_active_lobby_request_sent = true
		print("MIGRATION_ACTIVE_LOBBY_REQUESTED")
	else:
		_fail("Active lobby migration request was rejected")


func _track_active_lobby_recovery() -> void:
	if not _active_lobby_migration or not _migration_started_seen or _last_state != LOBBY_STATE:
		return
	if _active_lobby_recovery_logged:
		return

	if _role == "client":
		if _expect_migration_failure:
			return
		_active_lobby_recovery_logged = true
		print("MIGRATION_LOBBY_RECOVERED role=client")
		if _active_lobby_keep_open:
			return
		_finished = true
		_network.LeaveSession()
		quit(0)
		return

	var players: Array = _network.GetPlayersVariant()
	if players.size() < _player_count:
		return
	for raw_player in players:
		var player: Dictionary = raw_player
		if not bool(player.get("is_connected", false)):
			return

	_active_lobby_recovery_logged = true
	print("MIGRATION_LOBBY_RECOVERED role=host seats=%d" % players.size())
	_finished = true
	_network.LeaveSession()
	quit(0)


func _start_migration_fixture_if_needed() -> void:
	if _role != "host" or _migration_fixture.is_empty() or _migration_fixture_started:
		return
	if int(_network.call("DebugGetAuthorityEpoch")) != 0:
		return
	var board: Node = current_scene
	if board == null or String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return

	var started := false
	match _migration_fixture:
		"discard":
			started = bool(board.call("DebugStartPendingHandLimitDiscardForSeat", 0))
		"attachment", "visual":
			var result := {}
			started = bool(board.call("DebugStartMagnetGenerationOwnershipProbe", result))
		_:
			_fail("Unknown migration fixture: %s" % _migration_fixture)
			return

	if started:
		_migration_fixture_started = true
		print("MIGRATION_FIXTURE_STARTED kind=%s" % _migration_fixture)


func _track_migration_fixture() -> void:
	if not _migration_fixture_started:
		return
	var board: Node = current_scene
	if board == null:
		return
	var mode := String(board.call("DebugGetPendingItemSelectionModeName"))
	if mode != "None" and not _migration_pending_logged:
		_migration_pending_logged = true
		print("MIGRATION_PENDING_READY mode=%s" % mode)

	if _migration_fixture == "visual" and not _migration_visual_started \
		and mode == "AttachmentRewardHandCard":
		if bool(board.call("DebugBeginMagnetGenerationOwnershipAnimation")):
			_migration_visual_started = true
			print("MIGRATION_VISUAL_ACTIVE")


func _on_player_list_updated(players: Array) -> void:
	if _role != "host" or _started_match or _active_lobby_migration or players.size() != _player_count:
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
	if _paused_maintenance:
		_paused_maintenance_apply_pending = true


func _on_error(message: String) -> void:
	print("FULL_MATCH_%s_ERROR %s" % [_role.to_upper(), message])


func _drive_local_action() -> void:
	if (_paused_maintenance and paused) or _active_lobby_migration or (_migration_fixture_started and _role == "host" and int(_network.call("DebugGetAuthorityEpoch")) == 0) or _migration_kill_ready:
		return
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
	_track_migration_fixture()

	var winner := int(board.call("DebugGetWinnerPlayerIndex"))
	var action_state: Dictionary = {}
	if board.has_method("DebugGetNetworkActionState"):
		action_state = board.call("DebugGetNetworkActionState")
	var now := Time.get_ticks_msec() / 1000.0
	if _kill_when == "current-turn" and _role == "host" and int(_network.call("DebugGetAuthorityEpoch")) == 0 and not _migration_kill_ready:
		if int(action_state.get("current_turn", -1)) == 0 \
			and not bool(action_state.get("processing_card_play", false)) \
			and not bool(action_state.get("resolving_turn_sequence", false)) \
			and not bool(action_state.get("visual_processing", false)) \
			and int(action_state.get("visual_pending_events", 0)) == 0 \
			and String(action_state.get("pending_selection_mode", "None")) == "None":
			_migration_kill_ready = true
			print("MIGRATION_KILL_READY current_turn=0")
			return
	if now >= _next_status_at:
		_next_status_at = now + 1.0
		print("FULL_MATCH_%s_STATUS phase=%s turn=%d winner=%d snapshot=%d hand=%d items=%d action=%s seats=%s" % [_role.to_upper(), String(board.call("DebugGetGameLoopPhaseName")), int(board.call("DebugGetCurrentTurnPlayerIndex")), winner, int(board.call("DebugGetNetworkSnapshotId")), int(board.call("DebugGetTotalHandCardCountForPlayer", _seat)), int(board.call("DebugGetTotalItemHandCardCountForPlayer", _seat)), str(action_state), str(_network.GetSeatControlStatesVariant(true))])
	if winner < 0:
		return

	if _winner_seen < 0:
		_winner_seen = winner
		print("FULL_MATCH_%s_WINNER %d" % [_role.to_upper(), winner])

	var current_host := _is_current_host()
	if not current_host:
		if not _allow_client_result_without_host:
			var expected_snapshot_id := _read_expected_host_snapshot_id(_seat)
			if expected_snapshot_id < 0 or int(board.call("DebugGetNetworkSnapshotId")) != expected_snapshot_id:
				return
		if _require_all_migration_peers and not _all_non_source_migration_peers_connected():
			return

		if bool(action_state.get("visual_processing", false)) or int(action_state.get("visual_pending_events", 0)) > 0:
			return

	if _finish_started_at < 0.0:
		_finish_started_at = Time.get_ticks_msec() / 1000.0
		return

	if Time.get_ticks_msec() / 1000.0 - _finish_started_at < SETTLE_SECONDS:
		return

	if current_host:
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


func _all_non_source_migration_peers_connected() -> bool:
	var states: Array = _network.GetSeatControlStatesVariant(true)
	if states.size() < _player_count:
		return false

	for state_variant in states:
		var state: Dictionary = state_variant
		if int(state.get("seat_index", -1)) == 0:
			continue
		if String(state.get("mode", "")) != "HumanConnected":
			return false
	return true


func _is_current_host() -> bool:
	return bool(_network.call("DebugGetIsHost"))


func _read_expected_host_snapshot_id(seat: int) -> int:
	var path := "%s/expected_%d.result" % [_result_dir, seat]
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
	var current_host := _is_current_host()
	if current_host:
		fingerprint = String(board.call("DebugGetNetworkBoardViewFingerprintForSeat", result_seat))
		private_leak = bool(board.call("DebugNetworkBoardViewHasPrivateLeak", result_seat))
		snapshot_id = int(board.call("DebugGetNetworkSnapshotId"))
	else:
		fingerprint = String(board.call("DebugGetAppliedNetworkBoardViewFingerprint"))
		private_leak = bool(board.call("DebugNetworkBoardViewHasPrivateLeak", _seat))
		snapshot_id = int(board.call("DebugGetNetworkSnapshotId"))

	var lines := PackedStringArray()
	lines.append("role=%s" % ("host" if current_host else "client"))
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

	var output_paths: Array[String] = []
	output_paths.append("%s/%s_%d.result" % [_result_dir, "expected" if current_host else "client", result_seat])
	if current_host and result_seat == _seat:
		# A migrated client can become the authority. Keep a client result for the
		# outer smoke contract in addition to the host's expected result set.
		output_paths.append("%s/client_%d.result" % [_result_dir, result_seat])

	for path in output_paths:
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
	paused = false
	if _network != null:
		_network.LeaveSession()
	print("FULL_MATCH_%s_FAIL %s" % [_role.to_upper(), message])
	quit(1)
