extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const LOBBY_STATE := 4
const EXPECTED_ROLL := 2
const EXPECTED_STARTING_SEAT := 2
const TIMEOUT_SECONDS := 35.0

var _network: Node
var _expected_seat := -1
var _ready_sent := false
var _board_initialized := false
var _saw_dice := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_board()
	if not _finished:
		_fail("Client timed out")


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--expected-seat="):
			_expected_seat = int(argument.trim_prefix("--expected-seat="))


func _on_state_changed(state: int) -> void:
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _track_board() -> void:
	if not _board_initialized:
		return
	var board := current_scene
	if board == null:
		return
	if int(board.call("DebugGetLocalPlayerIndex")) != _expected_seat:
		_fail("Client received unexpected seat")
		return
	if bool(board.call("DebugIsDiceRollPlaying")):
		_saw_dice = true
		if not _assert_shared_opening_state(board):
			return
		if String(board.call("DebugGetGameLoopPhaseName")) != "OpeningDeal" \
				or bool(board.call("DebugCanMainPlayerAct")):
			_fail("Client input unlocked during opening dice")
			return
		print("LAN_STARTING_DICE_CLIENT_%d_DURING roll=2 turn=2" % _expected_seat)
		return
	if not _saw_dice or String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return
	if not _assert_shared_opening_state(board):
		return
	var can_act := bool(board.call("DebugCanMainPlayerAct"))
	if can_act != (_expected_seat == EXPECTED_STARTING_SEAT):
		_fail("Client action permission did not match starting seat")
		return
	_finished = true
	_finish_after_settle()


func _assert_shared_opening_state(board: Node) -> bool:
	if int(board.call("DebugGetOpeningDiceRollResult")) != EXPECTED_ROLL \
			or int(board.call("DebugGetOpeningStartingTurnSeat")) != EXPECTED_STARTING_SEAT \
			or int(board.call("DebugGetCurrentTurnPlayerIndex")) != EXPECTED_STARTING_SEAT:
		_fail("Client observed mismatched opening result or turn")
		return false
	return true


func _finish_after_settle() -> void:
	print("LAN_STARTING_DICE_CLIENT_%d_AFTER roll=2 turn=2 can_act=%s" % [
		_expected_seat,
		str(_expected_seat == EXPECTED_STARTING_SEAT).to_lower(),
	])
	await create_timer(1.0).timeout
	_network.LeaveSession()
	print("LAN_STARTING_DICE_CLIENT_%d_DONE" % _expected_seat)
	quit(0)


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("LAN_STARTING_DICE_CLIENT_%d_FAIL %s" % [_expected_seat, message])
	quit(1)
