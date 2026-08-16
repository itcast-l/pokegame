extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const FEEDBACK_SECONDS := 1.2
const RECT_TOLERANCE := 0.75
const SOLO_CASES := [
	{"size": Vector2i(1280, 720), "players": 2},
	{"size": Vector2i(1280, 720), "players": 3},
	{"size": Vector2i(1280, 720), "players": 4},
	{"size": Vector2i(1920, 1080), "players": 4},
	{"size": Vector2i(2560, 1080), "players": 4},
	{"size": Vector2i(960, 720), "players": 4},
]
const NETWORK_LOCAL_SEATS := [1, 2, 3]

var _failed := false
var _checked_feedback_expiry := false
var _screenshot_dir := ""


func _initialize() -> void:
	TranslationServer.set_locale("en")
	_screenshot_dir = OS.get_environment("DECKLAND_CAPTURE_THRESHOLD_SCREENSHOT_DIR")
	if not _screenshot_dir.is_empty():
		DirAccess.make_dir_recursive_absolute(_screenshot_dir)
	call_deferred("_run")


func _run() -> void:
	for spec in SOLO_CASES:
		if not await _audit_case(spec, false, 0):
			return
	for local_seat in NETWORK_LOCAL_SEATS:
		var spec := {
			"size": Vector2i(1280, 720),
			"players": 4,
		}
		if not await _audit_case(spec, true, local_seat):
			return
	print("CONTINUOUS_CAPTURE_THRESHOLD_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _audit_case(spec: Dictionary, network_mode: bool, local_seat: int) -> bool:
	var viewport_size: Vector2i = spec["size"]
	var player_count: int = spec["players"]
	var viewport := SubViewport.new()
	viewport.size = viewport_size
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var board := await _instantiate_board(viewport, player_count, network_mode, local_seat)
	if board == null:
		return false
	if not board.find_children("*CaptureThresholdIndicator", "", true, false).is_empty():
		_fail("Capture threshold badge nodes are still present")
		return false

	for seat in range(4):
		var state := board.call("DebugGetCaptureThresholdUiState", seat) as Dictionary
		if state.is_empty():
			_fail("Missing threshold UI state for seat %d" % seat)
			return false
		var expected_local := seat == local_seat
		if int(state["local_player_index"]) != local_seat \
			or bool(state["is_local_player"]) != expected_local:
			_fail("Local-player state mismatch for seat %d: %s" % [seat, state])
			return false
		if not expected_local:
			continue
		if int(state["view_slot_index"]) != 0:
			_fail("Local seat %d did not map to the bottom view slot" % local_seat)
			return false

	if not network_mode and not await _audit_threshold_text_updates(board, local_seat):
		return false
	if not await _audit_feedback_privacy_and_layout(
		board,
		viewport,
		local_seat,
		player_count,
		viewport_size,
		not network_mode
	):
		return false
	if not await _audit_status_tooltip_privacy(board, local_seat, player_count, viewport_size):
		return false

	viewport.queue_free()
	await process_frame
	return true


func _instantiate_board(
	viewport: SubViewport,
	player_count: int,
	network_mode: bool,
	local_seat: int
) -> Node:
	var packed := load(SCENE_PATH) as PackedScene
	var board := packed.instantiate() if packed != null else null
	if board == null:
		_fail("Could not instantiate board")
		return null
	board.set("player_count", player_count)
	board.set("item_mode", false)
	board.set("solo_mode", not network_mode)
	board.set("adventure_mode", false)
	if network_mode:
		board.set("network_session_id", "capture-threshold-smoke-session-%d" % local_seat)
		board.set("network_match_id", "capture-threshold-smoke-match-%d" % local_seat)
		board.set("network_wait_for_initialize", true)
		board.set("local_player_index", local_seat)
	viewport.add_child(board)
	await process_frame
	await process_frame
	await process_frame
	await process_frame
	return board


func _audit_threshold_text_updates(board: Node, local_seat: int) -> bool:
	if not bool(board.call("DebugSetBasePlayCaptureThreshold", 3)):
		_fail("Could not set debug threshold to 3")
		return false
	await process_frame
	if int(board.call("DebugGetBasePlayCaptureThreshold")) != 3:
		_fail("Debug threshold did not update to 3")
		return false
	if not bool(board.call("DebugSetBasePlayCaptureThreshold", 4)):
		_fail("Could not set debug threshold to 4")
		return false
	await process_frame
	if int(board.call("DebugGetBasePlayCaptureThreshold")) != 4:
		_fail("Debug threshold did not update to 4")
		return false
	return true


func _audit_feedback_privacy_and_layout(
	board: Node,
	viewport: SubViewport,
	local_seat: int,
	player_count: int,
	viewport_size: Vector2i,
	capture_visuals: bool
) -> bool:
	var opponent_seat := 0 if local_seat != 0 else 1
	if opponent_seat >= player_count:
		_fail("No opponent seat available for feedback privacy audit")
		return false
	for big_tide in [false, true]:
		board.call("DebugShowCaptureThresholdFeedback", opponent_seat, big_tide, 5)
		await process_frame
		var opponent := board.call("DebugGetCaptureThresholdUiState", opponent_seat) as Dictionary
		if bool(opponent["feedback_visible"]):
			_fail("Opponent threshold feedback became visible: %s" % opponent)
			return false

	board.call("DebugShowCaptureThresholdFeedback", local_seat, false, 4)
	await process_frame
	var blocked := board.call("DebugGetCaptureThresholdUiState", local_seat) as Dictionary
	var blocked_text := String(blocked["feedback_text"])
	if not bool(blocked["feedback_visible"]) \
		or blocked_text.is_empty() \
		or not blocked_text.contains("4") \
		or blocked_text.contains("{0}"):
		_fail("Blocked feedback content mismatch: %s" % blocked)
		return false
	var avatar_rect: Rect2 = blocked["avatar_rect"]
	var feedback_rect: Rect2 = blocked["feedback_rect"]
	if not _inside_viewport(feedback_rect, viewport_size) \
		or not feedback_rect.size.is_equal_approx(Vector2(196.0, 44.0)):
		_fail("Blocked feedback escaped fixed bounds: %s" % blocked)
		return false
	if feedback_rect.position.y < avatar_rect.end.y + 6.0 - RECT_TOLERANCE:
		_fail("Feedback gap below avatar is less than 6px: %s" % blocked)
		return false
	if absf(feedback_rect.get_center().x - avatar_rect.get_center().x) > RECT_TOLERANCE:
		_fail("Feedback is not horizontally centered on the local avatar: %s" % blocked)
		return false
	if capture_visuals and not await _capture_screenshot(viewport, player_count, "blocked", 0.18):
		return false

	board.call("DebugShowCaptureThresholdFeedback", local_seat, true, 5)
	await process_frame
	var tide := board.call("DebugGetCaptureThresholdUiState", local_seat) as Dictionary
	var tide_text := String(tide["feedback_text"])
	if not bool(tide["feedback_visible"]) \
		or tide_text.is_empty() \
		or tide_text == blocked_text:
		_fail("Big Tide feedback content mismatch: %s" % tide)
		return false
	if capture_visuals and not await _capture_screenshot(viewport, player_count, "big_tide", 0.18):
		return false
	if not _checked_feedback_expiry:
		_checked_feedback_expiry = true
		await create_timer(FEEDBACK_SECONDS + 0.25).timeout
		var expired := board.call("DebugGetCaptureThresholdUiState", local_seat) as Dictionary
		if bool(expired["feedback_visible"]):
			_fail("Threshold feedback remained visible past its duration")
			return false
	return true


func _capture_screenshot(
	viewport: SubViewport,
	player_count: int,
	state_name: String,
	delay_seconds := 0.0
) -> bool:
	if _screenshot_dir.is_empty():
		return true
	if delay_seconds > 0.0:
		await create_timer(delay_seconds).timeout
	else:
		await process_frame
	var output_path := "%s/%dx%d_%dp_%s.png" % [
		_screenshot_dir,
		viewport.size.x,
		viewport.size.y,
		player_count,
		state_name,
	]
	var texture := viewport.get_texture()
	var image := texture.get_image() if texture != null else null
	if image == null:
		_fail("Could not read rendered threshold screenshot for %s" % output_path)
		return false
	var error := image.save_png(output_path)
	if error != OK:
		_fail("Could not save threshold screenshot %s: %s" % [output_path, error_string(error)])
		return false
	return true


func _audit_status_tooltip_privacy(
	board: Node,
	local_seat: int,
	player_count: int,
	viewport_size: Vector2i
) -> bool:
	if not bool(board.call("DebugShowPlayerStatusTooltipForTest", local_seat)):
		_fail("Could not show local status tooltip")
		return false
	await process_frame
	var local_state := board.call("DebugGetCaptureThresholdUiState", local_seat) as Dictionary
	var local_tooltip_rect: Rect2 = local_state["tooltip_rect"]
	if not bool(local_state["tooltip_visible"]) \
		or not bool(local_state["tooltip_capture_threshold_visible"]) \
		or String(local_state["tooltip_capture_threshold_text"]).is_empty() \
		or not local_tooltip_rect.size.is_equal_approx(Vector2(220.0, 112.0)) \
		or not _inside_viewport(local_tooltip_rect, viewport_size):
		_fail("Local status tooltip mismatch: %s" % local_state)
		return false

	var opponent_seat := 0 if local_seat != 0 else 1
	if opponent_seat >= player_count \
		or not bool(board.call("DebugShowPlayerStatusTooltipForTest", opponent_seat)):
		_fail("Could not show opponent status tooltip")
		return false
	await process_frame
	var opponent_state := board.call("DebugGetCaptureThresholdUiState", opponent_seat) as Dictionary
	var opponent_tooltip_rect: Rect2 = opponent_state["tooltip_rect"]
	if not bool(opponent_state["tooltip_visible"]) \
		or bool(opponent_state["tooltip_capture_threshold_visible"]) \
		or not String(opponent_state["tooltip_capture_threshold_text"]).is_empty() \
		or not opponent_tooltip_rect.size.is_equal_approx(Vector2(220.0, 88.0)) \
		or not _inside_viewport(opponent_tooltip_rect, viewport_size):
		_fail("Opponent status tooltip mismatch: %s" % opponent_state)
		return false
	return true


func _inside_viewport(rect: Rect2, viewport_size: Vector2i) -> bool:
	return rect.position.x >= 0.0 and rect.position.y >= 0.0 \
		and rect.end.x <= float(viewport_size.x) \
		and rect.end.y <= float(viewport_size.y)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("CONTINUOUS_CAPTURE_THRESHOLD_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
