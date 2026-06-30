extends SceneTree

const SCENE_PATH := "res://scenes/upgrade_choice_popup_asset_scene.tscn"
const HEARTS_ROW_PATH := "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow"
const SPADES_ROW_PATH := "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow"
const DIAMONDS_ROW_PATH := "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow"
const CLUBS_ROW_PATH := "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % SCENE_PATH)
		return

	var node := packed_scene.instantiate()
	if node == null:
		_fail("Failed to instantiate scene: %s" % SCENE_PATH)
		return

	root.add_child(node)
	await process_frame
	await process_frame

	var hearts_row := node.get_node_or_null(HEARTS_ROW_PATH)
	if hearts_row == null:
		_fail("Missing hearts row: %s" % HEARTS_ROW_PATH)
		return

	if hearts_row.get_child_count() != 3:
		_fail("Expected 3 hearts preview cards, got %d" % hearts_row.get_child_count())
		return

	var spades_row := node.get_node_or_null(SPADES_ROW_PATH)
	if spades_row == null:
		_fail("Missing spades row: %s" % SPADES_ROW_PATH)
		return

	if spades_row.get_child_count() != 3:
		_fail("Expected 3 spades preview cards, got %d" % spades_row.get_child_count())
		return

	_validate_preview_row(hearts_row, "hearts")
	if _failed:
		return
	_validate_preview_row(spades_row, "spades")
	if _failed:
		return

	var diamonds_row := node.get_node_or_null(DIAMONDS_ROW_PATH)
	if diamonds_row == null:
		_fail("Missing diamonds row: %s" % DIAMONDS_ROW_PATH)
		return

	if diamonds_row.get_child_count() != 3:
		_fail("Expected 3 diamonds preview cards, got %d" % diamonds_row.get_child_count())
		return

	_validate_preview_row(diamonds_row, "diamonds")
	if _failed:
		return

	var clubs_row := node.get_node_or_null(CLUBS_ROW_PATH)
	if clubs_row == null:
		_fail("Missing clubs row: %s" % CLUBS_ROW_PATH)
		return

	if clubs_row.get_child_count() != 3:
		_fail("Expected 3 clubs preview cards, got %d" % clubs_row.get_child_count())
		return

	_validate_preview_row(clubs_row, "clubs")
	if _failed:
		return

	print("UPGRADE_CHOICE_POPUP_ASSET_SCENE_SMOKE_OK")
	quit(0)


func _validate_preview_row(row: Node, label: String) -> void:
	for child_index in range(row.get_child_count()):
		var texture_rect := row.get_child(child_index) as TextureRect
		if texture_rect == null:
			_fail("%s preview child %d is not a TextureRect" % [label, child_index])
			return
		if texture_rect.texture == null:
			_fail("%s preview child %d is missing texture" % [label, child_index])
			return
		if texture_rect.material != null:
			_fail("%s preview child %d still uses a chroma-key material" % [label, child_index])
			return

		var image := texture_rect.texture.get_image()
		if image == null:
			_fail("%s preview child %d texture has no image data" % [label, child_index])
			return
		if image.get_pixel(0, 0).a > 0.0:
			_fail("%s preview child %d texture background is not transparent" % [label, child_index])
			return


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("UPGRADE_CHOICE_POPUP_ASSET_SCENE_SMOKE_FAIL %s" % message)
	quit(1)
