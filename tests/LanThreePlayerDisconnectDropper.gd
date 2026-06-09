extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 15.0
const DISCONNECT_AFTER_BOARD_SECONDS := 0.4
const LOBBY_STATE := 4

var _network: Node
var _ready_sent := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("DROPPER_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	await create_timer(TIMEOUT_SECONDS).timeout
	push_error("Dropper timed out before disconnect")
	quit(1)


func _on_state_changed(state: int) -> void:
	print("DROPPER_STATE %d" % state)
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		print("DROPPER_READY")
		_network.SetLocalReady(true)


func _on_board_initialize(_payload: Dictionary) -> void:
	print("DROPPER_BOARD_INIT")
	_disconnect_after_delay()


func _disconnect_after_delay() -> void:
	await create_timer(DISCONNECT_AFTER_BOARD_SECONDS).timeout
	print("DROPPER_EXIT")
	quit(0)
