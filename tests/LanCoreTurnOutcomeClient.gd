extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const LOBBY_STATE := 4
const TIMEOUT_SECONDS := 45.0

var _network: Node
var _player_count := 3
var _expected_seat := -1
var _ready_sent := false
var _board_initialized := false
var _expected_turns: Array[int] = []
var _observed_turn_count := 0
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	_expected_turns.assign([2, 1] if _player_count == 3 else [2, 1, 3])
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track()
	if not _finished:
		_fail("Client timed out")


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--player-count="):
			_player_count = int(argument.trim_prefix("--player-count="))
		elif argument.begins_with("--expected-seat="):
			_expected_seat = int(argument.trim_prefix("--expected-seat="))


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
	if int(board.call("DebugGetLocalPlayerIndex")) != _expected_seat:
		_fail("Client received the wrong seat")
		return
	if String(board.call("DebugGetMatchOutcomeKindName")) == "Draw":
		if _observed_turn_count != _expected_turns.size():
			_fail("Client missed turn snapshots before draw: %d/%d" % [_observed_turn_count, _expected_turns.size()])
			return
		_finished = true
		_network.LeaveSession()
		print("CORE_TURN_LAN_CLIENT_%d_%d_DONE" % [_player_count, _expected_seat])
		quit(0)
		return
	if _observed_turn_count < _expected_turns.size() \
			and int(board.call("DebugGetCurrentTurnPlayerIndex")) == _expected_turns[_observed_turn_count]:
		_observed_turn_count += 1
		print("CORE_TURN_LAN_CLIENT_%d_%d_STEP_%d" % [_player_count, _expected_seat, _observed_turn_count])


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("CORE_TURN_LAN_CLIENT_%d_%d_FAIL %s" % [_player_count, _expected_seat, message])
	quit(1)
