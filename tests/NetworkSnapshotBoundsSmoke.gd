extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	var board := packed_scene.instantiate() if packed_scene != null else null
	if board == null:
		_fail("Could not instantiate game board")
		return

	root.add_child(board)
	await process_frame
	var valid_snapshot := board.call("BuildNetworkBoardSnapshotForSeat", 0, 1) as Dictionary
	if valid_snapshot == null:
		_fail("Could not build valid network snapshot baseline")
		return
	var seat_states: Array = []
	for seat_index in range(int(board.get("player_count"))):
		seat_states.append({
			"seat_index": seat_index,
			"mode": "HumanConnected",
			"peer_id": 0,
			"stable_user_id": "",
			"reconnect_token": "",
		})
	valid_snapshot["seat_control_states"] = seat_states
	if bool(board.call("DebugRejectsNetworkSnapshotPayload", valid_snapshot)):
		_fail("Rejected valid network snapshot baseline")
		return
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", valid_snapshot)):
		_fail("Could not apply valid network snapshot baseline")
		return
	var baseline_state := str(board.call("DebugGetNetworkStateFingerprint"))
	if not baseline_state.contains("table:"):
		_fail("Network state fingerprint omits table card identities")
		return

	# Snapshot shape validation alone is insufficient: malformed nested entries
	# must be rejected before the apply helpers can clear or rebuild live lists.
	var malformed_nested_snapshot := valid_snapshot.duplicate(true)
	var malformed_table_cards := malformed_nested_snapshot["table_cards"] as Array
	malformed_table_cards.append("not-a-table-card-dictionary")
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", malformed_nested_snapshot)):
		_fail("Accepted snapshot with malformed nested table card")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Malformed nested snapshot partially changed board state")
		return

	# Variant integers are int64. Values outside int32 must never become a
	# plausible count or visual index through narrowing conversion.
	var overflow_snapshot := {"deck_counts": [4294967296, 0]}
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", overflow_snapshot)):
		_fail("Accepted snapshot with overflowing deck count")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Overflow snapshot changed board state")
		return

	var negative_snapshot := {"deck_counts": [-1, 0]}
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", negative_snapshot)):
		_fail("Accepted snapshot with negative deck count")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Negative snapshot changed board state")
		return

	var overflow_event := {
		"event_type": "draw_card",
		"snapshot_id": 1,
		"turn_sequence_generation": 0,
		"actor_seat_index": 0,
		"display_index": 4294967296,
	}
	if not bool(board.call("DebugRejectsNetworkVisualEventPayload", overflow_event)):
		_fail("Accepted visual event with overflowing display index")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Overflow visual event changed board state")
		return

	var negative_event := overflow_event.duplicate()
	negative_event["display_index"] = -1
	if not bool(board.call("DebugRejectsNetworkVisualEventPayload", negative_event)):
		_fail("Accepted visual event with negative display index")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Negative visual event changed board state")
		return

	# Each hostile case below starts from a complete legal visual-event shape and
	# changes exactly one boundary field. This verifies rejection is caused by the
	# hostile value itself and leaves the live snapshot untouched.
	var current_hand_counts: Array = valid_snapshot["hand_counts"] as Array
	if int(current_hand_counts[0]) < 10:
		var legal_draw_boundary_event := {
			"event_type": "draw_card",
			"snapshot_id": int(valid_snapshot["snapshot_id"]) + 2,
			"turn_sequence_generation": int(valid_snapshot["turn_sequence_generation"]),
			"actor_seat_index": 0,
			"display_index": int(current_hand_counts[0]),
			"drawn_card_code": "",
		}
		if bool(board.call("DebugRejectsNetworkVisualEventPayload", legal_draw_boundary_event)):
			_fail("Rejected maximum legal draw display index")
			return
		if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
			_fail("Legal draw boundary probe changed board state")
			return

	var baseline_table_cards: Array = valid_snapshot["table_cards"] as Array
	if not baseline_table_cards.is_empty():
		var legal_lens_event := {
			"event_type": "lens_reveal",
			"snapshot_id": int(valid_snapshot["snapshot_id"]) + 3,
			"turn_sequence_generation": int(valid_snapshot["turn_sequence_generation"]),
			"actor_seat_index": 0,
			"split_center_x": 0.0,
			"split_center_y": 0.0,
			"target_table_card_indexes": [0],
		}
		if bool(board.call("DebugRejectsNetworkVisualEventPayload", legal_lens_event)):
			_fail("Rejected legal Lens visual event")
			return
		var overflowing_lens_event := legal_lens_event.duplicate(true)
		overflowing_lens_event["target_table_card_indexes"] = [baseline_table_cards.size()]
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", overflowing_lens_event)):
			_fail("Accepted Lens event with out-of-range table index")
			return
		var duplicate_lens_event := legal_lens_event.duplicate(true)
		duplicate_lens_event["target_table_card_indexes"] = [0, 0]
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", duplicate_lens_event)):
			_fail("Accepted Lens event with duplicate table indexes")
			return

		var legal_capture_event := {
			"event_type": "capture_to_deck",
			"snapshot_id": int(valid_snapshot["snapshot_id"]) + 4,
			"turn_sequence_generation": int(valid_snapshot["turn_sequence_generation"]),
			"actor_seat_index": 0,
			"capture_start_index": 0,
			"extra_table_card_indexes": [],
			"flight_stagger_seconds": 0.0,
		}
		if bool(board.call("DebugRejectsNetworkVisualEventPayload", legal_capture_event)):
			_fail("Rejected legal Capture visual event")
			return
		var overflowing_capture_event := legal_capture_event.duplicate(true)
		overflowing_capture_event["capture_start_index"] = baseline_table_cards.size()
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", overflowing_capture_event)):
			_fail("Accepted Capture event with out-of-range start index")
			return
		var duplicate_capture_event := legal_capture_event.duplicate(true)
		duplicate_capture_event["extra_table_card_indexes"] = [0, 0]
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", duplicate_capture_event)):
			_fail("Accepted Capture event with duplicate extra indexes")
			return
		var non_finite_capture_event := legal_capture_event.duplicate(true)
		non_finite_capture_event["flight_stagger_seconds"] = INF
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", non_finite_capture_event)):
			_fail("Accepted Capture event with non-finite animation delay")
			return

	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Hostile Lens/Capture matrix changed board state")
		return

	if not _run_full_visual_event_boundary_matrix(board, valid_snapshot):
		return
	if not _run_nested_snapshot_boundary_matrix(board, valid_snapshot):
		return

	# These four effects mutate hands, decks, or the table. Queue each behind a
	# deliberately delayed legal hand-play event, then stage a replacement
	# snapshot while the processor is awaiting. The queued effect must never run
	# against the replacement state; the processor must drain and apply that
	# snapshot exactly once.
	for delayed_event_type in ["steal_hand_card", "steal_deck_cards", "bomb_pair", "manta_tide_shift"]:
		var queue_fixture := board.call("DebugConfigureNetworkVisualQueueFixture") as Dictionary
		if queue_fixture == null:
			_fail("Could not configure delayed queue fixture for %s" % delayed_event_type)
			return
		var fixture_state := str(board.call("DebugGetNetworkStateFingerprint"))
		var fixture_snapshot_id := int(queue_fixture["snapshot_id"])
		var fixture_generation := int(queue_fixture["turn_sequence_generation"])
		var fixture_own_cards: Array = queue_fixture["own_hand_cards"] as Array
		if fixture_own_cards.is_empty():
			_fail("Delayed queue fixture omitted local blocking card")
			return
		var delayed_blocker := {
			"event_type": "play_hand_card",
			"snapshot_id": fixture_snapshot_id + 1,
			"turn_sequence_generation": fixture_generation,
			"actor_seat_index": 0,
			"hand_index": 0,
			"played_card_code": str((fixture_own_cards[0] as Dictionary)["code"]),
			"played_card_is_face_up": true,
			"table_card_index": int((queue_fixture["table_cards"] as Array).size()),
			"borrow_tide_adjusted_rank": "",
		}
		var delayed_event := {
			"event_type": delayed_event_type,
			"snapshot_id": fixture_snapshot_id + 2,
			"turn_sequence_generation": fixture_generation,
		}
		match delayed_event_type:
			"steal_hand_card":
				delayed_event["actor_seat_index"] = 0
				delayed_event["target_seat_index"] = 1
				delayed_event["target_hand_index"] = 0
				delayed_event["item_card_id"] = ""
				delayed_event["played_card_code"] = str(queue_fixture["debug_target_hand_card_code"])
				delayed_event["attached_item_card_ids"] = []
			"steal_deck_cards":
				delayed_event["actor_seat_index"] = 0
				delayed_event["target_seat_index"] = 1
				delayed_event["moved_card_count"] = 1
		var replacement_snapshot := queue_fixture.duplicate(true)
		replacement_snapshot["snapshot_id"] = fixture_snapshot_id + 3
		if bool(board.call("DebugRejectsNetworkVisualEventPayload", delayed_blocker)):
			_fail("Delayed queue blocker is not a legal event for %s" % delayed_event_type)
			return
		if bool(board.call("DebugRejectsNetworkVisualEventPayload", delayed_event)):
			_fail("Delayed queue target is not a legal event for %s" % delayed_event_type)
			return
		if bool(board.call("DebugRejectsNetworkSnapshotPayload", replacement_snapshot)):
			_fail("Delayed queue replacement snapshot is invalid for %s" % delayed_event_type)
			return
		board.call("DebugSetNetworkVisualPlaybackDelaySeconds", 0.05)
		if not bool(board.call("DebugQueueDelayedNetworkVisualEventWithSnapshotFence", delayed_blocker, delayed_event, replacement_snapshot)):
			_fail("Could not stage delayed queue fence for %s" % delayed_event_type)
			return
		for frame_index in range(240):
			if not bool(board.call("DebugIsNetworkVisualQueueProcessing")):
				break
			await process_frame
		board.call("DebugSetNetworkVisualPlaybackDelaySeconds", 0.0)
		if bool(board.call("DebugIsNetworkVisualQueueProcessing")) or int(board.call("DebugGetNetworkVisualEventQueueCount")) != 0:
			_fail("Delayed queue did not drain for %s" % delayed_event_type)
			return
		if int(board.call("DebugGetNetworkLastAppliedSnapshotId")) != int(replacement_snapshot["snapshot_id"]):
			_fail("Delayed queue did not apply replacement snapshot for %s" % delayed_event_type)
			return
		var delayed_applied_state := str(board.call("DebugGetNetworkStateFingerprint"))
		if not delayed_applied_state.begins_with("%s|" % int(replacement_snapshot["snapshot_id"])) \
			or int(board.call("DebugGetTotalHandCardCountForPlayer", 0)) != 1 \
			or int(board.call("DebugGetTotalHandCardCountForPlayer", 1)) != 1:
			_fail("Delayed queued %s mutated replacement snapshot state" % delayed_event_type)
			return
		var delayed_final_state := delayed_applied_state
		await process_frame
		if str(board.call("DebugGetNetworkStateFingerprint")) != delayed_final_state:
			_fail("Delayed queued %s mutated after replacement snapshot" % delayed_event_type)
			return

	# Exercise the production queue path, rather than calling a visual handler
	# directly. A newer reconnect snapshot staged while a visual event is queued
	# must fence the event, drain the queue, and become the only applied state.
	var original_hand_for_fence: Array = valid_snapshot["own_hand_cards"] as Array
	var original_hand_counts_for_fence: Array = valid_snapshot["hand_counts"] as Array
	if not original_hand_for_fence.is_empty() and int(original_hand_counts_for_fence[0]) > 0:
		var fenced_hand_event := {
			"event_type": "play_hand_card",
			"snapshot_id": int(valid_snapshot["snapshot_id"]) + 20,
			"turn_sequence_generation": int(valid_snapshot["turn_sequence_generation"]),
			"actor_seat_index": 0,
			"hand_index": 0,
			"played_card_code": str((original_hand_for_fence[0] as Dictionary)["code"]),
			"played_card_is_face_up": true,
			"table_card_index": int((valid_snapshot["table_cards"] as Array).size()),
			"borrow_tide_adjusted_rank": "",
		}
		var fenced_reconnect_snapshot := valid_snapshot.duplicate(true)
		fenced_reconnect_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 21
		if not bool(board.call("DebugQueueDecodedNetworkVisualEventWithSnapshotFence", fenced_hand_event, fenced_reconnect_snapshot)):
			_fail("Could not stage real queued hand-play snapshot fence")
			return
		await process_frame
		if bool(board.call("DebugIsNetworkVisualQueueProcessing")):
			_fail("Queued snapshot fence left visual processor running")
			return
		if int(board.call("DebugGetNetworkVisualEventQueueCount")) != 0:
			_fail("Queued snapshot fence did not drain visual event queue")
			return
		if int(board.call("DebugGetNetworkLastAppliedSnapshotId")) != int(fenced_reconnect_snapshot["snapshot_id"]):
			_fail("Queued snapshot fence did not apply reconnect snapshot")
			return
		var fenced_state := str(board.call("DebugGetNetworkStateFingerprint"))
		await process_frame
		if str(board.call("DebugGetNetworkStateFingerprint")) != fenced_state:
			_fail("Fenced queued event changed reconnect snapshot after queue drain")
			return

		var post_fence_recovery := valid_snapshot.duplicate(true)
		post_fence_recovery["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 22
		if not bool(board.call("DebugApplyNetworkSnapshotPayload", post_fence_recovery)):
			_fail("Valid snapshot did not recover after queued snapshot fence")
			return

	# Decode the events against the original state, then replace their sources with
	# a valid newer snapshot. Playback must reject the stale event without changing
	# the replacement snapshot. These cover hand play, draw, and deck theft.
	var generation := int(valid_snapshot["turn_sequence_generation"])
	var original_hand: Array = valid_snapshot["own_hand_cards"] as Array
	var original_hand_counts: Array = valid_snapshot["hand_counts"] as Array
	if not original_hand.is_empty() and int(original_hand_counts[0]) > 0:
		var stale_hand_event := {
			"event_type": "play_hand_card",
			"snapshot_id": int(valid_snapshot["snapshot_id"]) + 10,
			"turn_sequence_generation": generation,
			"actor_seat_index": 0,
			"hand_index": 0,
			"played_card_code": str((original_hand[0] as Dictionary)["code"]),
			"played_card_is_face_up": true,
			"table_card_index": int((valid_snapshot["table_cards"] as Array).size()),
			"borrow_tide_adjusted_rank": "",
		}
		var empty_hand_snapshot := valid_snapshot.duplicate(true)
		empty_hand_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 100
		var empty_hand_counts: Array = empty_hand_snapshot["hand_counts"] as Array
		empty_hand_counts[0] = 0
		empty_hand_snapshot["own_hand_cards"] = []
		if not bool(board.call("DebugPlayDecodedNetworkVisualEventAfterSnapshot", stale_hand_event, empty_hand_snapshot)):
			_fail("Could not stage stale hand-play event")
			return
		await process_frame
		var stale_hand_state := str(board.call("DebugGetNetworkStateFingerprint"))
		await process_frame
		if str(board.call("DebugGetNetworkStateFingerprint")) != stale_hand_state:
			_fail("Stale hand-play event changed the replacement snapshot")
			return
		var recovered_hand_snapshot := valid_snapshot.duplicate(true)
		recovered_hand_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 101
		if not bool(board.call("DebugApplyNetworkSnapshotPayload", recovered_hand_snapshot)):
			_fail("Valid snapshot did not recover after stale hand-play event")
			return

	if int(original_hand_counts[0]) < 10:
		var stale_draw_event := {
			"event_type": "draw_card",
			"snapshot_id": int(valid_snapshot["snapshot_id"]) + 12,
			"turn_sequence_generation": generation,
			"actor_seat_index": 0,
			"display_index": int(original_hand_counts[0]),
		}
		var empty_draw_snapshot := valid_snapshot.duplicate(true)
		empty_draw_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 102
		var empty_draw_counts: Array = empty_draw_snapshot["hand_counts"] as Array
		empty_draw_counts[0] = 0
		empty_draw_snapshot["own_hand_cards"] = []
		if not bool(board.call("DebugPlayDecodedNetworkVisualEventAfterSnapshot", stale_draw_event, empty_draw_snapshot)):
			_fail("Could not stage stale draw event")
			return
		var stale_draw_state := str(board.call("DebugGetNetworkStateFingerprint"))
		await process_frame
		if str(board.call("DebugGetNetworkStateFingerprint")) != stale_draw_state:
			_fail("Stale draw event changed the replacement snapshot")
			return
		var recovered_draw_snapshot := valid_snapshot.duplicate(true)
		recovered_draw_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 103
		if not bool(board.call("DebugApplyNetworkSnapshotPayload", recovered_draw_snapshot)):
			_fail("Valid snapshot did not recover after stale draw event")
			return

	var item_source_snapshot := valid_snapshot.duplicate(true)
	item_source_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 104
	var item_source_counts: Array = item_source_snapshot["item_hand_counts"] as Array
	item_source_counts[0] = 1
	item_source_snapshot["own_item_hand_cards"] = [{"id": "bomb"}]
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", item_source_snapshot)):
		_fail("Could not construct item source snapshot")
		return
	var stale_item_event := {
		"event_type": "play_item_card",
		"snapshot_id": int(valid_snapshot["snapshot_id"]) + 13,
		"turn_sequence_generation": generation,
		"actor_seat_index": 0,
		"hand_index": 0,
		"item_card_id": "bomb",
		"play_presentation": "table_overlay",
		"target_table_card_index": int((valid_snapshot["table_cards"] as Array).size()),
	}
	var empty_item_snapshot := valid_snapshot.duplicate(true)
	empty_item_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 105
	if not bool(board.call("DebugPlayDecodedNetworkVisualEventAfterSnapshot", stale_item_event, empty_item_snapshot)):
		_fail("Could not stage stale table-overlay event")
		return
	var stale_item_state := str(board.call("DebugGetNetworkStateFingerprint"))
	await process_frame
	if str(board.call("DebugGetNetworkStateFingerprint")) != stale_item_state:
		_fail("Stale table-overlay event changed the replacement snapshot")
		return
	var recovered_item_snapshot := valid_snapshot.duplicate(true)
	recovered_item_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 106
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", recovered_item_snapshot)):
		_fail("Valid snapshot did not recover after stale table-overlay event")
		return

	var original_decks: Array = valid_snapshot["deck_counts"] as Array
	if original_decks.size() > 1 and int(original_decks[1]) > 0:
		var stale_deck_event := {
			"event_type": "steal_deck_cards",
			"snapshot_id": int(valid_snapshot["snapshot_id"]) + 11,
			"turn_sequence_generation": generation,
			"actor_seat_index": 0,
			"target_seat_index": 1,
			"moved_card_count": 1,
		}
		var empty_deck_snapshot := valid_snapshot.duplicate(true)
		empty_deck_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 108
		var empty_deck_counts: Array = empty_deck_snapshot["deck_counts"] as Array
		empty_deck_counts[1] = 0
		if not bool(board.call("DebugPlayDecodedNetworkVisualEventAfterSnapshot", stale_deck_event, empty_deck_snapshot)):
			_fail("Could not stage stale deck-theft event")
			return
		var stale_deck_state := str(board.call("DebugGetNetworkStateFingerprint"))
		await process_frame
		if str(board.call("DebugGetNetworkStateFingerprint")) != stale_deck_state:
			_fail("Stale deck-theft event changed the replacement snapshot")
			return
		var recovered_deck_snapshot := valid_snapshot.duplicate(true)
		recovered_deck_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 109
		if not bool(board.call("DebugApplyNetworkSnapshotPayload", recovered_deck_snapshot)):
			_fail("Valid snapshot did not recover after stale deck-theft event")
			return

	var recovery_snapshot := valid_snapshot.duplicate(true)
	recovery_snapshot["snapshot_id"] = int(valid_snapshot["snapshot_id"]) + 110
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", recovery_snapshot)):
		_fail("Valid snapshot did not recover after rejected payloads")
		return

	board.queue_free()
	print("NETWORK_SNAPSHOT_BOUNDS_SMOKE_OK")
	quit(0)


func _run_full_visual_event_boundary_matrix(board: Node, original_snapshot: Dictionary) -> bool:
	# Use the production debug fixture so every legal event is decoded against a
	# populated hand, deck, and table instead of a source-level approximation.
	var fixture := board.call("DebugConfigureNetworkVisualQueueFixture") as Dictionary
	if fixture == null:
		_fail("Could not build full visual-event boundary fixture")
		return false
	# DebugConfigureNetworkVisualQueueFixture leaves the remote hand identities in
	# place. Do not apply its privacy-filtered snapshot yet: steal-hand validation
	# correctly requires the current source card, which a recipient snapshot hides.
	var baseline := str(board.call("DebugGetNetworkStateFingerprint"))
	var generation := int(fixture["turn_sequence_generation"])
	var event_snapshot_id := int(fixture["snapshot_id"]) + 1
	var own_hand := fixture["own_hand_cards"] as Array
	var table_cards := fixture["table_cards"] as Array
	if own_hand.is_empty() or table_cards.size() < 2:
		_fail("Full visual-event fixture is missing cards")
		return false
	var card_code := str((own_hand[0] as Dictionary)["code"])

	var cases: Array[Dictionary] = [
		{
			"name": "play_hand", "legal": {"event_type": "play_hand_card", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "hand_index": 0, "played_card_code": card_code, "played_card_is_face_up": true, "table_card_index": table_cards.size(), "borrow_tide_adjusted_rank": ""},
			"bad_key": "hand_index", "bad_value": 1,
		},
		{
			"name": "draw", "legal": {"event_type": "draw_card", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "display_index": 1, "drawn_card_code": ""},
			"bad_key": "display_index", "bad_value": 10,
		},
		{
			"name": "deck_shuffle", "legal": {"event_type": "deck_shuffle", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0},
			"bad_key": "actor_seat_index", "bad_value": 1,
		},
		{
			"name": "lens", "legal": {"event_type": "lens_reveal", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "split_center_x": 0.0, "split_center_y": 0.0, "target_table_card_indexes": [0]},
			"bad_key": "target_table_card_indexes", "bad_value": [table_cards.size()],
		},
		{
			"name": "table_shuffle", "legal": {"event_type": "table_shuffle", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "shuffle_target_indexes": [0, 1]},
			"bad_key": "shuffle_target_indexes", "bad_value": [0, 0],
		},
		{
			"name": "question_refresh", "legal": {"event_type": "question_refresh", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "table_card_index": table_cards.size()},
			"bad_key": "table_card_index", "bad_value": table_cards.size() + 1,
		},
		{
			"name": "capture_to_deck", "legal": {"event_type": "capture_to_deck", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "capture_start_index": 0, "extra_table_card_indexes": [], "flight_stagger_seconds": 0.0},
			"bad_key": "capture_start_index", "bad_value": table_cards.size(),
		},
		{
			"name": "steal_hand", "legal": {"event_type": "steal_hand_card", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "target_seat_index": 1, "target_hand_index": 0, "played_card_code": card_code, "item_card_id": "", "attached_item_card_ids": []},
			"bad_key": "target_hand_index", "bad_value": 1,
		},
		{
			"name": "steal_deck", "legal": {"event_type": "steal_deck_cards", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "target_seat_index": 1, "moved_card_count": 1},
			"bad_key": "moved_card_count", "bad_value": 2,
		},
		{
			"name": "bomb_pair", "legal": {"event_type": "bomb_pair", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation},
			"bad_key": "snapshot_id", "bad_value": -1,
		},
		{
			"name": "manta", "legal": {"event_type": "manta_tide_shift", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation},
			"bad_key": "turn_sequence_generation", "bad_value": generation + 1,
		},
	]
	for test_case in cases:
		if not _assert_visual_boundary(board, baseline, test_case["legal"] as Dictionary, str(test_case["name"]) + " legal"):
			return false
		var hostile := (test_case["legal"] as Dictionary).duplicate(true)
		hostile[str(test_case["bad_key"])] = test_case["bad_value"]
		if not _assert_visual_rejected(board, baseline, hostile, str(test_case["name"]) + " index/counter"):
			return false

	var non_finite_lens := (cases[3]["legal"] as Dictionary).duplicate(true)
	non_finite_lens["split_center_x"] = NAN
	if not _assert_visual_rejected(board, baseline, non_finite_lens, "lens NaN coordinate"):
		return false
	var non_finite_capture := (cases[6]["legal"] as Dictionary).duplicate(true)
	non_finite_capture["flight_stagger_seconds"] = -INF
	if not _assert_visual_rejected(board, baseline, non_finite_capture, "capture negative infinity stagger"):
		return false

	# Play-item and question-reward require an item-hand shape. Bomb is a shipped,
	# playable question reward and table overlay, so it exercises both constrained paths.
	var item_fixture := fixture.duplicate(true)
	item_fixture["snapshot_id"] = int(board.call("DebugGetNetworkLastAppliedSnapshotId")) + 1
	item_fixture["item_hand_counts"] = [1, 0]
	item_fixture["own_item_hand_cards"] = [{"id": "bomb"}]
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", item_fixture)):
		_fail("Could not apply item visual-event boundary fixture")
		return false
	baseline = str(board.call("DebugGetNetworkStateFingerprint"))
	event_snapshot_id = int(item_fixture["snapshot_id"]) + 1
	var item_event := {"event_type": "play_item_card", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "hand_index": 0, "item_card_id": "bomb", "play_presentation": "table_overlay", "target_table_card_index": table_cards.size()}
	if not _assert_visual_boundary(board, baseline, item_event, "play_item legal"):
		return false
	var bad_item_index := item_event.duplicate(true)
	bad_item_index["target_table_card_index"] = table_cards.size() + 1
	if not _assert_visual_rejected(board, baseline, bad_item_index, "play_item table index"):
		return false
	var reward_event := {"event_type": "question_reward", "snapshot_id": event_snapshot_id, "turn_sequence_generation": generation, "actor_seat_index": 0, "item_card_id": "bomb", "hand_index": 1, "start_global_x": 0.0, "start_global_y": 0.0, "requires_magnet_attachment": false}
	if not _assert_visual_boundary(board, baseline, reward_event, "question_reward legal"):
		return false
	var bad_reward_float := reward_event.duplicate(true)
	bad_reward_float["start_global_y"] = INF
	if not _assert_visual_rejected(board, baseline, bad_reward_float, "question_reward infinite coordinate"):
		return false
	var bad_reward_index := reward_event.duplicate(true)
	bad_reward_index["hand_index"] = 10
	if not _assert_visual_rejected(board, baseline, bad_reward_index, "question_reward hand counter"):
		return false

	# Capture reveal has a separate atomically decoded nested entry shape.
	var reveal_fixture := item_fixture.duplicate(true)
	reveal_fixture["snapshot_id"] = int(board.call("DebugGetNetworkLastAppliedSnapshotId")) + 1
	var reveal_tables := reveal_fixture["table_cards"] as Array
	var reveal_table := (reveal_tables[0] as Dictionary).duplicate(true)
	reveal_table["is_face_up"] = false
	reveal_tables[0] = reveal_table
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", reveal_fixture)):
		_fail("Could not apply capture-reveal boundary fixture")
		return false
	baseline = str(board.call("DebugGetNetworkStateFingerprint"))
	var reveal_code := str(reveal_table["code"])
	var reveal_event := {"event_type": "capture_reveal", "snapshot_id": int(reveal_fixture["snapshot_id"]) + 1, "turn_sequence_generation": generation, "actor_seat_index": 0, "reveal_table_card_indexes": [0], "reveal_card_codes": [reveal_code], "reveal_attached_item_card_ids_csv": [""], "reveal_point_values": [1], "reveal_point_double_flags": [false]}
	if not _assert_visual_boundary(board, baseline, reveal_event, "capture_reveal legal"):
		return false
	var malformed_reveal := reveal_event.duplicate(true)
	malformed_reveal["reveal_point_values"] = [1, 2]
	if not _assert_visual_rejected(board, baseline, malformed_reveal, "capture_reveal mismatched nested count"):
		return false

	var restored := original_snapshot.duplicate(true)
	restored["snapshot_id"] = int(board.call("DebugGetNetworkLastAppliedSnapshotId")) + 1
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", restored)):
		_fail("Could not restore snapshot after visual-event boundary matrix")
		return false
	return true


func _run_nested_snapshot_boundary_matrix(board: Node, valid_snapshot: Dictionary) -> bool:
	var fixture := board.call("DebugConfigureNetworkVisualQueueFixture") as Dictionary
	if fixture == null:
		_fail("Could not build nested snapshot boundary fixture")
		return false
	var baseline := str(board.call("DebugGetNetworkStateFingerprint"))
	var matrix_snapshot := fixture as Dictionary
	var card := ((matrix_snapshot["own_hand_cards"] as Array)[0] as Dictionary).duplicate(true)
	var table_card := ((matrix_snapshot["table_cards"] as Array)[0] as Dictionary).duplicate(true)
	var hostile_cases: Array[Dictionary] = []

	for key in ["protocol_version", "match_id", "scores", "deck_counts", "hand_counts", "item_hand_counts", "own_hand_cards", "own_item_hand_cards", "revealed_hand_cards", "revealed_item_hand_cards", "table_cards", "seat_control_states", "pending_selection_mode", "opening_deal_animation_playing"]:
		var missing := matrix_snapshot.duplicate(true)
		missing.erase(key)
		hostile_cases.append({"name": "snapshot missing " + key, "payload": missing})

	for key in ["snapshot_id", "turn_sequence_generation", "scores", "deck_counts", "hand_counts", "item_hand_counts", "pending_selection_mode", "opening_deal_animation_playing"]:
		var wrong_type := matrix_snapshot.duplicate(true)
		wrong_type[key] = "wrong-type"
		hostile_cases.append({"name": "snapshot wrong type " + key, "payload": wrong_type})

	for extreme in [9223372036854775807, -9223372036854775807 - 1]:
		for key in ["snapshot_id", "turn_sequence_generation"]:
			var long_value := matrix_snapshot.duplicate(true)
			long_value[key] = extreme
			hostile_cases.append({"name": "snapshot int64 " + key, "payload": long_value})
		var long_count := matrix_snapshot.duplicate(true)
		long_count["deck_counts"] = [extreme, 0]
		hostile_cases.append({"name": "snapshot int64 deck count", "payload": long_count})

	var too_many_hand_cards: Array = []
	for ignored in range(9):
		too_many_hand_cards.append(card.duplicate(true))
	var oversized_hand := matrix_snapshot.duplicate(true)
	oversized_hand["hand_counts"] = [9, 0]
	oversized_hand["own_hand_cards"] = too_many_hand_cards
	hostile_cases.append({"name": "snapshot hand array exceeds maximum", "payload": oversized_hand})
	var too_many_items: Array = []
	for ignored in range(9):
		too_many_items.append({"id": "bomb"})
	var oversized_items := matrix_snapshot.duplicate(true)
	oversized_items["item_hand_counts"] = [9, 0]
	oversized_items["own_item_hand_cards"] = too_many_items
	hostile_cases.append({"name": "snapshot item array exceeds maximum", "payload": oversized_items})
	var oversized_table := matrix_snapshot.duplicate(true)
	var many_tables: Array = []
	for ignored in range(512):
		many_tables.append(table_card.duplicate(true))
	oversized_table["table_cards"] = many_tables
	hostile_cases.append({"name": "snapshot table array exceeds maximum", "payload": oversized_table})
	var oversized_seats := matrix_snapshot.duplicate(true)
	var seats := oversized_seats["seat_control_states"] as Array
	seats.append((seats[0] as Dictionary).duplicate(true))
	oversized_seats["seat_control_states"] = seats
	hostile_cases.append({"name": "snapshot seat array exceeds player count", "payload": oversized_seats})

	var bad_card_code := matrix_snapshot.duplicate(true)
	var bad_cards := bad_card_code["own_hand_cards"] as Array
	var bad_card := (bad_cards[0] as Dictionary).duplicate(true)
	bad_card["code"] = "x".repeat(65)
	bad_cards[0] = bad_card
	hostile_cases.append({"name": "snapshot card code string exceeds maximum", "payload": bad_card_code})
	var bad_attachment := matrix_snapshot.duplicate(true)
	var attachment_cards := bad_attachment["own_hand_cards"] as Array
	var attachment_card := (attachment_cards[0] as Dictionary).duplicate(true)
	attachment_card["attached_item_card_ids"] = ["magnet", "magnet"]
	attachment_cards[0] = attachment_card
	hostile_cases.append({"name": "snapshot attachment array exceeds maximum", "payload": bad_attachment})
	var bad_table_owner := matrix_snapshot.duplicate(true)
	var table_cards := bad_table_owner["table_cards"] as Array
	var invalid_owner := (table_cards[0] as Dictionary).duplicate(true)
	invalid_owner["card_owner_player_index"] = 2
	table_cards[0] = invalid_owner
	hostile_cases.append({"name": "snapshot table owner index exceeds seats", "payload": bad_table_owner})
	var bad_enum := matrix_snapshot.duplicate(true)
	bad_enum["pending_selection_mode"] = "not-a-pending-mode"
	hostile_cases.append({"name": "snapshot unknown enum", "payload": bad_enum})
	var bad_seat := matrix_snapshot.duplicate(true)
	var bad_seats := bad_seat["seat_control_states"] as Array
	var first_seat := (bad_seats[0] as Dictionary).duplicate(true)
	first_seat["seat_index"] = 9223372036854775807
	bad_seats[0] = first_seat
	hostile_cases.append({"name": "snapshot seat int64 overflow", "payload": bad_seat})

	for test_case in hostile_cases:
		if not _assert_snapshot_rejected(board, baseline, test_case["payload"] as Dictionary, str(test_case["name"])):
			return false

	# Decode-only probes prove the exact legal hand and item maxima still remain
	# acceptable; the following one-over maximum cases above prove no clamping.
	var maximum_hand := matrix_snapshot.duplicate(true)
	maximum_hand["hand_counts"] = [8, 0]
	maximum_hand["own_hand_cards"] = too_many_hand_cards.slice(0, 8)
	if bool(board.call("DebugRejectsNetworkSnapshotPayload", maximum_hand)):
		_fail("Rejected exact legal snapshot hand maximum")
		return false
	var maximum_item := matrix_snapshot.duplicate(true)
	maximum_item["item_hand_counts"] = [8, 0]
	maximum_item["own_item_hand_cards"] = too_many_items.slice(0, 8)
	maximum_item["own_hand_cards"] = []
	maximum_item["hand_counts"] = [0, 0]
	if bool(board.call("DebugRejectsNetworkSnapshotPayload", maximum_item)):
		_fail("Rejected exact legal snapshot item maximum")
		return false
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline:
		_fail("Snapshot boundary decode probes changed live state")
		return false
	var restored := valid_snapshot.duplicate(true)
	restored["snapshot_id"] = int(board.call("DebugGetNetworkLastAppliedSnapshotId")) + 1
	if not bool(board.call("DebugApplyNetworkSnapshotPayload", restored)):
		_fail("Could not restore snapshot after nested snapshot boundary matrix")
		return false
	return true


func _assert_visual_boundary(board: Node, baseline: String, payload: Dictionary, label: String) -> bool:
	if bool(board.call("DebugRejectsNetworkVisualEventPayload", payload)):
		_fail("Rejected legal visual event: " + label)
		return false
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline:
		_fail("Legal visual decode mutated state: " + label)
		return false
	return true


func _assert_visual_rejected(board: Node, baseline: String, payload: Dictionary, label: String) -> bool:
	if not bool(board.call("DebugRejectsNetworkVisualEventPayload", payload)):
		_fail("Accepted hostile visual event: " + label)
		return false
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline:
		_fail("Hostile visual event mutated state: " + label)
		return false
	return true


func _assert_snapshot_rejected(board: Node, baseline: String, payload: Dictionary, label: String) -> bool:
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", payload)):
		_fail("Accepted hostile snapshot: " + label)
		return false
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline:
		_fail("Hostile snapshot mutated state: " + label)
		return false
	return true


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("NETWORK_SNAPSHOT_BOUNDS_SMOKE_FAIL %s" % message)
	quit(1)
