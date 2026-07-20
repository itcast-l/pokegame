extends SceneTree

const LOBBY_STATE := 4
const TIMEOUT_SECONDS := 45.0

var _network: Node
var _case := "resource_failure"
var _result_path := ""
var _ready_sent := false
var _board_initialized := false
var _armed := false
var _pending_seen := false
var _unexpected_release_sent := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	_network.JoinLan("127.0.0.1", "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track()
	if not _finished:
		_fail("client timed out")


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--case="):
			_case = argument.trim_prefix("--case=")
		elif argument.begins_with("--result-path="):
			_result_path = argument.trim_prefix("--result-path=")


func _on_state(state: int) -> void:
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _track() -> void:
	if not _board_initialized:
		return
	var board := current_scene
	if board == null:
		return
	if not _armed and String(board.call("DebugGetGameLoopPhaseName")) != "OpeningDeal":
		if _case == "resource_failure":
			board.call("DebugForceNextRequiredItemSelectionOverlayResourceFailure")
		elif _case == "instantiation_failure":
			board.call("DebugForceNextRequiredItemSelectionOverlayInstantiationFailure")
		var armed_file := FileAccess.open("%s.client_armed" % _result_path, FileAccess.WRITE)
		if armed_file == null:
			_fail("could not write client armed marker")
			return
		armed_file.store_string("armed\n")
		armed_file.close()
		_armed = true
		print("REQUIRED_SELECTION_CLIENT_ARMED case=%s" % _case)

	var pending_mode := String(board.call("DebugGetPendingItemSelectionModeName"))
	if pending_mode == "HandLimitDiscard":
		_pending_seen = true
	if _case == "unexpected_release" and _pending_seen and not _unexpected_release_sent:
		if board.get_node_or_null("ItemSelectionOverlay") != null:
			_unexpected_release_sent = bool(board.call("DebugUnexpectedlyReleaseActiveSelectionOverlay"))
			if _unexpected_release_sent:
				print("REQUIRED_SELECTION_CLIENT_UNEXPECTED_RELEASE")

	if not FileAccess.file_exists(_result_path):
		return
	if _case != "zero_candidate" and not _pending_seen:
		_fail("client never observed pending selection")
		return
	var done_file := FileAccess.open("%s.client_done" % _result_path, FileAccess.WRITE)
	if done_file == null:
		_fail("could not write client done marker")
		return
	done_file.store_string("done\n")
	done_file.close()
	_finished = true
	_network.LeaveSession()
	print("REQUIRED_SELECTION_CLIENT_DONE case=%s" % _case)
	quit(0)


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("REQUIRED_SELECTION_CLIENT_FAIL case=%s %s" % [_case, message])
	quit(1)
