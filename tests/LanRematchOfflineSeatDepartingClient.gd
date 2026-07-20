extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 25.0
const LOBBY_STATE := 4

var _network: Node
var _ready_sent := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("DEPARTING_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
	if not _finished:
		_fail("Departing client timed out")


func _on_state_changed(state: int) -> void:
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)


func _on_board_initialize(_payload: Dictionary) -> void:
	_disconnect_after_settle()


func _disconnect_after_settle() -> void:
	await create_timer(0.5).timeout
	if _finished:
		return
	_finished = true
	print("DEPARTING_DISCONNECT")
	_network.LeaveSession()
	print("DEPARTING_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("DEPARTING_FAIL %s" % message)
	quit(1)
