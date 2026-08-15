extends SceneTree

const ROOM_NAME := "CodexStartingPlayerDice"
const PLAYER_COUNT := 3
const EXPECTED_ROLL := 2
const EXPECTED_STARTING_SEAT := 2
const TIMEOUT_SECONDS := 35.0

var _network: Node
var _started_match := false
var _board_initialized := false
var _saw_dice := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	_network.HostLan(PLAYER_COUNT, true, 100, ROOM_NAME, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_board()
	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if _started_match or players.size() != PLAYER_COUNT:
		return
	for player in players:
		if not bool(player.get("is_ready", false)):
			return
	_started_match = true
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _track_board() -> void:
	if not _board_initialized:
		return
	var board := current_scene
	if board == null:
		return
	if bool(board.call("DebugIsDiceRollPlaying")):
		_saw_dice = true
		if not _assert_shared_opening_state(board, true):
			return
		return
	if not _saw_dice or String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return
	if not _assert_shared_opening_state(board, false):
		return
	_finished = true
	_finish_after_client_settle()


func _assert_shared_opening_state(board: Node, during_opening: bool) -> bool:
	if int(board.call("DebugGetOpeningDiceRollResult")) != EXPECTED_ROLL \
			or int(board.call("DebugGetOpeningStartingTurnSeat")) != EXPECTED_STARTING_SEAT \
			or int(board.call("DebugGetCurrentTurnPlayerIndex")) != EXPECTED_STARTING_SEAT:
		_fail("Host observed mismatched opening result or turn")
		return false
	if during_opening:
		if String(board.call("DebugGetGameLoopPhaseName")) != "OpeningDeal" \
				or bool(board.call("DebugCanMainPlayerAct")):
			_fail("Host opening barrier released during dice")
			return false
		print("LAN_STARTING_DICE_HOST_DURING roll=2 turn=2")
	else:
		print("LAN_STARTING_DICE_HOST_AFTER roll=2 turn=2")
	return true


func _finish_after_client_settle() -> void:
	await create_timer(4.0).timeout
	_network.LeaveSession()
	print("LAN_STARTING_DICE_HOST_DONE")
	quit(0)


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("LAN_STARTING_DICE_HOST_FAIL %s" % message)
	quit(1)
