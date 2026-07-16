extends SceneTree

const SCENE_PATH := "res://scenes/upgrade_choice_popup_asset_scene.tscn"
const CARD_PATHS := [
	"Margin/Layout/SuitGroups/HeartsGroup/HeartsRow/CommonCard",
	"Margin/Layout/SuitGroups/HeartsGroup/HeartsRow/UncommonCard",
	"Margin/Layout/SuitGroups/HeartsGroup/HeartsRow/RareCard",
	"Margin/Layout/SuitGroups/SpadesGroup/SpadesRow/CommonCard",
	"Margin/Layout/SuitGroups/SpadesGroup/SpadesRow/UncommonCard",
	"Margin/Layout/SuitGroups/SpadesGroup/SpadesRow/RareCard",
	"Margin/Layout/SuitGroups/DiamondsGroup/DiamondsRow/CommonCard",
	"Margin/Layout/SuitGroups/DiamondsGroup/DiamondsRow/UncommonCard",
	"Margin/Layout/SuitGroups/DiamondsGroup/DiamondsRow/RareCard",
	"Margin/Layout/SuitGroups/ClubsGroup/ClubsRow/CommonCard",
	"Margin/Layout/SuitGroups/ClubsGroup/ClubsRow/UncommonCard",
	"Margin/Layout/SuitGroups/ClubsGroup/ClubsRow/RareCard",
]
const VIEWPORT_SIZE := Vector2i(1920, 1080)
const EDGE_PADDING_PIXELS := 4

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = VIEWPORT_SIZE
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load popup asset scene")
		return

	var instance := packed_scene.instantiate()
	if instance == null:
		_fail("Failed to instantiate popup asset scene")
		return

	root.add_child(instance)
	await process_frame
	await process_frame

	var preview_cards: Array[Control] = []
	for card_path in CARD_PATHS:
		var card := instance.get_node_or_null(card_path) as Control
		if card == null or not card.has_method("DebugGetCardState"):
			_fail("Missing shared preview card: %s" % card_path)
			return
		preview_cards.append(card)

	if preview_cards.size() != 12:
		_fail("Expected 12 shared preview cards, got %d" % preview_cards.size())
		return

	for card in preview_cards:
		var texture_rect := card.get_node("CardTexture") as TextureRect
		if texture_rect.texture == null:
			_fail("Preview texture is null: %s" % texture_rect.get_path())
			return
		if not (texture_rect.texture is Texture2D):
			_fail("Preview texture is not Texture2D: %s" % texture_rect.get_path())
			return
		if card.scene_file_path != "res://scenes/adventure_upgrade_catalog_card.tscn":
			_fail("Preview card is not a shared scene instance: %s" % card.get_path())
			return
		if not _rect_inside_viewport(card.get_global_rect(), root.size):
			_fail("Preview card is clipped by viewport: %s rect=%s viewport=%s" % [
				card.get_path(),
				str(card.get_global_rect()),
				str(root.size),
			])
			return
		if _texture_touches_crop_edge(texture_rect.texture.get_image()):
			_fail("Preview texture touches atlas crop edge: %s" % texture_rect.get_path())
			return

	print("UPGRADE_CHOICE_POPUP_ASSET_SCENE_VISUAL_AUDIT_OK")
	quit(0)


func _rect_inside_viewport(rect: Rect2, viewport_size: Vector2i) -> bool:
	return (
		rect.position.x >= 0.0
		and rect.position.y >= 0.0
		and rect.end.x <= float(viewport_size.x)
		and rect.end.y <= float(viewport_size.y)
	)


func _texture_touches_crop_edge(image: Image) -> bool:
	if image == null:
		return true

	var width := image.get_width()
	var height := image.get_height()
	for y in range(height):
		for x in range(EDGE_PADDING_PIXELS):
			if image.get_pixel(x, y).a > 0.0:
				return true
			if image.get_pixel(width - 1 - x, y).a > 0.0:
				return true

	for x in range(width):
		for y in range(EDGE_PADDING_PIXELS):
			if image.get_pixel(x, y).a > 0.0:
				return true
			if image.get_pixel(x, height - 1 - y).a > 0.0:
				return true

	return false


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("UPGRADE_CHOICE_POPUP_ASSET_SCENE_VISUAL_AUDIT_FAIL %s" % message)
	quit(1)
