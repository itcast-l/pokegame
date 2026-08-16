extends RefCounted

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const MAX_WAIT_FRAMES := 3600

var _tree: SceneTree
var _fail_callback: Callable


func _init(tree: SceneTree, fail_callback: Callable) -> void:
	_tree = tree
	_fail_callback = fail_callback


func create_board(
	enemy_avatar_id := "shrimp",
	solo_mode := false,
	recording_setup_id := ""
) -> Node:
	await _tree.process_frame
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % BOARD_SCENE_PATH)
		return null

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % BOARD_SCENE_PATH)
		return null

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", solo_mode)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", enemy_avatar_id)
	if not recording_setup_id.is_empty():
		board.set("debug_recording_setup_id", recording_setup_id)
	_tree.root.add_child(board)
	await _tree.process_frame
	await _tree.process_frame
	return board


func require_methods(board: Node, methods: Array, label: String) -> bool:
	for method in methods:
		if not board.has_method(method):
			_fail("Missing %s debug hook: %s" % [label, method])
			return false
	return true


func wait_for_actionable(board: Node, label := "actionable state") -> bool:
	for _frame_index in range(MAX_WAIT_FRAMES):
		await _tree.process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for %s" % label)
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true
	_fail("Exceeded %d process frames waiting for %s" % [MAX_WAIT_FRAMES, label])
	return false


func wait_for_actionable_table_count(
	board: Node,
	expected_table_count: int,
	label: String
) -> bool:
	for _frame_index in range(MAX_WAIT_FRAMES):
		await _tree.process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for %s" % label)
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction" \
			and int(board.call("DebugGetTableCardCount")) == expected_table_count:
			return true
	_fail(
		"Exceeded %d process frames waiting for %s (phase=%s table=%d)"
		% [
			MAX_WAIT_FRAMES,
			label,
			String(board.call("DebugGetGameLoopPhaseName")),
			int(board.call("DebugGetTableCardCount")),
		]
	)
	return false


func wait_for_pre_refill_hand_count_after_table_clear(
	board: Node,
	label: String
) -> int:
	for _frame_index in range(MAX_WAIT_FRAMES):
		await _tree.process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for %s" % label)
			return -1
		if int(board.call("DebugGetTableCardCount")) != 0:
			continue
		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "AwaitingMainPlayerAction":
			_fail("Reached actionable state before observing %s" % label)
			return -1
		return int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
	_fail("Exceeded %d process frames waiting for %s" % [MAX_WAIT_FRAMES, label])
	return -1


func wait_for_processing_hand_count(
	board: Node,
	expected_hand_count: int,
	label: String
) -> bool:
	var saw_processing := false
	for _frame_index in range(MAX_WAIT_FRAMES):
		await _tree.process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for %s" % label)
			return false
		var processing := bool(board.call("DebugIsProcessingCardPlayForOwnershipProbe"))
		var hand_count := int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
		if processing:
			saw_processing = true
			if hand_count == expected_hand_count:
				return true
			if hand_count > expected_hand_count:
				_fail("Exceeded expected hand count while waiting for %s" % label)
				return false
		elif saw_processing:
			_fail(
				"Card processing ended before %s (hand=%d)" \
				% [label, hand_count]
			)
			return false
	_fail("Exceeded %d process frames waiting for %s" % [MAX_WAIT_FRAMES, label])
	return false


func wait_for_processing_end_with_max_hand_count(
	board: Node,
	maximum_hand_count: int,
	label: String
) -> bool:
	for _frame_index in range(MAX_WAIT_FRAMES):
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for %s" % label)
			return false
		var processing := bool(board.call("DebugIsProcessingCardPlayForOwnershipProbe"))
		if not processing:
			return true
		var hand_count := int(board.call("DebugGetTotalHandCardCountForPlayer", 0))
		if hand_count > maximum_hand_count:
			_fail(
				"Exceeded maximum hand count while processing %s (hand=%d max=%d)" \
				% [label, hand_count, maximum_hand_count]
			)
			return false
		await _tree.process_frame
	_fail("Exceeded %d process frames waiting for %s" % [MAX_WAIT_FRAMES, label])
	return false


func wait_for_actionable_pending_state(
	board: Node,
	expected_pending: bool,
	label: String
) -> bool:
	for _frame_index in range(MAX_WAIT_FRAMES):
		await _tree.process_frame
		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for %s" % label)
			return false
		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction" \
			and bool(board.call("DebugHasAdventurePendingExtraBaseCardAction")) == expected_pending:
			return true
	_fail(
		"Exceeded %d process frames waiting for %s (phase=%s pending=%s)"
		% [
			MAX_WAIT_FRAMES,
			label,
			String(board.call("DebugGetGameLoopPhaseName")),
			str(bool(board.call("DebugHasAdventurePendingExtraBaseCardAction"))),
		]
	)
	return false


func dispose_audio() -> void:
	var game_audio := _tree.root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(2):
		await _tree.process_frame
	await _tree.create_timer(0.15).timeout
	for _frame in range(2):
		await _tree.process_frame


func dispose_board_and_audio(board: Node, label: String) -> bool:
	var board_ref: WeakRef = weakref(board)
	board.queue_free()
	for _frame in range(120):
		await _tree.process_frame
		if board_ref.get_ref() == null:
			break
	if board_ref.get_ref() != null:
		_fail("Board remained alive after %s shutdown" % label)
		return false

	var probe_script := load("res://scripts/BoardAsyncLifetimeDebugProbe.cs") as Script
	var lifetime_probe: RefCounted = probe_script.new() as RefCounted if probe_script != null else null
	if lifetime_probe == null:
		_fail("Could not instantiate lifetime-count probe after %s shutdown" % label)
		return false
	var active_lifetime_count := int(lifetime_probe.call("GetActiveLifetimeCount"))
	if active_lifetime_count != 0:
		_fail("%s shutdown left %d active board lifetime(s)" % [label, active_lifetime_count])
		return false

	await dispose_audio()
	return true


func _fail(message: String) -> void:
	_fail_callback.call(message)
