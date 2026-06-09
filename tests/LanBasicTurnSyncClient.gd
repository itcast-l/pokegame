extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 25.0
const LOBBY_STATE := 4

var _network: Node
var _ready_sent := false
var _board_initialized := false
var _saw_host_turn := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("CLIENT_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_progress()

	if not _finished:
		_fail("Client timed out")


func _on_state_changed(state: int) -> void:
	print("CLIENT_STATE %d" % state)
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		print("CLIENT_READY")
		_network.SetLocalReady(true)


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("CLIENT_BOARD_INIT")


func _track_progress() -> void:
	if not _board_initialized:
		return

	var board: Node = current_scene
	if board == null:
		return

	var current_turn := int(board.call("DebugGetCurrentTurnPlayerIndex"))
	if current_turn == 0:
		_saw_host_turn = true
		return

	if not _saw_host_turn or current_turn != 1:
		return

	print("CLIENT_SAW_CLIENT_TURN")
	_finished = true
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_FAIL %s" % message)
	quit(1)
