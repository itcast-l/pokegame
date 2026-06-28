extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const RECORDING_SETUP_ID := "jellyfish_capture_showcase_01"
const TIMEOUT_SECONDS := 10.0
const EXPECTED_TABLE_COUNT := 10
const EXPECTED_CAPTURE_COUNT := 11

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", false)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "jellyfish")
	board.set("debug_recording_setup_id", RECORDING_SETUP_ID)

	root.add_child(board)
	await process_frame
	await process_frame

	if not board.has_method("DebugGetTableCardCount") \
		or not board.has_method("DebugGetTableCardCode") \
		or not board.has_method("DebugIsTableCardFaceUp") \
		or not board.has_method("DebugGetHandCardCode") \
		or not board.has_method("DebugGetDeckCardCountForPlayer") \
		or not board.has_method("DebugGetDeckCardCode") \
		or not board.has_method("DebugGetPotentialCaptureCountForHandCard"):
		_fail("Missing recording setup debug hooks")
		return

	if not await _wait_for_actionable_state(board):
		return

	if String(board.get("adventure_enemy_avatar_id")) != "jellyfish":
		_fail("Expected jellyfish enemy avatar")
		return

	if String(board.get("debug_recording_setup_id")) != RECORDING_SETUP_ID:
		_fail("Recording setup id was not applied")
		return

	if int(board.call("DebugGetTableCardCount")) != EXPECTED_TABLE_COUNT:
		_fail("Expected %d table cards" % EXPECTED_TABLE_COUNT)
		return

	var seen_codes := {}
	for card_index in range(EXPECTED_TABLE_COUNT):
		var card_code := String(board.call("DebugGetTableCardCode", card_index))
		if card_code.is_empty():
			_fail("Missing table card code at index %d" % card_index)
			return

		var rank := card_code.get_slice(":", 1)
		if rank == "J" or rank == "Q" or rank == "K":
			_fail("Excluded rank %s should not appear in item-mode recording setup" % rank)
			return

		if seen_codes.has(card_code):
			_fail("Duplicate table card code found: %s" % card_code)
			return

		seen_codes[card_code] = true

		var expected_face_up := card_index % 2 == 0
		var actual_face_up := bool(board.call("DebugIsTableCardFaceUp", card_index))
		if actual_face_up != expected_face_up:
			_fail("Unexpected face state at table index %d" % card_index)
			return

	if int(board.call("DebugGetTotalHandCardCountForPlayer", 0)) != 1:
		_fail("Expected the main player to start with exactly one hand card")
		return

	var main_hand_code := String(board.call("DebugGetHandCardCode", 0, 0))
	if main_hand_code.is_empty():
		_fail("Main player hand card code missing")
		return

	var first_table_code := String(board.call("DebugGetTableCardCode", 0))
	if first_table_code.is_empty():
		_fail("First table card code missing")
		return

	var main_hand_rank := main_hand_code.get_slice(":", 1)
	var first_table_rank := first_table_code.get_slice(":", 1)
	if main_hand_rank != first_table_rank:
		_fail(
			"Recording setup must use real full-capture logic: hand rank %s should match first table rank %s"
			% [main_hand_rank, first_table_rank])
		return

	if int(board.call("DebugGetPotentialCaptureCountForHandCard", 0, 0)) != EXPECTED_CAPTURE_COUNT:
		_fail("Expected a %d-card capture from the main player hand card" % EXPECTED_CAPTURE_COUNT)
		return

	if not bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
		_fail("Failed to play the recording setup hand card")
		return

	if not await _wait_for_table_card_count(board, 0):
		return

	for player_index in [0, 1]:
		var hand_count := int(board.call("DebugGetTotalHandCardCountForPlayer", player_index))
		for hand_index in range(hand_count):
			var hand_code := String(board.call("DebugGetHandCardCode", player_index, hand_index))
			if _has_excluded_rank(hand_code):
				_fail("Excluded rank should not appear in recording hand: %s" % hand_code)
				return

		var deck_count := int(board.call("DebugGetDeckCardCountForPlayer", player_index))
		for deck_index in range(deck_count):
			var deck_code := String(board.call("DebugGetDeckCardCode", player_index, deck_index))
			if _has_excluded_rank(deck_code):
				_fail("Excluded rank should not appear in recording deck: %s" % deck_code)
				return

	board.queue_free()
	await process_frame
	print("JELLYFISH_RECORDING_SETUP_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly")
			return false

		if not board.has_method("DebugGetGameLoopPhaseName"):
			_fail("Missing phase debug hook")
			return false

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		if phase == "AwaitingMainPlayerAction":
			return true

	_fail("Timed out waiting for actionable game state")
	return false


func _wait_for_table_card_count(board: Node, expected_count: int) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for capture resolution")
			return false

		if int(board.call("DebugGetTableCardCount")) == expected_count:
			return true

	_fail("Timed out waiting for table card count %d" % expected_count)
	return false


func _has_excluded_rank(card_code: String) -> bool:
	if card_code.is_empty():
		return false

	var rank := card_code.get_slice(":", 1)
	return rank == "J" or rank == "Q" or rank == "K"


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("JELLYFISH_RECORDING_SETUP_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
