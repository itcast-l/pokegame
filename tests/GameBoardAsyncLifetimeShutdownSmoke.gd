extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var packed_scene := load(SCENE_PATH) as PackedScene
	var board: Node = _instantiate_board(packed_scene)
	if board == null:
		_fail("Could not instantiate game board")
		return

	root.add_child(board)
	await process_frame
	if not bool(board.call("DebugStartBoardAsyncLifetimeShutdownProbe")):
		_fail("Could not start lifetime Timer/Tween probe")
		return
	if not bool(board.call("DebugShowAdventureUpgradeSelectionOverlaySample")):
		_fail("Could not show selection-overlay shutdown fixture")
		return
	if not _stage_network_visual_fixture(board):
		return

	await process_frame
	if int(board.call("DebugGetBoardTrackedTweenCount")) < 1:
		_fail("Lifetime probe did not retain an active Tween")
		return
	if int(board.call("DebugGetBoardTrackedTaskCount")) < 2:
		_fail("Lifetime probe did not retain cancellable tasks")
		return
	if not bool(board.call("DebugIsNetworkVisualQueueProcessing")):
		_fail("Network visual playback was not active before shutdown")
		return
	if board.find_child("AdventureUpgradeSelectionOverlay", true, false) == null:
		_fail("Selection overlay was not active before shutdown")
		return

	var board_ref: WeakRef = weakref(board)
	board.queue_free()
	for _frame in 6:
		await process_frame
	if board_ref.get_ref() != null:
		_fail("Board remained alive after queued free")
		return

	var probe_script := load("res://scripts/BoardAsyncLifetimeDebugProbe.cs") as Script
	var lifetime_probe: RefCounted = probe_script.new() as RefCounted if probe_script != null else null
	if lifetime_probe == null:
		_fail("Could not instantiate lifetime-count probe")
		return
	if int(lifetime_probe.call("GetActiveLifetimeCount")) != 0:
		_fail("A board async lifetime remained active after shutdown")
		return
	await _dispose_audio_before_quit()
	print("GAME_BOARD_ASYNC_LIFETIME_SHUTDOWN_SMOKE_OK")
	quit(0)


func _instantiate_board(packed_scene: PackedScene) -> Node:
	var board: Node = packed_scene.instantiate() if packed_scene != null else null
	if board == null:
		return null
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("solo_ai_difficulty", 0)
	board.set("adventure_mode", false)
	return board


func _stage_network_visual_fixture(board: Node) -> bool:
	var fixture := board.call("DebugConfigureNetworkVisualQueueFixture") as Dictionary
	if fixture == null:
		_fail("Could not configure network visual fixture")
		return false
	var own_hand_cards := fixture["own_hand_cards"] as Array
	var table_cards := fixture["table_cards"] as Array
	if own_hand_cards.is_empty():
		_fail("Network visual fixture omitted local hand card")
		return false

	var visual_event := {
		"session_id": fixture["session_id"],
		"match_id": fixture["match_id"],
		"authority_epoch": fixture["authority_epoch"],
		"checkpoint_id": fixture["checkpoint_id"],
		"action_id": 0,
		"event_type": "play_hand_card",
		"snapshot_id": int(fixture["snapshot_id"]) + 1,
		"turn_sequence_generation": fixture["turn_sequence_generation"],
		"actor_seat_index": 0,
		"hand_index": 0,
		"played_card_code": (own_hand_cards[0] as Dictionary)["code"],
		"played_card_is_face_up": true,
		"table_card_index": table_cards.size(),
		"borrow_tide_adjusted_rank": "",
	}
	var queued_event := visual_event.duplicate(true)
	queued_event["snapshot_id"] = int(visual_event["snapshot_id"]) + 1
	var replacement_snapshot := fixture.duplicate(true)
	replacement_snapshot["snapshot_id"] = int(queued_event["snapshot_id"]) + 1
	board.call("DebugSetNetworkVisualPlaybackDelaySeconds", 60.0)
	if not bool(board.call(
		"DebugQueueDelayedNetworkVisualEventWithSnapshotFence",
		visual_event,
		queued_event,
		replacement_snapshot)):
		_fail("Could not stage network visual playback shutdown fixture")
		return false
	return true


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
	for _frame in 2:
		await process_frame
	await create_timer(0.15).timeout
	for _frame in 2:
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("GAME_BOARD_ASYNC_LIFETIME_SHUTDOWN_SMOKE_FAIL %s" % message)
	quit(1)
