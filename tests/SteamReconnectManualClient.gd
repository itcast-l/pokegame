extends SceneTree

const TIMEOUT_SECONDS := 120.0
const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const RECONNECTING_STATE := 8
const DISCONNECT_DELAY_SECONDS := 0.35
const POST_RECONNECT_SETTLE_SECONDS := 0.5

var _network: Node
var _ready_sent := false
var _board_initialized := false
var _drop_requested := false
var _saw_reconnecting := false
var _saw_reconnected := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	if not bool(_network.get("SteamAvailable")):
		_fail("Steam is not available")
		return

	var lobby_id_text := OS.get_environment("CODEX_STEAM_LOBBY_ID").strip_edges()
	if lobby_id_text.is_empty():
		_fail("Missing CODEX_STEAM_LOBBY_ID")
		return

	var lobby_id := int(lobby_id_text)
	if lobby_id <= 0:
		_fail("Invalid CODEX_STEAM_LOBBY_ID")
		return

	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("CLIENT_START")
	print("CLIENT_JOIN_LOBBY %d" % lobby_id)
	await process_frame
	_network.JoinSteamLobby(lobby_id, "")
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
	elif state == RECONNECTING_STATE and not _saw_reconnecting:
		_saw_reconnecting = true
		print("CLIENT_RECONNECTING")
	elif state == IN_GAME_STATE and _board_initialized and _saw_reconnecting and not _saw_reconnected:
		_saw_reconnected = true
		print("CLIENT_RECONNECTED")
		_finish_after_settle()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("CLIENT_BOARD_INIT")


func _track_progress() -> void:
	if not _board_initialized or _drop_requested:
		return

	var board: Node = current_scene
	if board == null:
		return

	if not board.has_method("DebugGetLocalSeatControlModeName"):
		_fail("Missing DebugGetLocalSeatControlModeName")
		return

	if String(board.call("DebugGetLocalSeatControlModeName")) != "HumanConnected":
		return

	_schedule_drop()


func _schedule_drop() -> void:
	if _drop_requested:
		return

	_drop_requested = true
	await create_timer(DISCONNECT_DELAY_SECONDS).timeout
	if not _network.has_method("DebugSimulateLocalReconnectDrop"):
		_fail("Missing DebugSimulateLocalReconnectDrop")
		return

	if not bool(_network.call("DebugSimulateLocalReconnectDrop")):
		_fail("Failed to simulate reconnect drop")
		return

	print("CLIENT_FORCE_RECONNECT_DROP")


func _finish_after_settle() -> void:
	await create_timer(POST_RECONNECT_SETTLE_SECONDS).timeout
	if _finished:
		return

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
