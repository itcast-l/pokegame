extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 25.0
const LOBBY_STATE := 4
const RECONNECTING_STATE := 8
const FAILED_STATE := 9
const MAIN_MENU_SUFFIX := "scenes/main_menu_scene.tscn"

var _network: Node
var _ready_sent := false
var _board_initialized := false
var _failed_state_seen := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("ErrorRaised", Callable(self, "_on_error_raised"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("CLIENT_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if _failed_state_seen:
			_track_main_menu_transition()

	if not _finished:
		_fail("Client timed out")


func _on_state_changed(state: int) -> void:
	print("CLIENT_STATE %d" % state)
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		print("CLIENT_READY")
		_network.SetLocalReady(true)
	elif state == RECONNECTING_STATE:
		print("CLIENT_UNEXPECTED_RECONNECT")
	elif state == FAILED_STATE:
		_failed_state_seen = true
		print("CLIENT_FAILED")


func _on_error_raised(message: String) -> void:
	print("CLIENT_ERROR %s" % message)


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("CLIENT_BOARD_INIT")


func _track_main_menu_transition() -> void:
	if current_scene == null:
		return

	var scene_path := String(current_scene.scene_file_path)
	if not scene_path.ends_with(MAIN_MENU_SUFFIX):
		return

	print("CLIENT_MAIN_MENU")
	_finish()


func _finish() -> void:
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
