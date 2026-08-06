extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 30.0
const LOBBY_STATE := 4
const LOADING_SCENE_STATE := 6
const INGAME_STATE := 7
const GAME_BOARD_SUFFIX := "scenes/game_board_scene.tscn"
const MULTIPLAYER_LOBBY_SUFFIX := "scenes/multiplayer_lobby_scene.tscn"

var _network: Node
var _mode := "observer"
var _ready_count := 0
var _saw_board := false
var _saw_loading := false
var _finished := false
var _host_disconnect_errors: Array[String] = []
var _host_disconnect_lobby_returned := false
var _idle_replacement_started := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_mode = OS.get_environment("CODEX_LOADING_DISCONNECT_MODE").strip_edges()
	if _mode.is_empty():
		_mode = "observer"
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("ErrorRaised", Callable(self, "_on_error_raised"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("CLIENT_%s_START" % _mode.to_upper())
	await process_frame
	_network.JoinLan(HOST_ADDRESS, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_scene()

	if not _finished:
		_fail("%s timed out" % _mode)


func _on_state_changed(state: int) -> void:
	print("CLIENT_%s_STATE %d" % [_mode.to_upper(), state])
	if state == 0 and _mode == "host_disconnect_idle_replacement" and _saw_loading and not _idle_replacement_started:
		_idle_replacement_started = true
		print("CLIENT_IDLE_REPLACEMENT_START")
		_network.HostLan(2, true, 100, "CodexSmokeIdleReplacement", "")
		_network.LeaveSession()
		call_deferred("_verify_idle_replacement_after_deferred")
		return

	if state == LOBBY_STATE:
		if _mode == "observer" and _saw_loading and _ready_count == 1:
			_ready_count = 2
			print("CLIENT_OBSERVER_REREADY")
			_network.SetLocalReady(true)
		elif _ready_count == 0:
			_ready_count = 1
			print("CLIENT_%s_READY" % _mode.to_upper())
			_network.SetLocalReady(true)
	elif state == LOADING_SCENE_STATE:
		_saw_loading = true
		if _mode == "dropper":
			print("CLIENT_DROPPER_EXIT_DURING_LOADING")
			_finished = true
			_network.LeaveSession()
			quit(0)


func _on_error_raised(message: String) -> void:
	if _mode == "host_disconnect":
		if _host_disconnect_lobby_returned:
			_fail("Received ErrorRaised after returning to the lobby: %s" % message)
			return
		_host_disconnect_errors.append(message)
	print("CLIENT_%s_ERROR %s" % [_mode.to_upper(), message])


func _on_board_initialize(_payload: Dictionary) -> void:
	if _mode == "observer" and not _saw_board:
		_fail("Observer received board initialization before loading cancellation")
		return
	if _mode == "observer" and _ready_count == 2 and _network.State == INGAME_STATE:
		_finished = true
		_network.LeaveSession()
		print("CLIENT_OBSERVER_RECOVERY_DONE")
		quit(0)
		return
	if _mode == "recovery" and _network.State == INGAME_STATE:
		_finished = true
		_network.LeaveSession()
		print("CLIENT_RECOVERY_DONE")
		quit(0)


func _track_scene() -> void:
	if current_scene == null:
		return

	var scene_path := String(current_scene.scene_file_path)
	if _mode == "host_disconnect" and _saw_loading and _network.State == 0 and scene_path.ends_with(MULTIPLAYER_LOBBY_SUFFIX):
		_host_disconnect_lobby_returned = true
		if _host_disconnect_errors.size() != 1:
			_fail("Expected exactly one host-disconnect error before lobby return, got %d: %s" % [_host_disconnect_errors.size(), str(_host_disconnect_errors)])
			return
		if _host_disconnect_errors[0] != "NETWORK_ERROR_HOST_DISCONNECTED":
			_fail("Expected terminal host-disconnect error before lobby return, got: %s" % _host_disconnect_errors[0])
			return
		_finished = true
		print("CLIENT_HOST_DISCONNECT_ERROR_COUNT %d" % _host_disconnect_errors.size())
		print("CLIENT_HOST_DISCONNECT_ERROR_BEFORE_LOBBY")
		print("CLIENT_HOST_DISCONNECT_RETURNED_TO_LOBBY")
		quit(0)
		return

	if _mode == "observer":
		if scene_path.ends_with(GAME_BOARD_SUFFIX):
			if not _saw_board:
				print("CLIENT_OBSERVER_ENTERED_BOARD")
			_saw_board = true
		if _saw_loading and _network.State == LOBBY_STATE and scene_path.ends_with(MULTIPLAYER_LOBBY_SUFFIX):
			print("CLIENT_OBSERVER_RETURNED_TO_LOBBY")
			return

	if _mode == "recovery" and _saw_loading and scene_path.ends_with(GAME_BOARD_SUFFIX):
		return

	if _mode == "recovery" and _network.State == LOADING_SCENE_STATE:
		_saw_loading = true


func _verify_idle_replacement_after_deferred() -> void:
	await process_frame
	await process_frame
	print("CLIENT_IDLE_REPLACEMENT_DEFERRED_WAIT_COMPLETE")
	if not _idle_replacement_started or _network.State != 0:
		_fail("Idle replacement did not remain Idle")
		return

	if current_scene == null or not String(current_scene.scene_file_path).ends_with(GAME_BOARD_SUFFIX):
		_fail("Deferred host-loss return navigated the replacement Idle session to the lobby")
		return

	_finished = true
	print("CLIENT_IDLE_REPLACEMENT_RESULT PASS")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_%s_FAIL %s" % [_mode.to_upper(), message])
	quit(1)
