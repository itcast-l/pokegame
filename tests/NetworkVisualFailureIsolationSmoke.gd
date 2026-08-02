extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var packed_scene := load(SCENE_PATH) as PackedScene
	var board := _instantiate_board(packed_scene)
	if board == null:
		_fail("Could not instantiate game board")
		return

	root.add_child(board)
	await process_frame
	var fixture := board.call("DebugConfigureNetworkVisualQueueFixture") as Dictionary
	if fixture == null:
		_fail("Could not configure network visual fixture")
		return

	var own_hand_cards := fixture["own_hand_cards"] as Array
	var table_cards := fixture["table_cards"] as Array
	if own_hand_cards.is_empty() or table_cards.is_empty():
		_fail("Network visual fixture omitted cards")
		return

	var first_snapshot_id := int(fixture["snapshot_id"]) + 1
	var hand_event := _with_context({
		"event_type": "play_hand_card",
		"snapshot_id": first_snapshot_id,
		"turn_sequence_generation": fixture["turn_sequence_generation"],
		"actor_seat_index": 0,
		"hand_index": 0,
		"played_card_code": str((own_hand_cards[0] as Dictionary)["code"]),
		"played_card_is_face_up": true,
		"table_card_index": table_cards.size(),
		"borrow_tide_adjusted_rank": "",
	}, fixture)

	var failure_count_before := int(board.call("DebugGetNetworkVisualPlaybackFailureCount"))
	board.call("DebugInjectNetworkVisualPlaybackFailures", 1)
	if not bool(board.call("DebugQueueDecodedNetworkVisualEvent", hand_event)):
		_fail("Could not queue injected client visual failure")
		return
	if not await _wait_for_failure(board, failure_count_before + 1):
		return
	if not bool(board.call("DebugIsNetworkVisualFastForwarding")):
		_fail("Client visual failure did not wait for a recovery snapshot")
		return

	var replacement_snapshot := fixture.duplicate(true)
	replacement_snapshot["snapshot_id"] = first_snapshot_id + 1
	if not bool(board.call("DebugSubmitNetworkVisualSnapshot", replacement_snapshot)):
		_fail("Could not submit recovery snapshot")
		return
	await process_frame
	if bool(board.call("DebugIsNetworkVisualFastForwarding")):
		_fail("Recovery snapshot did not leave fast-forward mode")
		return
	if int(board.call("DebugGetNetworkLastAppliedSnapshotId")) != int(replacement_snapshot["snapshot_id"]):
		_fail("Recovery snapshot was not applied")
		return

	var recovered_state := str(board.call("DebugGetNetworkStateFingerprint"))
	var host_hand_event := hand_event.duplicate(true)
	host_hand_event["snapshot_id"] = int(replacement_snapshot["snapshot_id"]) + 1
	host_hand_event["table_card_index"] = 0
	var host_playback_before := int(board.call("DebugGetNetworkAuthorityHostVisualPlaybackCount"))
	if not bool(board.call("DebugQueueDecodedNetworkAuthorityHostVisualEvent", host_hand_event)):
		_fail("Could not queue Host visual-only hand event")
		return
	if not await _wait_for_host_tween(board, host_playback_before + 1):
		return

	var flight := board.find_child("PlayedCardFlight", true, false)
	if flight == null:
		_fail("Host visual-only event did not create its transient node")
		return
	flight.queue_free()
	board.call("DebugKillTrackedBoardTweens")
	if not await _wait_for_visual_idle(board, 180):
		_fail("Canceled Host Tween left visual playback locked")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != recovered_state:
		_fail("Host visual-only playback mutated the authority model")
		return

	var followup_event := _with_context({
		"event_type": "bomb_pair",
		"snapshot_id": int(host_hand_event["snapshot_id"]) + 1,
		"turn_sequence_generation": fixture["turn_sequence_generation"],
	}, fixture)
	if not bool(board.call("DebugQueueDecodedNetworkAuthorityHostVisualEvent", followup_event)):
		_fail("Could not queue follow-up Host visual event")
		return
	if not await _wait_for_host_playback(board, host_playback_before + 2):
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != recovered_state:
		_fail("Follow-up Host visual event mutated the authority model")
		return

	board.call("DebugSetNetworkVisualPlaybackDelaySeconds", 60.0)
	var shutdown_event := followup_event.duplicate(true)
	shutdown_event["snapshot_id"] = int(followup_event["snapshot_id"]) + 1
	if not bool(board.call("DebugQueueDecodedNetworkAuthorityHostVisualEvent", shutdown_event)):
		_fail("Could not queue shutdown visual event")
		return
	await process_frame
	if not bool(board.call("DebugIsNetworkVisualQueueProcessing")):
		_fail("Shutdown visual event was not processing")
		return

	var board_ref: WeakRef = weakref(board)
	board.queue_free()
	for _frame in 8:
		await process_frame
	if board_ref.get_ref() != null:
		_fail("Game board remained alive after visual playback shutdown")
		return

	await _dispose_audio_before_quit()
	print("NETWORK_VISUAL_FAILURE_ISOLATION_SMOKE_OK")
	quit(0)


func _instantiate_board(packed_scene: PackedScene) -> Node:
	var board := packed_scene.instantiate() if packed_scene != null else null
	if board == null:
		return null
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("solo_ai_difficulty", 0)
	board.set("adventure_mode", false)
	return board


func _with_context(payload: Dictionary, fixture: Dictionary) -> Dictionary:
	payload["session_id"] = fixture["session_id"]
	payload["match_id"] = fixture["match_id"]
	payload["authority_epoch"] = fixture["authority_epoch"]
	payload["checkpoint_id"] = fixture["checkpoint_id"]
	payload["action_id"] = 0
	return payload


func _wait_for_failure(board: Node, expected_count: int) -> bool:
	for _frame in 120:
		if int(board.call("DebugGetNetworkVisualPlaybackFailureCount")) >= expected_count \
				and not bool(board.call("DebugIsNetworkVisualQueueProcessing")):
			return true
		await process_frame
	_fail("Injected visual failure did not settle")
	return false


func _wait_for_host_tween(board: Node, expected_playback_count: int) -> bool:
	for _frame in 120:
		if int(board.call("DebugGetNetworkAuthorityHostVisualPlaybackCount")) >= expected_playback_count \
				and int(board.call("DebugGetBoardTrackedTweenCount")) > 0:
			return true
		await process_frame
	_fail("Host visual-only event did not start a tracked Tween")
	return false


func _wait_for_host_playback(board: Node, expected_count: int) -> bool:
	for _frame in 120:
		if int(board.call("DebugGetNetworkAuthorityHostVisualPlaybackCount")) >= expected_count \
				and not bool(board.call("DebugIsNetworkVisualQueueProcessing")):
			return true
		await process_frame
	_fail("Follow-up Host visual event did not complete")
	return false


func _wait_for_visual_idle(board: Node, frame_limit: int) -> bool:
	for _frame in frame_limit:
		if not bool(board.call("DebugIsNetworkVisualQueueProcessing")) \
				and int(board.call("DebugGetNetworkVisualEventQueueCount")) == 0:
			return true
		await process_frame
	return false


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
	print("NETWORK_VISUAL_FAILURE_ISOLATION_SMOKE_FAIL %s" % message)
	quit(1)
