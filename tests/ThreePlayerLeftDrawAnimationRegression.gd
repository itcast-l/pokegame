extends SceneTree

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const TIMEOUT_SECONDS := 12.0
const LEFT_PLAYER_INDEX := 2
const MAX_HIDDEN_HAND_SETTLE_SCALE := 1.02
const MAX_POSITION_OVERSHOOT_PIXELS := 1.5
const LEFT_PLAYER_HAND_AREA_PATH := "BoardCanvas/LayoutAreas/Player3HandArea"
const LEFT_PILE_PATH := "BoardCanvas/LayoutAreas/Player3CardPileArea/Player3CardPile"
const VIEWPORT_SIZE := Vector2i(616, 888)

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	root.size = VIEWPORT_SIZE

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var board := GameBoardSmokeFactory.create_solo(packed_scene, 3, false)
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	root.add_child(board)
	await process_frame

	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	var played_main_turn := false
	var saw_left_turn := false
	var sampled_left_draw := false
	var start_x := INF
	var start_y := INF
	var target_x := INF
	var target_y := INF

	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly")
			return

		var phase := String(board.call("DebugGetGameLoopPhaseName"))
		var current_turn := int(board.call("DebugGetCurrentTurnPlayerIndex"))

		if not played_main_turn:
			if phase == "OpeningDeal":
				continue

			if bool(board.call("DebugPlayFirstAvailableLocalMainPlayerHandCard")):
				played_main_turn = true
			continue

		if current_turn == LEFT_PLAYER_INDEX:
			saw_left_turn = true

		var flight := board.get_node_or_null("ReplacementDrawCardFlight") as Control
		if saw_left_turn and flight != null:
			sampled_left_draw = true
			if is_inf(start_x):
				var pile_card := _get_visible_pile_card(board)
				var hand_visual := _get_last_hand_card_visual(board)
				if pile_card == null or hand_visual == null:
					_fail("Missing pile or hand visual during left draw")
					return
				start_x = pile_card.global_position.x
				start_y = pile_card.global_position.y
				target_x = hand_visual.global_position.x
				target_y = hand_visual.global_position.y
			if flight.global_position.x < minf(start_x, target_x) - MAX_POSITION_OVERSHOOT_PIXELS \
				or flight.global_position.x > maxf(start_x, target_x) + MAX_POSITION_OVERSHOOT_PIXELS:
				_fail("Left draw flight x escaped segment: start=%.3f target=%.3f actual=%.3f" % [start_x, target_x, flight.global_position.x])
				return
			if flight.global_position.y < minf(start_y, target_y) - MAX_POSITION_OVERSHOOT_PIXELS \
				or flight.global_position.y > maxf(start_y, target_y) + MAX_POSITION_OVERSHOOT_PIXELS:
				_fail("Left draw flight y escaped segment: start=%.3f target=%.3f actual=%.3f" % [start_y, target_y, flight.global_position.y])
				return

		if saw_left_turn and sampled_left_draw and flight == null and current_turn != LEFT_PLAYER_INDEX:
			board.queue_free()
			await process_frame
			print("THREE_PLAYER_LEFT_DRAW_ANIMATION_REGRESSION_OK")
			await _dispose_audio_before_quit()
			quit(0)
			return

		if saw_left_turn and sampled_left_draw and flight == null:
			var hidden_hand_scale := _get_max_hand_visual_scale(board)
			if hidden_hand_scale > MAX_HIDDEN_HAND_SETTLE_SCALE:
				_fail("Left hidden hand settle scale exceeded limit: %.3f" % hidden_hand_scale)
				return

	_fail("Timed out waiting for left-player replacement draw animation")


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("THREE_PLAYER_LEFT_DRAW_ANIMATION_REGRESSION_FAIL %s" % message)
	quit(1)


func _get_visible_pile_card(board: Node) -> Control:
	var pile := board.get_node_or_null(LEFT_PILE_PATH)
	if pile == null:
		return null
	for index in range(3, -1, -1):
		var card := pile.get_node_or_null("PileCard_%d" % index) as Control
		if card != null and card.visible:
			return card
	return null


func _get_last_hand_card_visual(board: Node) -> Control:
	var hand_area := board.get_node_or_null(LEFT_PLAYER_HAND_AREA_PATH) as Control
	if hand_area == null:
		return null

	var last_index := -1
	var last_slot: Control = null
	for child in hand_area.get_children():
		if child is not Control:
			continue
		var name := String(child.name)
		if not name.begins_with("HandCard_"):
			continue
		var index := int(name.trim_prefix("HandCard_"))
		if index > last_index:
			last_index = index
			last_slot = child
	if last_slot == null:
		return null

	for grandchild in last_slot.get_children():
		if grandchild is TextureRect:
			return grandchild
	return last_slot


func _get_max_hand_visual_scale(board: Node) -> float:
	var hand_area := board.get_node_or_null(LEFT_PLAYER_HAND_AREA_PATH) as Control
	if hand_area == null:
		return 1.0

	var max_scale := 1.0
	for child in hand_area.get_children():
		if child is not Control:
			continue
		if not String(child.name).begins_with("HandCard_"):
			continue
		for grandchild in child.get_children():
			if grandchild is TextureRect:
				var scale_value := maxf(absf(grandchild.scale.x), absf(grandchild.scale.y))
				max_scale = maxf(max_scale, scale_value)
				break
	return max_scale


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
