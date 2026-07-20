extends SceneTree

const TIMEOUT_SECONDS := 45.0

var _network: Node
var _case := "resource_failure"
var _result_path := ""
var _started := false
var _board_initialized := false
var _selection_started := false
var _zero_forced := false
var _initial_hand_count := -1
var _settled_at := -1.0
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_players"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	_network.HostLan(2, true, 100, "CodexRequiredSelectionFallback", "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track()
	if not _finished:
		_fail("host timed out")


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--case="):
			_case = argument.trim_prefix("--case=")
		elif argument.begins_with("--result-path="):
			_result_path = argument.trim_prefix("--result-path=")


func _on_players(players: Array) -> void:
	if _started or players.size() != 2:
		return
	for player in players:
		if not bool(player.get("is_ready", false)):
			return
	_started = true
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _track() -> void:
	if not _board_initialized:
		return
	var board := current_scene
	if board == null or String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return
	if not FileAccess.file_exists("%s.client_armed" % _result_path):
		return

	if not _selection_started:
		_initial_hand_count = int(board.call("DebugGetTotalHandCardCountForPlayer", 1))
		if _initial_hand_count <= 0 or not bool(board.call("DebugStartPendingHandLimitDiscardForSeat", 1)):
			_fail("could not start remote required selection")
			return
		_selection_started = true
		print("REQUIRED_SELECTION_HOST_PENDING case=%s initial=%d" % [_case, _initial_hand_count])
		return

	var pending_mode := String(board.call("DebugGetPendingItemSelectionModeName"))
	if _case == "zero_candidate" and pending_mode == "HandLimitDiscard" and not _zero_forced:
		_zero_forced = bool(board.call("DebugForcePendingRequiredSelectionZeroCandidateRecovery"))
		if not _zero_forced:
			_fail("could not force zero-candidate recovery")
			return
		print("REQUIRED_SELECTION_HOST_ZERO_FORCED")
		return

	if pending_mode != "None":
		return
	var action_state: Dictionary = board.call("DebugGetNetworkActionState")
	if int(action_state.get("authority_action_id", -1)) != 0 \
			or bool(action_state.get("processing_card_play", true)):
		return
	var final_hand_count := int(board.call("DebugGetTotalHandCardCountForPlayer", 1))
	var accepted: Dictionary = _network.GetLastAcceptedClientActionIdsVariant()
	var accepted_id := int(accepted.get(1, 0))
	if _case == "zero_candidate":
		if final_hand_count != 0 or accepted_id != 0 \
				or String(board.call("DebugGetMatchOutcomeKindName")) == "InProgress":
			return
	else:
		if final_hand_count != _initial_hand_count - 1 or accepted_id != 1:
			return

	if _settled_at < 0.0:
		_settled_at = Time.get_ticks_msec() / 1000.0
		return
	if Time.get_ticks_msec() / 1000.0 - _settled_at < 0.75:
		return
	if int(board.call("DebugGetTotalHandCardCountForPlayer", 1)) != final_hand_count \
			or int(_network.GetLastAcceptedClientActionIdsVariant().get(1, 0)) != accepted_id:
		_fail("required selection submitted more than once")
		return

	var result_file := FileAccess.open(_result_path, FileAccess.WRITE)
	if result_file == null:
		_fail("could not write result")
		return
	result_file.store_string(
		"case=%s\ninitial=%d\nfinal=%d\nclient_action_id=%d\noutcome=%s\n" % [
			_case,
			_initial_hand_count,
			final_hand_count,
			accepted_id,
			String(board.call("DebugGetMatchOutcomeKindName")),
		])
	result_file.close()
	print("REQUIRED_SELECTION_HOST_DONE case=%s" % _case)
	var client_done_path := "%s.client_done" % _result_path
	while not FileAccess.file_exists(client_done_path):
		await process_frame
	_finished = true
	_network.LeaveSession()
	quit(0)


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("REQUIRED_SELECTION_HOST_FAIL case=%s %s" % [_case, message])
	quit(1)
