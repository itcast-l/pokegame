extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const LOBBY_STATE := 4
const INACTIVE_PLAYER_INDEX := 2
const EXPECTED_NEXT_PLAYER_INDEX := 1
const TIMEOUT_SECONDS := 30.0
const POST_SYNC_SETTLE_SECONDS := 0.25

var _network: Node
var _expected_seat := -1
var _ready_sent := false
var _board_initialized := false
var _finish_scheduled := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	if _expected_seat < 1 or _expected_seat > 2:
		_fail("Expected seat argument must be 1 or 2")
		return

	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("INACTIVE_SKIP_CLIENT_%d_START" % _expected_seat)
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_progress()

	if not _finished:
		_fail("Client %d timed out" % _expected_seat)


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--expected-seat="):
			_expected_seat = int(argument.trim_prefix("--expected-seat="))


func _on_state_changed(state: int) -> void:
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		print("INACTIVE_SKIP_CLIENT_%d_READY" % _expected_seat)
		_network.SetLocalReady(true)


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("INACTIVE_SKIP_CLIENT_%d_BOARD_INIT" % _expected_seat)


func _track_progress() -> void:
	if not _board_initialized or _finish_scheduled:
		return

	var board: Node = current_scene
	if board == null:
		return
	if int(board.call("DebugGetLocalPlayerIndex")) != _expected_seat:
		_fail("Client expected seat %d but received seat %d" % [
			_expected_seat,
			int(board.call("DebugGetLocalPlayerIndex")),
		])
		return
	if int(board.call("DebugGetCurrentTurnPlayerIndex")) != EXPECTED_NEXT_PLAYER_INDEX:
		return

	var action_state: Dictionary = board.call("DebugGetNetworkActionState")
	if bool(action_state.get("processing_card_play", true)) \
			or bool(action_state.get("resolving_turn_sequence", true)) \
			or bool(action_state.get("pending_item_selection", true)) \
			or bool(action_state.get("visual_processing", true)) \
			or int(action_state.get("visual_pending_events", 1)) != 0:
		return
	if int(board.call("DebugGetWinnerPlayerIndex")) != -1:
		_fail("Client observed an unexpected winner")
		return
	if int(board.call("DebugGetTotalHandCardCountForPlayer", INACTIVE_PLAYER_INDEX)) != 0 \
			or int(board.call("DebugGetDeckCardCountForPlayer", INACTIVE_PLAYER_INDEX)) != 0 \
			or int(board.call("DebugGetTotalItemHandCardCountForPlayer", INACTIVE_PLAYER_INDEX)) != 0:
		_fail("Client observed cards on the inactive human player")
		return
	if _expected_seat == INACTIVE_PLAYER_INDEX:
		var legal_action: Dictionary = board.call("DebugGetFirstLegalNetworkAction")
		if String(legal_action.get("kind", "")) != "None":
			_fail("Inactive human player received a legal action")
			return

	_finish_scheduled = true
	print("INACTIVE_SKIP_CLIENT_%d_SKIPPED_TO_1" % _expected_seat)
	_finish_after_settle()


func _finish_after_settle() -> void:
	await create_timer(POST_SYNC_SETTLE_SECONDS).timeout
	if _finished:
		return

	_finished = true
	_network.LeaveSession()
	print("INACTIVE_SKIP_CLIENT_%d_DONE" % _expected_seat)
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("INACTIVE_SKIP_CLIENT_%d_FAIL %s" % [_expected_seat, message])
	quit(1)
