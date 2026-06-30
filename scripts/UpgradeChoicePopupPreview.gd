@tool
extends Control

const HEARTS_TEXTURES := {
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/CommonCard": "res://generated-images/upgrade-popup-assets/hearts/processed/hearts-common.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/UncommonCard": "res://generated-images/upgrade-popup-assets/hearts/processed/hearts-uncommon.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/RareCard": "res://generated-images/upgrade-popup-assets/hearts/processed/hearts-rare.png",
}

const SPADES_TEXTURES := {
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/CommonCard": "res://generated-images/upgrade-popup-assets/spades/processed/spades-common.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/UncommonCard": "res://generated-images/upgrade-popup-assets/spades/processed/spades-uncommon.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/RareCard": "res://generated-images/upgrade-popup-assets/spades/processed/spades-rare.png",
}

const DIAMONDS_TEXTURES := {
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/CommonCard": "res://generated-images/upgrade-popup-assets/diamonds/processed/diamonds-common.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/UncommonCard": "res://generated-images/upgrade-popup-assets/diamonds/processed/diamonds-uncommon.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/RareCard": "res://generated-images/upgrade-popup-assets/diamonds/processed/diamonds-rare.png",
}

const CLUBS_TEXTURES := {
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/CommonCard": "res://generated-images/upgrade-popup-assets/clubs/processed/clubs-common.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/UncommonCard": "res://generated-images/upgrade-popup-assets/clubs/processed/clubs-uncommon.png",
	"Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/RareCard": "res://generated-images/upgrade-popup-assets/clubs/processed/clubs-rare.png",
}


func _ready() -> void:
	_apply_texture_group(HEARTS_TEXTURES)
	_apply_texture_group(SPADES_TEXTURES)
	_apply_texture_group(DIAMONDS_TEXTURES)
	_apply_texture_group(CLUBS_TEXTURES)


func _apply_texture_group(texture_map: Dictionary) -> void:
	for node_path in texture_map.keys():
		var texture_rect := get_node_or_null(node_path) as TextureRect
		if texture_rect == null:
			continue

		var texture := _load_texture_from_png(String(texture_map[node_path]))
		if texture != null:
			texture_rect.texture = texture


func _load_texture_from_png(resource_path: String) -> Texture2D:
	var image := Image.new()
	var error := image.load(ProjectSettings.globalize_path(resource_path))
	if error != OK:
		push_warning("Failed to load popup preview texture: %s" % resource_path)
		return null

	return ImageTexture.create_from_image(image)
