extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 25.0

var _network: Node
var _ready_sent := false
var _board_initialized := false
var _saw_reconnecting := false
var _saw_reconnected := false
var _pause_during_reconnect := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_pause_during_reconnect = OS.get_environment("CODEX_LAN_RECONNECT_PAUSED") == "1"
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("CLIENT_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	await create_timer(TIMEOUT_SECONDS).timeout
	_fail("Client timed out")


func _on_state_changed(state: int) -> void:
	print("CLIENT_STATE %d" % state)
	if state == 4 and not _ready_sent:
		_ready_sent = true
		print("CLIENT_READY")
		_network.SetLocalReady(true)
	elif state == 8:
		if _pause_during_reconnect and not paused:
			_fail("Scene tree pause was cleared before reconnecting")
			return
		_saw_reconnecting = true
	elif state == 7 and _board_initialized and _saw_reconnecting and not _saw_reconnected:
		if _pause_during_reconnect and not paused:
			_fail("Scene tree pause was cleared during reconnect")
			return
		_saw_reconnected = true
		print("CLIENT_RECONNECTED")
		_finish()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("CLIENT_BOARD_INIT")
	if _pause_during_reconnect:
		paused = true
		print("CLIENT_PAUSED_FOR_RECONNECT")


func _finish() -> void:
	if _pause_during_reconnect:
		if not paused:
			_fail("Network reconnect code cleared the fixture-owned pause")
			return
		paused = false
		print("CLIENT_PAUSE_PRESERVED")
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	paused = false
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_FAIL %s" % message)
	quit(1)
