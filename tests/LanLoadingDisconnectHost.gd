extends SceneTree

const ROOM_NAME := "CodexSmokeLoadingDisconnect"
const PLAYER_COUNT := 3
const ITEM_MODE := true
const WINNING_SCORE := 100
const TIMEOUT_SECONDS := 35.0
const READY_SETTLE_SECONDS := 0.4
const LOBBY_STATE := 4
const LOADING_SCENE_STATE := 6
const INGAME_STATE := 7
const GAME_BOARD_SUFFIX := "scenes/game_board_scene.tscn"

var _network: Node
var _mode := "non_host_disconnect"
var _required_player_count := PLAYER_COUNT
var _start_count := 0
var _start_scheduled := false
var _saw_loading := false
var _cancellation_verified := false
var _reentrant_cancel_sent := false
var _reentrant_restart_started := false
var _same_lobby_replacement_started := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_mode = OS.get_environment("CODEX_LOADING_DISCONNECT_HOST_MODE").strip_edges()
	if _mode.is_empty():
		_mode = "non_host_disconnect"
	if _mode == "host_disconnect":
		_required_player_count = 2
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_player_list_updated"))
	_network.connect("StateChanged", Callable(self, "_on_state_changed"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("HOST_START")
	await process_frame
	_network.HostLan(_required_player_count, ITEM_MODE, WINNING_SCORE, ROOM_NAME, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_verify_cancellation_if_ready()

	if not _finished:
		_fail("Host timed out")


func _on_player_list_updated(players: Array) -> void:
	if _start_count == 0:
		if _start_scheduled or players.size() != _required_player_count or not _are_all_ready(players):
			return
		_start_scheduled = true
		_start_match_after_ready_settle(1)
		return

	if not _cancellation_verified or _start_count != 1 or _start_scheduled:
		return

	if players.size() != _required_player_count or not _are_all_ready(players):
		return

	_start_scheduled = true
	_start_match_after_ready_settle(2)


func _start_match_after_ready_settle(match_number: int) -> void:
	await create_timer(READY_SETTLE_SECONDS).timeout
	if _finished or _network.State != LOBBY_STATE:
		return

	var players: Array = _network.GetPlayersVariant()
	if players.size() != _required_player_count or not _are_all_ready(players):
		_start_scheduled = false
		return

	_start_count = match_number
	_start_scheduled = false
	if match_number == 1:
		print("HOST_START_FIRST_MATCH")
	else:
		print("HOST_START_RECOVERY_MATCH")
	_network.StartMatch()


func _on_state_changed(state: int) -> void:
	print("HOST_STATE %d" % state)
	if state == LOADING_SCENE_STATE:
		_saw_loading = true
		if _mode == "host_disconnect" and _start_count == 1:
			call_deferred("_disconnect_host_during_loading")
		elif (_mode == "reentrant" or _mode == "same_lobby_reentrant") and _start_count == 1 and not _reentrant_cancel_sent:
			call_deferred("_trigger_reentrant_cancellation")
	elif state == LOBBY_STATE and _reentrant_cancel_sent:
		if _mode == "reentrant" and not _reentrant_restart_started:
			_reentrant_restart_started = true
			print("HOST_REENTRANT_START_MATCH")
			_network.StartMatch()
		elif _mode == "same_lobby_reentrant" and not _same_lobby_replacement_started:
			_same_lobby_replacement_started = true
			_rebuild_lobby_during_cancellation()


func _on_board_initialize(_payload: Dictionary) -> void:
	if _mode == "host_disconnect":
		_fail("Host-disconnect scenario initialized the board")
		return

	if _start_count != 2:
		_fail("Initial load unexpectedly initialized the board")
		return

	if _network.State != INGAME_STATE:
		_fail("Recovery match did not enter in-game")
		return

	_finish_after_recovery_delivery()


func _finish_after_recovery_delivery() -> void:
	await create_timer(0.5).timeout
	if _finished:
		return
	_finished = true
	_network.LeaveSession()
	print("HOST_DONE")
	quit(0)


func _disconnect_host_during_loading() -> void:
	if _finished or _network.State != LOADING_SCENE_STATE:
		return

	_finished = true
	print("HOST_EXIT_DURING_LOADING")
	_network.LeaveSession()
	quit(0)


func _trigger_reentrant_cancellation() -> void:
	if _finished or _network.State != LOADING_SCENE_STATE:
		return

	_reentrant_cancel_sent = true
	print("HOST_REENTRANT_CANCEL")
	if _mode == "same_lobby_reentrant":
		_disconnect_peer_during_loading()
		return
	_network.call("RpcCancelMatchStart", "Smoke cancellation")
	_verify_reentrant_match()


func _disconnect_peer_during_loading() -> void:
	var players: Array = _network.GetPlayersVariant()
	for player in players:
		var peer_id := int(player.get("peer_id", 0))
		if peer_id > 1:
			print("HOST_SAME_LOBBY_DISCONNECT_PEER %d" % peer_id)
			_network.get_multiplayer().multiplayer_peer.disconnect_peer(peer_id)
			return
	_fail("Could not find a remote peer to disconnect during loading")


func _verify_reentrant_match() -> void:
	await process_frame
	await process_frame
	print("HOST_REENTRANT_DEFERRED_WAIT_COMPLETE")
	if not _reentrant_restart_started or _network.State != LOADING_SCENE_STATE:
		_fail("Synchronous restart did not remain in LoadingScene")
		return

	if current_scene == null or not String(current_scene.scene_file_path).ends_with(GAME_BOARD_SUFFIX):
		_fail("Deferred cancellation returned the reentrant match to the lobby")
		return

	_finished = true
	_network.LeaveSession()
	print("HOST_REENTRANT_RESULT PASS")
	quit(0)


func _rebuild_lobby_during_cancellation() -> void:
	print("HOST_SAME_LOBBY_REPLACEMENT_START")
	_network.HostLan(_required_player_count, ITEM_MODE, WINNING_SCORE, "%sReplacement" % ROOM_NAME, "")
	if _network.State != LOBBY_STATE:
		_fail("Synchronous replacement did not return to Lobby")
		return
	call_deferred("_verify_same_lobby_replacement")


func _verify_same_lobby_replacement() -> void:
	await process_frame
	await process_frame
	print("HOST_SAME_LOBBY_DEFERRED_WAIT_COMPLETE")
	if not _same_lobby_replacement_started or _network.State != LOBBY_STATE:
		_fail("Synchronous replacement did not remain in Lobby")
		return

	if current_scene == null or not String(current_scene.scene_file_path).ends_with(GAME_BOARD_SUFFIX):
		_fail("Deferred cancellation navigated the replacement lobby away")
		return

	_finished = true
	_network.LeaveSession()
	print("HOST_SAME_LOBBY_RESULT PASS")
	quit(0)


func _verify_cancellation_if_ready() -> void:
	if _mode == "reentrant" or _mode == "same_lobby_reentrant":
		return

	if _cancellation_verified or not _saw_loading or _network.State != LOBBY_STATE:
		return

	var players: Array = _network.GetPlayersVariant()
	if players.size() != PLAYER_COUNT - 1:
		_fail("Disconnected player remained in the host lobby roster: %s" % [str(players)])
		return

	_cancellation_verified = true
	print("HOST_CANCELLATION_VERIFIED")


func _are_all_ready(players: Array) -> bool:
	for player in players:
		if not bool(player.get("is_ready", false)):
			return false
	return true


func _fail(message: String) -> void:
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	if _mode == "reentrant" or _mode == "same_lobby_reentrant":
		print("HOST_REENTRANT_RESULT FAIL")
	print("HOST_FAIL %s" % message)
	quit(1)
