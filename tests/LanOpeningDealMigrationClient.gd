extends SceneTree

const LOBBY_STATE := 4
const IN_GAME_STATE := 7
const MIGRATING_HOST_STATE := 10
const TIMEOUT_SECONDS := 75.0

var _network: Node
var _point := "hands_start"
var _ready_sent := false
var _board_initialized := false
var _migration_seen := false
var _pre_generation := -1
var _pre_hand_count := -1
var _pre_deck_count := -1
var _released := false
var _before_release_fingerprint := ""
var _post_release_frames := 0
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--point="):
			_point = argument.trim_prefix("--point=")
	_network = root.get_node("/root/NetworkManager")
	_network.connect("StateChanged", Callable(self, "_on_state"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	await process_frame
	_network.JoinLan("127.0.0.1", "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track()
	if not _finished:
		_fail("client timed out")


func _on_state(state: int) -> void:
	if state == LOBBY_STATE and not _ready_sent:
		_ready_sent = true
		_network.SetLocalReady(true)
	elif state == MIGRATING_HOST_STATE:
		_migration_seen = true


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _track() -> void:
	if not _board_initialized:
		return
	var board := current_scene
	if board == null or not board.has_method("DebugGetNetworkActionState"):
		return
	var action_state: Dictionary = board.call("DebugGetNetworkActionState")
	if _pre_generation < 0 and String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		var hand_count := int(board.call("DebugGetTotalHandCardCountForPlayer", 1))
		var deck_count := int(board.call("DebugGetDeckCardCountForPlayer", 1))
		if hand_count > 0 and deck_count > 0:
			_pre_generation = int(action_state.get("generation", -1))
			_pre_hand_count = hand_count
			_pre_deck_count = deck_count

	if not _migration_seen \
			or not bool(_network.call("DebugGetIsHost")) \
			or int(_network.call("DebugGetAuthorityEpoch")) <= 0:
		return
	if _pre_generation < 0:
		_fail("client did not capture pre-migration opening state")
		return
	var recovered_generation := int(action_state.get("generation", -1))
	if recovered_generation <= _pre_generation:
		_fail("restored generation did not advance")
		return
	if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		_fail("restored board remained in OpeningDeal")
		return
	if int(board.call("DebugGetTotalHandCardCountForPlayer", 1)) != _pre_hand_count \
			or int(board.call("DebugGetDeckCardCountForPlayer", 1)) != _pre_deck_count:
		_fail("restored opening cards are incomplete")
		return

	if not _released:
		_before_release_fingerprint = String(board.call("DebugGetAuthorityTransactionStableFingerprint"))
		if not bool(board.call("DebugReleaseOpeningDealMigrationFault")):
			_fail("could not release stale opening callback")
			return
		_released = true
		print("OPENING_DEAL_STALE_CALLBACK_RELEASED point=%s" % _point)
		return

	if not bool(board.call("DebugDidOpeningDealFaultCallbackReturn")):
		return
	_post_release_frames += 1
	if _post_release_frames < 12:
		return
	if String(board.call("DebugGetAuthorityTransactionStableFingerprint")) != _before_release_fingerprint \
			or int(action_state.get("generation", -1)) != recovered_generation \
			or String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		_fail("stale opening callback mutated restored state")
		return

	_finished = true
	print("OPENING_DEAL_MIGRATION_CLIENT_DONE point=%s generation=%d" % [_point, recovered_generation])
	_network.LeaveSession()
	quit(0)


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("OPENING_DEAL_MIGRATION_CLIENT_FAIL point=%s %s" % [_point, message])
	quit(1)
