extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"

var _failed := false
var _visual_event_context: Dictionary = {}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed_scene := load(SCENE_PATH) as PackedScene
	if not await _assert_multiplayer_opening_dice_bounds(3):
		return
	if not await _assert_multiplayer_opening_dice_bounds(4):
		return
	var board := GameBoardSmokeFactory.create_local(packed_scene) if packed_scene != null else null
	if board == null:
		_fail("Could not instantiate game board")
		return

	root.add_child(board)
	await process_frame
	var valid_snapshot := board.call("BuildNetworkBoardSnapshotForSeat", 0, 1) as Dictionary
	if valid_snapshot == null:
		_fail("Could not build valid network snapshot baseline")
		return
	if not _assert_snapshot_contract(valid_snapshot, GameBoardSmokeFactory.get_player_count(board), 0):
		return
	for recipient_seat in range(1, GameBoardSmokeFactory.get_player_count(board)):
		var recipient_snapshot := board.call(
			"BuildNetworkBoardSnapshotForSeat",
			recipient_seat,
			recipient_seat + 1) as Dictionary
		if recipient_snapshot == null \
				or not _assert_snapshot_contract(
					recipient_snapshot,
					GameBoardSmokeFactory.get_player_count(board),
					recipient_seat):
			return
	_visual_event_context = {
		"session_id": valid_snapshot["session_id"],
		"match_id": valid_snapshot["match_id"],
		"authority_epoch": valid_snapshot["authority_epoch"],
		"checkpoint_id": valid_snapshot["checkpoint_id"],
		"action_id": 0,
	}
	var seat_states: Array = []
	for seat_index in range(GameBoardSmokeFactory.get_player_count(board)):
		seat_states.append({
			"seat_index": seat_index,
			"mode": "HumanConnected",
			"peer_id": 0,
			"player_session_id": "",
			"credential_generation": 1,
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

	var missing_threshold := valid_snapshot.duplicate(true)
	missing_threshold.erase("base_play_capture_threshold")
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", missing_threshold)):
		_fail("Accepted snapshot without global capture threshold")
		return
	var array_threshold := valid_snapshot.duplicate(true)
	array_threshold["base_play_capture_threshold"] = [0]
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", array_threshold)):
		_fail("Accepted snapshot with capture threshold array")
		return
	var high_threshold := valid_snapshot.duplicate(true)
	high_threshold["base_play_capture_threshold"] = 5
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", high_threshold)):
		_fail("Accepted snapshot with capture threshold above four")
		return
	var low_threshold := valid_snapshot.duplicate(true)
	low_threshold["base_play_capture_threshold"] = -1
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", low_threshold)):
		_fail("Accepted snapshot with negative capture threshold")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Capture threshold boundary probes changed board state")
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
	if not _rejects_visual_event(board, overflow_event):
		_fail("Accepted visual event with overflowing display index")
		return
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
		_fail("Overflow visual event changed board state")
		return

	var negative_event := overflow_event.duplicate()
	negative_event["display_index"] = -1
	if not _rejects_visual_event(board, negative_event):
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
		if _rejects_visual_event(board, legal_draw_boundary_event):
			_fail("Rejected maximum legal draw display index")
			return
		if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
			_fail("Legal draw boundary probe changed board state")
			return

		var maximum_action_id_event := _with_visual_event_context(legal_draw_boundary_event.duplicate(true))
		maximum_action_id_event["action_id"] = 9223372036854775807
		if bool(board.call("DebugRejectsNetworkVisualEventPayload", maximum_action_id_event)):
			_fail("Rejected maximum legal visual event action ID")
			return
		var missing_action_id_event := maximum_action_id_event.duplicate(true)
		missing_action_id_event.erase("action_id")
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", missing_action_id_event)):
			_fail("Accepted visual event without action ID")
			return
		var negative_action_id_event := maximum_action_id_event.duplicate(true)
		negative_action_id_event["action_id"] = -1
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", negative_action_id_event)):
			_fail("Accepted visual event with negative action ID")
			return
		var wrong_type_action_id_event := maximum_action_id_event.duplicate(true)
		wrong_type_action_id_event["action_id"] = "1"
		if not bool(board.call("DebugRejectsNetworkVisualEventPayload", wrong_type_action_id_event)):
			_fail("Accepted visual event with non-integer action ID")
			return
		if str(board.call("DebugGetNetworkStateFingerprint")) != baseline_state:
			_fail("Visual event action ID boundary probes changed board state")
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
		if _rejects_visual_event(board, legal_lens_event):
			_fail("Rejected legal Lens visual event")
			return
		var overflowing_lens_event := legal_lens_event.duplicate(true)
		overflowing_lens_event["target_table_card_indexes"] = [baseline_table_cards.size()]
		if not _rejects_visual_event(board, overflowing_lens_event):
			_fail("Accepted Lens event with out-of-range table index")
			return
		var duplicate_lens_event := legal_lens_event.duplicate(true)
		duplicate_lens_event["target_table_card_indexes"] = [0, 0]
		if not _rejects_visual_event(board, duplicate_lens_event):
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
		if _rejects_visual_event(board, legal_capture_event):
			_fail("Rejected legal Capture visual event")
			return
		var overflowing_capture_event := legal_capture_event.duplicate(true)
		overflowing_capture_event["capture_start_index"] = baseline_table_cards.size()
		if not _rejects_visual_event(board, overflowing_capture_event):
			_fail("Accepted Capture event with out-of-range start index")
			return
		var duplicate_capture_event := legal_capture_event.duplicate(true)
		duplicate_capture_event["extra_table_card_indexes"] = [0, 0]
		if not _rejects_visual_event(board, duplicate_capture_event):
			_fail("Accepted Capture event with duplicate extra indexes")
			return
		var non_finite_capture_event := legal_capture_event.duplicate(true)
		non_finite_capture_event["flight_stagger_seconds"] = INF
		if not _rejects_visual_event(board, non_finite_capture_event):
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
		if _rejects_visual_event(board, delayed_blocker):
			_fail("Delayed queue blocker is not a legal event for %s" % delayed_event_type)
			return
		if _rejects_visual_event(board, delayed_event):
			_fail("Delayed queue target is not a legal event for %s" % delayed_event_type)
			return
		if bool(board.call("DebugRejectsNetworkSnapshotPayload", replacement_snapshot)):
			_fail("Delayed queue replacement snapshot is invalid for %s" % delayed_event_type)
			return
		board.call("DebugSetNetworkVisualPlaybackDelaySeconds", 0.05)
		if not bool(board.call(
			"DebugQueueDelayedNetworkVisualEventWithSnapshotFence",
			_with_visual_event_context(delayed_blocker),
			_with_visual_event_context(delayed_event),
			replacement_snapshot)):
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
		if not bool(board.call(
			"DebugQueueDecodedNetworkVisualEventWithSnapshotFence",
			_with_visual_event_context(fenced_hand_event),
			fenced_reconnect_snapshot)):
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
		if not bool(board.call(
			"DebugPlayDecodedNetworkVisualEventAfterSnapshot",
			_with_visual_event_context(stale_hand_event),
			empty_hand_snapshot)):
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
		if not bool(board.call(
			"DebugPlayDecodedNetworkVisualEventAfterSnapshot",
			_with_visual_event_context(stale_draw_event),
			empty_draw_snapshot)):
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
	if not bool(board.call(
		"DebugPlayDecodedNetworkVisualEventAfterSnapshot",
		_with_visual_event_context(stale_item_event),
		empty_item_snapshot)):
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
		if not bool(board.call(
			"DebugPlayDecodedNetworkVisualEventAfterSnapshot",
			_with_visual_event_context(stale_deck_event),
			empty_deck_snapshot)):
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
	var blocked_threshold_event := {
		"event_type": "capture_threshold_feedback",
		"snapshot_id": event_snapshot_id,
		"turn_sequence_generation": generation,
		"actor_seat_index": 0,
		"capture_threshold_feedback_type": "blocked",
		"capture_threshold_count": 1,
	}
	if not _assert_visual_boundary(board, baseline, blocked_threshold_event, "capture threshold blocked legal"):
		return false
	var big_tide_threshold_event := blocked_threshold_event.duplicate(true)
	big_tide_threshold_event["capture_threshold_feedback_type"] = "big_tide"
	big_tide_threshold_event["capture_threshold_count"] = 256
	if not _assert_visual_boundary(board, baseline, big_tide_threshold_event, "capture threshold Big Tide legal"):
		return false
	var invalid_threshold_type := blocked_threshold_event.duplicate(true)
	invalid_threshold_type["capture_threshold_feedback_type"] = "unknown"
	if not _assert_visual_rejected(board, baseline, invalid_threshold_type, "capture threshold feedback type"):
		return false
	var zero_threshold_count := blocked_threshold_event.duplicate(true)
	zero_threshold_count["capture_threshold_count"] = 0
	if not _assert_visual_rejected(board, baseline, zero_threshold_count, "capture threshold zero count"):
		return false
	var oversized_threshold_count := blocked_threshold_event.duplicate(true)
	oversized_threshold_count["capture_threshold_count"] = 257
	if not _assert_visual_rejected(board, baseline, oversized_threshold_count, "capture threshold oversized count"):
		return false
	var invalid_threshold_seat := blocked_threshold_event.duplicate(true)
	invalid_threshold_seat["actor_seat_index"] = GameBoardSmokeFactory.get_player_count(board)
	if not _assert_visual_rejected(board, baseline, invalid_threshold_seat, "capture threshold invalid seat"):
		return false

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

	for key in ["protocol_version", "session_id", "match_id", "match_outcome_kind", "scores", "base_play_capture_threshold", "deck_counts", "hand_counts", "item_hand_counts", "own_hand_cards", "own_item_hand_cards", "table_cards", "seat_control_states", "pending_selection_mode", "opening_dice_roll_result", "opening_deal_animation_playing", "opening_deal_completed", "authority_action"]:
		var missing := matrix_snapshot.duplicate(true)
		missing.erase(key)
		hostile_cases.append({"name": "snapshot missing " + key, "payload": missing})

	for key in ["snapshot_id", "turn_sequence_generation", "opening_dice_roll_result", "scores", "base_play_capture_threshold", "deck_counts", "hand_counts", "item_hand_counts", "pending_selection_mode", "opening_deal_animation_playing"]:
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
	var bad_two_player_dice := matrix_snapshot.duplicate(true)
	bad_two_player_dice["opening_dice_roll_result"] = 1
	hostile_cases.append({"name": "two-player snapshot has opening dice", "payload": bad_two_player_dice})
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


func _assert_multiplayer_opening_dice_bounds(player_count: int) -> bool:
	var packed_scene := load(SCENE_PATH) as PackedScene
	var board := GameBoardSmokeFactory.create_local(packed_scene) if packed_scene != null else null
	if board == null:
		_fail("Could not instantiate %d-player dice boundary board" % player_count)
		return false
	if not GameBoardSmokeFactory.defer_initialization(board):
		_fail("Could not defer %d-player dice boundary board" % player_count)
		return false
	root.add_child(board)
	await process_frame
	if not bool(board.call("DebugStartOpeningDiceMatch", player_count, 1)):
		_fail("Could not start %d-player dice boundary board" % player_count)
		return false
	var snapshot := board.call("BuildNetworkBoardSnapshotForSeat", 0, 1) as Dictionary
	if snapshot == null:
		_fail("Could not build %d-player dice boundary snapshot" % player_count)
		return false
	var seat_states: Array = []
	for seat_index in range(player_count):
		seat_states.append({
			"seat_index": seat_index,
			"mode": "HumanConnected",
			"peer_id": 0,
			"player_session_id": "",
			"credential_generation": 1,
		})
	snapshot["seat_control_states"] = seat_states
	for legal_result in [1, 6]:
		var legal := snapshot.duplicate(true)
		legal["opening_dice_roll_result"] = legal_result
		if bool(board.call("DebugRejectsNetworkSnapshotPayload", legal)):
			_fail("Rejected legal %d-player opening dice result %d" % [player_count, legal_result])
			return false
	var hostile_cases := [
		{"name": "missing", "value": null},
		{"name": "zero", "value": 0},
		{"name": "seven", "value": 7},
	]
	for test_case in hostile_cases:
		var hostile := snapshot.duplicate(true)
		if test_case["value"] == null:
			hostile.erase("opening_dice_roll_result")
		else:
			hostile["opening_dice_roll_result"] = test_case["value"]
		if not bool(board.call("DebugRejectsNetworkSnapshotPayload", hostile)):
			_fail("Accepted %d-player %s opening dice result" % [player_count, test_case["name"]])
			return false
	board.queue_free()
	await process_frame
	return true


func _assert_visual_boundary(board: Node, baseline: String, payload: Dictionary, label: String) -> bool:
	if _rejects_visual_event(board, payload):
		_fail("Rejected legal visual event: " + label)
		return false
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline:
		_fail("Legal visual decode mutated state: " + label)
		return false
	return true


func _assert_visual_rejected(board: Node, baseline: String, payload: Dictionary, label: String) -> bool:
	if not _rejects_visual_event(board, payload):
		_fail("Accepted hostile visual event: " + label)
		return false
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline:
		_fail("Hostile visual event mutated state: " + label)
		return false
	return true


func _rejects_visual_event(board: Node, payload: Dictionary) -> bool:
	return bool(board.call(
		"DebugRejectsNetworkVisualEventPayload",
		_with_visual_event_context(payload)))


func _with_visual_event_context(payload: Dictionary) -> Dictionary:
	for key in _visual_event_context:
		payload[key] = _visual_event_context[key]
	return payload


func _assert_snapshot_rejected(board: Node, baseline: String, payload: Dictionary, label: String) -> bool:
	if not bool(board.call("DebugRejectsNetworkSnapshotPayload", payload)):
		_fail("Accepted hostile snapshot: " + label)
		return false
	if str(board.call("DebugGetNetworkStateFingerprint")) != baseline:
		_fail("Hostile snapshot mutated state: " + label)
		return false
	return true


func _assert_snapshot_contract(snapshot: Dictionary, player_count: int, recipient_seat: int) -> bool:
	var expected_keys := [
		"protocol_version",
		"session_id",
		"match_id",
		"authority_epoch",
		"checkpoint_id",
		"snapshot_id",
		"turn_sequence_generation",
		"current_turn_player_index",
		"opening_dice_roll_result",
		"match_outcome_kind",
		"winner_player_index",
		"scores",
		"base_play_capture_threshold",
		"deck_counts",
		"hand_counts",
		"item_hand_counts",
		"own_item_hand_cards",
		"table_cards",
		"own_hand_cards",
		"pending_selection_mode",
		"pending_selection_owner",
		"pending_item_card_id",
		"pending_turn_sequence_generation",
		"opening_deal_animation_playing",
		"opening_deal_completed",
		"seat_control_states",
		"authority_action",
	]
	if snapshot.size() != expected_keys.size():
		_fail("Network snapshot key count changed for seat %d" % recipient_seat)
		return false
	for key in expected_keys:
		if not snapshot.has(key):
			_fail("Network snapshot omitted key %s for seat %d" % [key, recipient_seat])
			return false

	var integer_keys := [
		"protocol_version",
		"authority_epoch",
		"checkpoint_id",
		"snapshot_id",
		"turn_sequence_generation",
		"current_turn_player_index",
		"opening_dice_roll_result",
		"winner_player_index",
		"base_play_capture_threshold",
		"pending_selection_owner",
		"pending_turn_sequence_generation",
	]
	for key in integer_keys:
		if typeof(snapshot[key]) != TYPE_INT:
			_fail("Network snapshot key %s changed integer type" % key)
			return false
	for key in ["session_id", "match_id", "match_outcome_kind", "pending_selection_mode", "pending_item_card_id"]:
		if typeof(snapshot[key]) != TYPE_STRING:
			_fail("Network snapshot key %s changed string type" % key)
			return false
	for key in ["scores", "deck_counts", "hand_counts", "item_hand_counts", "own_item_hand_cards", "table_cards", "own_hand_cards", "seat_control_states"]:
		if typeof(snapshot[key]) != TYPE_ARRAY:
			_fail("Network snapshot key %s changed array type" % key)
			return false
	for key in ["opening_deal_animation_playing", "opening_deal_completed"]:
		if typeof(snapshot[key]) != TYPE_BOOL:
			_fail("Network snapshot key %s changed boolean type" % key)
			return false
	if typeof(snapshot["authority_action"]) != TYPE_DICTIONARY:
		_fail("Network snapshot authority action changed dictionary type")
		return false

	var hand_counts := snapshot["hand_counts"] as Array
	var item_counts := snapshot["item_hand_counts"] as Array
	var own_hand := snapshot["own_hand_cards"] as Array
	var own_items := snapshot["own_item_hand_cards"] as Array
	if hand_counts.size() != player_count \
			or item_counts.size() != player_count \
			or own_hand.size() != int(hand_counts[recipient_seat]) \
			or own_items.size() != int(item_counts[recipient_seat]):
		_fail("Network snapshot own-hand privacy shape changed for seat %d" % recipient_seat)
		return false
	var capture_threshold := int(snapshot["base_play_capture_threshold"])
	if capture_threshold < 0 or capture_threshold > 4:
		_fail("Network snapshot capture threshold value changed contract")
		return false
	for raw_card in own_hand:
		if typeof(raw_card) != TYPE_DICTIONARY \
				or typeof((raw_card as Dictionary).get("code")) != TYPE_STRING \
				or typeof((raw_card as Dictionary).get("attached_item_card_ids")) != TYPE_ARRAY:
			_fail("Network snapshot own hand card value types changed")
			return false
	for raw_item in own_items:
		if typeof(raw_item) != TYPE_DICTIONARY \
				or typeof((raw_item as Dictionary).get("id")) != TYPE_STRING:
			_fail("Network snapshot own item card value types changed")
			return false

	for raw_table_card in snapshot["table_cards"] as Array:
		if typeof(raw_table_card) != TYPE_DICTIONARY:
			_fail("Network snapshot table card changed dictionary type")
			return false
		var table_card := raw_table_card as Dictionary
		if not bool(table_card.get("is_face_up", false)) \
				and int(table_card.get("card_owner_player_index", -1)) != recipient_seat \
				and (not String(table_card.get("code", "")).is_empty() \
					or not (table_card.get("attached_item_card_ids", []) as Array).is_empty()):
			_fail("Network snapshot exposed a hidden table card to seat %d" % recipient_seat)
			return false
	return true


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("NETWORK_SNAPSHOT_BOUNDS_SMOKE_FAIL %s" % message)
	quit(1)
