extends SceneTree

const SCENE_PATH := "res://scenes/game_board_scene.tscn"
const LEFT_PLAYER_INDEX := 2
const VIEWPORT_SIZE := Vector2i(616, 888)
const LEFT_PILE_PATH := "BoardCanvas/LayoutAreas/Player3CardPileArea/Player3CardPile"
const CARD_VISUAL_SIZE := Vector2(84.0, 112.0)
const MAX_ALIGNMENT_ERROR_PIXELS := 1.5

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

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	board.set("player_count", 3)
	board.set("item_mode", false)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", false)
	root.add_child(board)
	await process_frame
	await process_frame

	var deadline := Time.get_ticks_msec() / 1000.0 + 8.0
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if String(board.call("DebugGetGameLoopPhaseName")) != "OpeningDeal":
			break

	var pile_card := _get_visible_pile_card(board)
	if pile_card == null:
		_fail("Left visible pile card not found")
		return

	var flight_target := board.call("DebugGetCardPileCaptureTargetGlobalPosition", LEFT_PLAYER_INDEX) as Vector2
	var delta_x := absf(pile_card.global_position.x - flight_target.x)
	var delta_y := absf(pile_card.global_position.y - flight_target.y)
	if delta_x > MAX_ALIGNMENT_ERROR_PIXELS or delta_y > MAX_ALIGNMENT_ERROR_PIXELS:
		_fail("Capture flight end target misaligned with left pile: pile=(%.2f,%.2f) flight=(%.2f,%.2f)" % [
			pile_card.global_position.x,
			pile_card.global_position.y,
			flight_target.x,
			flight_target.y,
		])
		return

	print("LEFT_CAPTURE_PILE_ALIGNMENT_REGRESSION_OK")
	quit(0)


func _get_visible_pile_card(board: Node) -> Control:
	var pile := board.get_node_or_null(LEFT_PILE_PATH)
	if pile == null:
		return null
	for index in range(3, -1, -1):
		var card := pile.get_node_or_null("PileCard_%d" % index) as Control
		if card != null and card.visible:
			return card
	return null


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("LEFT_CAPTURE_PILE_ALIGNMENT_REGRESSION_FAIL %s" % message)
	quit(1)
