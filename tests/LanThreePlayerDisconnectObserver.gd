extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 20.0
const STABLE_INGAME_SECONDS := 2.0
const LOBBY_STATE := 4
const INGAME_STATE := 7
const RECONNECTING_STATE := 8
const FAILED_STATE := 9

var _network: Node
var _ready_sent := false
var _board_initialized := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("OBSERVER_START")
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

	if not _finished:
		_fail("Observer timed out")


func _on_state_changed(state: int) -> void:
	print("OBSERVER_STATE %d" % state)
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		print("OBSERVER_READY")
		_network.SetLocalReady(true)
	elif state == RECONNECTING_STATE:
		_fail("Observer entered reconnecting")
	elif state == FAILED_STATE:
		_fail("Observer entered failed state")


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("OBSERVER_BOARD_INIT")
	_verify_still_ingame_after_delay()


func _verify_still_ingame_after_delay() -> void:
	await create_timer(STABLE_INGAME_SECONDS).timeout
	if _finished:
		return

	if _network.State != INGAME_STATE:
		_fail("Observer left in-game state")
		return

	print("OBSERVER_STILL_INGAME")
	_finish()


func _finish() -> void:
	_finished = true
	if _network != null:
		_network.LeaveSession()
	print("OBSERVER_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("OBSERVER_FAIL %s" % message)
	quit(1)
