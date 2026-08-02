extends SceneTree

const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const MIGRATING_HOST_STATE := 10
const TIMEOUT_SECONDS := 100.0

var _network: Node
var _role := "client"
var _fixture := "base_capture"
var _mode := "baseline"
var _result_path := ""
var _ready_sent := false
var _match_started := false
var _board_initialized := false
var _fixture_configured := false
var _action_submitted := false
var _action_seen := false
var _migration_seen := false
var _result_written := false
var _finished := false
var _host_visual_playback_before := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	if _result_path.is_empty() or _fixture not in ["base_capture", "question_capture", "item_shuffle"]:
		_fail("invalid transaction fixture arguments")
		return

	Engine.time_scale = 10.0
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	if _role == "host":
		_network.HostLan(2, true, 100, "CodexAuthorityTransaction", "")
	else:
		_network.JoinLan("127.0.0.1", "")

	var timeout_seconds := TIMEOUT_SECONDS
	var configured_timeout := OS.get_environment("CODEX_AUTHORITY_TRANSACTION_TIMEOUT_SECONDS")
	if configured_timeout.is_valid_float():
		timeout_seconds = maxf(1.0, configured_timeout.to_float())
	var deadline := Time.get_ticks_msec() / 1000.0 + timeout_seconds
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track()
	if not _finished:
		_print_timeout_diagnostics()
		_fail("transaction peer timed out")


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--role="):
			_role = argument.trim_prefix("--role=")
		elif argument.begins_with("--fixture="):
			_fixture = argument.trim_prefix("--fixture=")
		elif argument.begins_with("--mode="):
			_mode = argument.trim_prefix("--mode=")
		elif argument.begins_with("--result-path="):
			_result_path = argument.trim_prefix("--result-path=")


func _on_state_changed(state: int) -> void:
	print("AUTHORITY_TRANSACTION_%s_STATE %d" % [_role.to_upper(), state])
	if state == MIGRATING_HOST_STATE:
		_migration_seen = true
	if _role == "client" and state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)


func _on_player_list_updated(players: Array) -> void:
	if _role != "host" or _match_started or players.size() != 2:
		return
	for player in players:
		if not bool(player.get("is_ready", false)):
			return
	_match_started = true
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _track() -> void:
	if not _board_initialized:
		return
	var board := current_scene
	if board == null or not board.has_method("DebugGetNetworkActionState"):
		return

	if _role == "host" and not _fixture_configured:
		if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			return
		if not bool(board.call("DebugConfigureAuthorityTransactionFixture", _fixture)):
			_fail("could not configure authority transaction fixture")
			return
		_fixture_configured = true
		_host_visual_playback_before = int(board.call("DebugGetNetworkAuthorityHostVisualPlaybackCount"))
		print("AUTHORITY_TRANSACTION_FIXTURE_READY fixture=%s" % _fixture)

	var action_state: Dictionary = board.call("DebugGetNetworkActionState")
	if int(board.call("DebugGetNextNetworkAuthorityActionId")) > 0:
		_action_seen = true

	if _role == "client" and not _action_submitted and not _migration_seen:
		var expected_kind := "ItemCard" if _fixture == "item_shuffle" else "BaseCard"
		_action_submitted = bool(board.call("DebugSubmitAuthorityTransactionFixtureAction", _fixture))
		if _action_submitted:
			print("AUTHORITY_TRANSACTION_CLIENT_SUBMITTED fixture=%s kind=%s" % [_fixture, expected_kind])

	if _mode == "baseline":
		_track_baseline(board)
	else:
		_track_fault_recovery(board, action_state)


func _track_baseline(board: Node) -> void:
	if _role == "host" and _action_seen and not _result_written \
			and bool(board.call("DebugIsAuthorityTransactionFixtureCompleted", _fixture)):
		if int(board.call("DebugGetNetworkAuthorityHostVisualDecodeFailureCount")) != 0:
			_fail("Host visual event decode failed")
			return
		if int(board.call("DebugGetNetworkAuthorityHostVisualPlaybackCount")) <= _host_visual_playback_before \
				or bool(board.call("DebugIsNetworkVisualQueueProcessing")) \
				or int(board.call("DebugGetNetworkVisualEventQueueCount")) != 0:
			return
		_write_result(board)
		_result_written = true
		print("AUTHORITY_TRANSACTION_BASELINE_RESULT fixture=%s" % _fixture)
		return

	if _role == "client" and FileAccess.file_exists(_result_path):
		var done_path := "%s.client_done" % _result_path
		var done_file := FileAccess.open(done_path, FileAccess.WRITE)
		if done_file != null:
			done_file.store_string("done\n")
			done_file.close()
		_finish("AUTHORITY_TRANSACTION_BASELINE_CLIENT_DONE")
	elif _role == "host" and _result_written and FileAccess.file_exists("%s.client_done" % _result_path):
		_finish("AUTHORITY_TRANSACTION_BASELINE_HOST_DONE")


