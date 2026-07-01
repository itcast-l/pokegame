extends SceneTree

const SCENE_PATH := "res://scenes/upgrade_choice_popup_asset_scene.tscn"
const TEXTURE_PATHS := [
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/CommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/UncommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/RareCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/CommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/UncommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/RareCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/CommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/UncommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/RareCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/CommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/UncommonCard",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/RareCard",
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
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

	var preview_nodes: Array[TextureRect] = []
	for texture_path in TEXTURE_PATHS:
		var texture_rect := instance.get_node_or_null(texture_path) as TextureRect
		if texture_rect == null:
			_fail("Missing preview texture node: %s" % texture_path)
			return
		preview_nodes.append(texture_rect)

	if preview_nodes.size() != 12:
		_fail("Expected 12 preview texture nodes, got %d" % preview_nodes.size())
		return

	for texture_rect in preview_nodes:
		if texture_rect.texture == null:
			_fail("Preview texture is null: %s" % texture_rect.get_path())
			return
		if not (texture_rect.texture is Texture2D):
			_fail("Preview texture is not Texture2D: %s" % texture_rect.get_path())
			return

	print("UPGRADE_CHOICE_POPUP_ASSET_SCENE_VISUAL_AUDIT_OK")
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("UPGRADE_CHOICE_POPUP_ASSET_SCENE_VISUAL_AUDIT_FAIL %s" % message)
	quit(1)
