@tool
extends Control

const VISUAL_CATALOG_PATH := "res://resources/adventure_upgrades/visuals/adventure_upgrade_card_visual_catalog.tres"
const PREVIEW_BINDINGS := [
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/CommonCard",
		"suit": "hearts",
		"tier": "common",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/UncommonCard",
		"suit": "hearts",
		"tier": "uncommon",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/HeartsGroup/HeartsRow/RareCard",
		"suit": "hearts",
		"tier": "rare",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/CommonCard",
		"suit": "spades",
		"tier": "common",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/UncommonCard",
		"suit": "spades",
		"tier": "uncommon",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/SpadesGroup/SpadesRow/RareCard",
		"suit": "spades",
		"tier": "rare",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/CommonCard",
		"suit": "diamonds",
		"tier": "common",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/UncommonCard",
		"suit": "diamonds",
		"tier": "uncommon",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/DiamondsGroup/DiamondsRow/RareCard",
		"suit": "diamonds",
		"tier": "rare",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/CommonCard",
		"suit": "clubs",
		"tier": "common",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/UncommonCard",
		"suit": "clubs",
		"tier": "uncommon",
	},
	{
		"node_path": "Margin/Layout/PreviewPanel/PreviewMargin/PreviewLayout/SuitGroups/ClubsGroup/ClubsRow/RareCard",
		"suit": "clubs",
		"tier": "rare",
	},
]

var _catalog: Resource
var _entry_by_key := {}


func _ready() -> void:
	_apply_preview_textures()


func _apply_preview_textures() -> void:
	if not _load_catalog():
		return

	for binding in PREVIEW_BINDINGS:
		var texture_rect := get_node_or_null(String(binding["node_path"])) as TextureRect
		if texture_rect == null:
			continue

		var entry := _find_entry(String(binding["suit"]), String(binding["tier"]))
		if entry == null:
			push_warning("Missing popup preview catalog entry for %s/%s" % [binding["suit"], binding["tier"]])
			continue

		var atlas_texture := _create_atlas_texture(entry)
		if atlas_texture != null:
			texture_rect.texture = atlas_texture


func _load_catalog() -> bool:
	if _catalog != null:
		return true

	_catalog = load(VISUAL_CATALOG_PATH)
	if _catalog == null:
		push_warning("Failed to load popup preview visual catalog: %s" % VISUAL_CATALOG_PATH)
		return false

	_entry_by_key.clear()
	var entries = _catalog.get("Entries")
	if entries == null:
		push_warning("Popup preview visual catalog has no Entries array")
		return false

	for entry in entries:
		if entry == null:
			continue
		var suit := String(entry.get("Suit")).strip_edges().to_lower()
		var tier := String(entry.get("Tier")).strip_edges().to_lower()
		_entry_by_key["%s::%s" % [suit, tier]] = entry

	return true


func _find_entry(suit: String, tier: String) -> Resource:
	return _entry_by_key.get("%s::%s" % [suit.strip_edges().to_lower(), tier.strip_edges().to_lower()])


func _create_atlas_texture(entry: Resource) -> AtlasTexture:
	var texture_path := String(entry.get("TexturePath"))
	var source_texture := load(texture_path) as Texture2D
	if source_texture == null:
		push_warning("Failed to load popup preview texture: %s" % texture_path)
		return null

	var atlas_texture := AtlasTexture.new()
	atlas_texture.atlas = source_texture
	atlas_texture.region = entry.get("Region") as Rect2
	return atlas_texture
