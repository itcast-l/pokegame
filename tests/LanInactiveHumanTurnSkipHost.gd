extends SceneTree

const ROOM_NAME := "CodexSmokeInactiveHumanTurnSkip"
const PLAYER_COUNT := 3
const ITEM_MODE := false
const WINNING_SCORE := 100
const INACTIVE_PLAYER_INDEX := 2
const EXPECTED_NEXT_PLAYER_INDEX := 1
const TIMEOUT_SECONDS := 30.0
const FIXTURE_SETTLE_SECONDS := 0.75
const POST_SYNC_SETTLE_SECONDS := 1.0

var _network: Node
var _started_match := false
var _board_initialized := false
var _fixture_configured := false
var _fixture_configured_at := 0.0
var _fixture_snapshot_id := -1
var _played_card := false
var _finish_scheduled := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("INACTIVE_SKIP_HOST_START")
	await process_frame
	_network.HostLan(PLAYER_COUNT, ITEM_MODE, WINNING_SCORE, ROOM_NAME, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_track_progress()

	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if _started_match or players.size() != PLAYER_COUNT:
		return

	for player in players:
		if not bool(player.get("is_ready", false)):
			return

	_started_match = true
	print("INACTIVE_SKIP_HOST_START_MATCH")
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _board_initialized:
		return

	_board_initialized = true
	print("INACTIVE_SKIP_HOST_BOARD_INIT")


func _track_progress() -> void:
	if not _board_initialized or _finish_scheduled:
		return

	var board: Node = current_scene
	if board == null:
		return

	if not _fixture_configured:
		if String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
			return
		if not board.has_method("DebugConfigureInactiveHumanTurnSkipScenario"):
			_fail("Inactive-turn fixture hook is missing")
			return
		if not bool(board.call("DebugConfigureInactiveHumanTurnSkipScenario")):
			_fail("Inactive-turn fixture hook rejected the board")
			return
		if not _assert_inactive_player_state(board):
			return

		_fixture_configured = true
		_fixture_configured_at = Time.get_ticks_msec() / 1000.0
		_fixture_snapshot_id = int(board.call("DebugGetNetworkSnapshotId"))
		print("INACTIVE_SKIP_HOST_FIXTURE inactive=2 snapshot=%d" % _fixture_snapshot_id)
		return

	if not _played_card:
		if Time.get_ticks_msec() / 1000.0 - _fixture_configured_at < FIXTURE_SETTLE_SECONDS:
			return
		if int(board.call("DebugGetCurrentTurnPlayerIndex")) != 0:
			_fail("Turn changed before the host action")
			return
		if not bool(board.call("DebugPlayFirstAvailableNetworkHostHandCard")):
			return

		_played_card = true
		print("INACTIVE_SKIP_HOST_PLAYED_CARD")
		return

	if int(board.call("DebugGetCurrentTurnPlayerIndex")) != EXPECTED_NEXT_PLAYER_INDEX:
		return

	var action_state: Dictionary = board.call("DebugGetNetworkActionState")
	if bool(action_state.get("processing_card_play", true)) \
			or bool(action_state.get("resolving_turn_sequence", true)) \
			or bool(action_state.get("pending_item_selection", true)):
		return
	if int(board.call("DebugGetNetworkSnapshotId")) <= _fixture_snapshot_id:
		return
	if int(board.call("DebugGetWinnerPlayerIndex")) != -1:
		_fail("Fixture unexpectedly declared a winner")
		return
	if not _assert_inactive_player_state(board):
		return

	_finish_scheduled = true
	print("INACTIVE_SKIP_HOST_SKIPPED_TO_1")
	_finish_after_settle()


func _assert_inactive_player_state(board: Node) -> bool:
	if int(board.call("DebugGetTotalHandCardCountForPlayer", INACTIVE_PLAYER_INDEX)) != 0 \
			or int(board.call("DebugGetDeckCardCountForPlayer", INACTIVE_PLAYER_INDEX)) != 0 \
			or int(board.call("DebugGetTotalItemHandCardCountForPlayer", INACTIVE_PLAYER_INDEX)) != 0:
		_fail("Inactive human player still has cards")
		return false
	return true


func _finish_after_settle() -> void:
	await create_timer(POST_SYNC_SETTLE_SECONDS).timeout
	if _finished:
		return

	_finished = true
	_network.LeaveSession()
	print("INACTIVE_SKIP_HOST_DONE")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("INACTIVE_SKIP_HOST_FAIL %s" % message)
	quit(1)
