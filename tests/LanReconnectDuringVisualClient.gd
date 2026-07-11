extends SceneTree

const HOST_ADDRESS := "127.0.0.1"
const TIMEOUT_SECONDS := 25.0
const VISUAL_DELAY_SECONDS := 3.0
const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const RECONNECTING_STATE := 8
const DEFAULT_POST_RECONNECT_HOLD_SECONDS := 0.0
const DEFAULT_VISUAL_CAPTURE_DIR := ""
const RECOVERED_CAPTURE_DELAY_SECONDS := 0.35
const STABLE_RECOVERED_CAPTURE_DELAY_SECONDS := 0.8

var _network: Node
var _ready_sent := false
var _board_initialized := false
var _saw_reconnecting := false
var _saw_overlay_during_reconnect := false
var _saw_catchup_pending_during_reconnect := false
var _saw_visual_queue_during_reconnect := false
var _saw_reconnected := false
var _finished := false
var _post_reconnect_hold_seconds := DEFAULT_POST_RECONNECT_HOLD_SECONDS
var _visual_capture_dir := DEFAULT_VISUAL_CAPTURE_DIR
var _saved_overlay_frame := false
var _saved_recovered_frame := false
var _saved_recovered_stable_frame := false
var _overlay_capture_started := false
var _completion_started := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_post_reconnect_hold_seconds = max(
		0.0,
		float(OS.get_environment("CODEX_VISUAL_MANUAL_HOLD_SECONDS")))
	_visual_capture_dir = OS.get_environment("CODEX_VISUAL_CAPTURE_DIR").strip_edges()
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
	elif state == RECONNECTING_STATE:
		_saw_reconnecting = true
		print("CLIENT_RECONNECTING")
	elif state == IN_GAME_STATE and _board_initialized and _saw_reconnecting and not _saw_reconnected:
		_saw_reconnected = true
		print("CLIENT_RECONNECTED")


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("CLIENT_BOARD_INIT")
	var board: Node = current_scene
	if board != null:
		board.call("DebugSetNetworkVisualPlaybackDelaySeconds", VISUAL_DELAY_SECONDS)


func _track_progress() -> void:
	var board: Node = current_scene
	if board == null or not _board_initialized:
		return

	if not _saw_reconnecting:
		return

	if bool(board.call("DebugIsReconnectOverlayVisible")):
		_saw_overlay_during_reconnect = true
		print("CLIENT_SAW_OVERLAY")
		_request_overlay_capture_if_needed()

	if bool(board.call("DebugIsReconnectCatchupPendingCompletion")):
		_saw_catchup_pending_during_reconnect = true
		print("CLIENT_SAW_CATCHUP_PENDING")

	if bool(board.call("DebugIsNetworkVisualQueueProcessing")):
		_saw_visual_queue_during_reconnect = true
		print("CLIENT_SAW_VISUAL_QUEUE")

	if not _saw_reconnected:
		return

	if bool(board.call("DebugIsReconnectOverlayVisible")):
		return

	if bool(board.call("DebugIsReconnectCatchupPendingCompletion")):
		return

	if bool(board.call("DebugIsNetworkVisualQueueProcessing")):
		return

	if not _saw_overlay_during_reconnect:
		_fail("Reconnect overlay was never visible during visual catch-up")
		return

	if not _saw_catchup_pending_during_reconnect:
		_fail("Reconnect catch-up never stayed pending during visual playback")
		return

	if not _saw_visual_queue_during_reconnect:
		_fail("Visual queue never processed during reconnect catch-up")
		return

	if _completion_started:
		return

	_completion_started = true
	_complete_after_reconnect_capture()


func _complete_after_reconnect_capture() -> void:
	await process_frame
	await process_frame
	if _visual_capture_dir.is_empty():
		_save_visual_frame_now("recovered")
		_finish_after_hold()
		return

	await _capture_visual_frame_after_render("recovered", RECOVERED_CAPTURE_DELAY_SECONDS)
	await _capture_visual_frame_after_render("recovered_stable", STABLE_RECOVERED_CAPTURE_DELAY_SECONDS)
	_finish_after_hold()


func _finish() -> void:
	_finished = true
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_DONE")
	quit(0)


func _finish_after_hold() -> void:
	if _post_reconnect_hold_seconds > 0.0:
		await create_timer(_post_reconnect_hold_seconds).timeout
		if _finished:
			return

	_finish()


func _request_overlay_capture_if_needed() -> void:
	if _visual_capture_dir.is_empty() or _saved_overlay_frame or _overlay_capture_started:
		return

	_overlay_capture_started = true
	_capture_overlay_frame_after_render()


func _capture_overlay_frame_after_render() -> void:
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	_save_visual_frame_now("overlay")


func _capture_visual_frame_after_render(kind: String, delay_seconds: float) -> void:
	if _visual_capture_dir.is_empty():
		return

	if delay_seconds > 0.0:
		await create_timer(delay_seconds).timeout

	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	_save_visual_frame_now(kind)


func _save_visual_frame_now(kind: String) -> void:
	if _visual_capture_dir.is_empty():
		return

	if kind == "overlay" and _saved_overlay_frame:
		return

	if kind == "recovered" and _saved_recovered_frame:
		return

	if kind == "recovered_stable" and _saved_recovered_stable_frame:
		return

	var dir := DirAccess.open(_visual_capture_dir)
	if dir == null:
		var make_dir_error := DirAccess.make_dir_recursive_absolute(_visual_capture_dir)
		if make_dir_error != OK:
			print("CLIENT_VISUAL_CAPTURE_FAIL %s %s" % [kind, error_string(make_dir_error)])
			return

	var scene_root: Node = current_scene
	if scene_root == null:
		print("CLIENT_VISUAL_CAPTURE_FAIL %s scene_null" % kind)
		return

	var image = scene_root.get_viewport().get_texture().get_image()
	if image == null:
		print("CLIENT_VISUAL_CAPTURE_FAIL %s image_null" % kind)
		return

	var save_path := _visual_capture_dir.path_join("client_%s.png" % kind)
	var save_error: int = image.save_png(save_path)
	if save_error != OK:
		print("CLIENT_VISUAL_CAPTURE_FAIL %s %s" % [kind, error_string(save_error)])
		return

	print("CLIENT_VISUAL_CAPTURE %s %s" % [kind, save_path])
	if kind == "overlay":
		_saved_overlay_frame = true
	elif kind == "recovered":
		_saved_recovered_frame = true
	else:
		_saved_recovered_stable_frame = true


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("CLIENT_FAIL %s" % message)
	quit(1)