func _track_fault_recovery(board: Node, action_state: Dictionary) -> void:
	if _role != "client" or not _migration_seen or not bool(_network.call("DebugGetIsHost")):
		return
	if int(_network.call("DebugGetAuthorityEpoch")) <= 0:
		return
	if int(action_state.get("authority_action_id", -1)) != 0:
		return
	if not bool(board.call("DebugIsAuthorityTransactionFixtureCompleted", _fixture)):
		return

	_write_result(board)
	print("AUTHORITY_TRANSACTION_RECOVERED fixture=%s" % _fixture)
	_finish("AUTHORITY_TRANSACTION_FAULT_CLIENT_DONE")


func _print_timeout_diagnostics() -> void:
	var board := current_scene
	if board == null or not board.has_method("DebugGetNetworkActionState"):
		print("AUTHORITY_TRANSACTION_TIMEOUT role=%s board=unavailable" % _role)
		return
	var action_state: Dictionary = board.call("DebugGetNetworkActionState")
	print(
		"AUTHORITY_TRANSACTION_TIMEOUT role=%s fixture=%s completed=%s action=%s next_action=%d turn=%d table=%d score_0=%d score_1=%d item_hand=%d reward_count=%d reward_id=%s visual_processing=%s visual_pending=%d host_visual_playback_before=%d host_visual_playback=%d host_visual_decode_failures=%d" % [
			_role,
			_fixture,
			bool(board.call("DebugIsAuthorityTransactionFixtureCompleted", _fixture)),
			JSON.stringify(action_state),
			int(board.call("DebugGetNextNetworkAuthorityActionId")),
			int(board.call("DebugGetCurrentTurnPlayerIndex")),
			int(board.call("DebugGetTableCardCount")),
			int(board.call("DebugGetPlayerScoreValue", 0)),
			int(board.call("DebugGetPlayerScoreValue", 1)),
			int(board.call("DebugGetTotalItemHandCardCountForPlayer", 1)),
			int(board.call("DebugGetQuestionRewardGrantCount")),
			String(board.call("DebugGetLastQuestionRewardItemCardId")),
			bool(board.call("DebugIsNetworkVisualQueueProcessing")),
			int(board.call("DebugGetNetworkVisualEventQueueCount")),
			_host_visual_playback_before,
			int(board.call("DebugGetNetworkAuthorityHostVisualPlaybackCount")),
			int(board.call("DebugGetNetworkAuthorityHostVisualDecodeFailureCount")),
		]
	)


func _write_result(board: Node) -> void:
	var action_state: Dictionary = board.call("DebugGetNetworkActionState")
	var accepted_ids: Dictionary = _network.GetLastAcceptedClientActionIdsVariant()
	var lines := PackedStringArray()
	lines.append("fingerprint=%s" % String(board.call("DebugGetAuthorityTransactionStableFingerprint")))
	lines.append("score_0=%d" % int(board.call("DebugGetPlayerScoreValue", 0)))
	lines.append("score_1=%d" % int(board.call("DebugGetPlayerScoreValue", 1)))
	lines.append("rng=%s" % String(board.call("DebugGetAuthorityTransactionRngState")))
	lines.append("next_action_id=%d" % int(board.call("DebugGetNextNetworkAuthorityActionId")))
	lines.append("client_action_id=%d" % int(accepted_ids.get(1, 0)))
	lines.append("generation=%d" % int(action_state.get("generation", -1)))
	lines.append("turn=%d" % int(action_state.get("current_turn", -1)))
	lines.append("outcome=%s" % String(board.call("DebugGetMatchOutcomeKindName")))
	lines.append("authority_action_id=%d" % int(action_state.get("authority_action_id", -1)))
	lines.append("authority_epoch=%d" % int(_network.call("DebugGetAuthorityEpoch")))
	lines.append("host_visual_playback_count=%d" % int(board.call("DebugGetNetworkAuthorityHostVisualPlaybackCount")))
	lines.append("host_visual_decode_failures=%d" % int(board.call("DebugGetNetworkAuthorityHostVisualDecodeFailureCount")))
	var file := FileAccess.open(_result_path, FileAccess.WRITE)
	if file == null:
		_fail("could not write transaction result")
		return
	file.store_string("\n".join(lines) + "\n")
	file.close()


func _finish(marker: String) -> void:
	if _finished:
		return
	_finished = true
	_network.LeaveSession()
	print(marker)
	quit(0)


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("AUTHORITY_TRANSACTION_%s_FAIL %s" % [_role.to_upper(), message])
	quit(1)
