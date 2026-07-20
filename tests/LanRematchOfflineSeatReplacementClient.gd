extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 30.0
const LOBBY_STATE := 4

var _network: Node
var _ready_scheduled := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("REPLACEMENT_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
	if not _finished:
		_fail("Replacement client timed out")


func _on_state_changed(state: int) -> void:
	if state == LOBBY_STATE and not _ready_scheduled:
		_ready_scheduled = true
		print("REPLACEMENT_JOINED_UNREADY")
		_set_ready_after_gate_window()


func _set_ready_after_gate_window() -> void:
	await create_timer(1.2).timeout
	if _finished:
		return
	_network.SetLocalReady(true)
	print("REPLACEMENT_READY")


func _on_board_initialize(payload: Dictionary) -> void:
	if String(payload.get("match_id", "")).is_empty():
		_fail("Replacement match context was empty")
		return
	print("REPLACEMENT_BOARD_INIT")
	_finish_after_settle()


func _finish_after_settle() -> void:
	await create_timer(0.5).timeout
	if _finished:
		return
	_finished = true
	_network.LeaveSession()
	print("REPLACEMENT_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("REPLACEMENT_FAIL %s" % message)
	quit(1)
