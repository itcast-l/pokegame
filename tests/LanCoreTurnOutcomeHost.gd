extends SceneTree

const ROOM_NAME := "CodexCoreTurnOutcome"
const TIMEOUT_SECONDS := 45.0

var _network: Node
var _player_count := 3
var _started := false
var _board_initialized := false
var _scenario_runner_started := false
var _finished := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_parse_arguments()
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_players"))
	_network.connect("BoardInitialize", Callable(self, "_on_board_initialize"))
	print("CORE_TURN_LAN_HOST_%d_START" % _player_count)
	await process_frame
	_network.HostLan(_player_count, true, 100, ROOM_NAME, "")
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while not _finished and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		_try_start_scenarios()
	if not _finished:
		_fail("Host timed out")


func _parse_arguments() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--player-count="):
			_player_count = int(argument.trim_prefix("--player-count="))


func _on_players(players: Array) -> void:
	if _started or players.size() != _player_count:
		return
	for player in players:
		if not bool(player.get("is_ready", false)):
			return
	_started = true
	_network.StartMatch()


func _on_board_initialize(_payload: Dictionary) -> void:
	_board_initialized = true


func _try_start_scenarios() -> void:
	if not _board_initialized or _scenario_runner_started:
		return
	var board := current_scene
	if board == null or String(board.call("DebugGetGameLoopPhaseName")) == "OpeningDeal":
		return
	_scenario_runner_started = true
	_run_scenarios(board)


func _run_scenarios(board: Node) -> void:
	await _run_probe(board, "human_last_noncompleting_item")
	if _finished:
		return
	if _player_count == 4:
		if not bool(_network.call("DebugSetSeatAiProxyForCoreTurnProbe", 2)):
			_fail("Could not put seat 2 under AI proxy control")
			return
		await _run_probe(board, "ai_last_noncompleting_item")
		if _finished:
			return
	await _run_probe(board, "consecutive_skips")
	if _finished:
		return
	await _run_probe(board, "all_blocked")
	if _finished:
		return
	await create_timer(2.0).timeout
	_finished = true
	_network.LeaveSession()
	print("CORE_TURN_LAN_HOST_%d_DONE" % _player_count)
	quit(0)


func _run_probe(board: Node, scenario: String) -> void:
	var result := {}
	if not bool(board.call("DebugStartCoreTurnResolutionProbe", scenario, result)):
		_fail("Probe rejected %s" % scenario)
		return
	var deadline := Time.get_ticks_msec() / 1000.0 + 10.0
	while not bool(result.get("completed", false)) and Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
	if not bool(result.get("completed", false)) or String(result.get("error", "")) != "":
		_fail("Probe failed %s: %s" % [scenario, result])
		return
	if scenario == "all_blocked":
		if String(result.get("outcome", "")) != "Draw":
			_fail("All-blocked probe did not draw: %s" % result)
			return
	elif int(result.get("current_turn", -1)) != int(result.get("expected_turn", -2)):
		_fail("Turn probe mismatch %s: %s" % [scenario, result])
		return
	if scenario == "ai_last_noncompleting_item" and not bool(result.get("actor_ai_controlled", false)):
		_fail("AI probe actor was not AI-controlled")
		return
	print("CORE_TURN_LAN_HOST_%d_%s" % [_player_count, scenario.to_upper()])
	await create_timer(1.0).timeout


func _fail(message: String) -> void:
	if _finished:
		return
	_finished = true
	push_error(message)
	if _network != null:
		_network.LeaveSession()
	print("CORE_TURN_LAN_HOST_%d_FAIL %s" % [_player_count, message])
	quit(1)
