extends SceneTree

const AVATAR_SCENE_PATH := "res://scenes/character_avatar.tscn"
const AVATAR_ROOT := "res://art/characters/player_dragon_queen_avatar/run-002"
const FRAME_COUNT := 12
const FRAME_SIZE := Vector2i(128, 128)
const ACTIONS := ["idle", "score", "attack", "hit", "victory", "defeat"]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	if not _validate_frame_files():
		return

	var packed_scene := load(AVATAR_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load avatar scene: %s" % AVATAR_SCENE_PATH)
		return

	var avatar := packed_scene.instantiate()
	avatar.set("AvatarRoot", AVATAR_ROOT)
	avatar.set("PortraitTexture", load(_frame_path("idle", 1)))
	avatar.set("LoadActionFrames", true)
	root.add_child(avatar)
	await process_frame

	var portrait := avatar.get_node_or_null("PortraitPivot/Portrait") as TextureRect
	if portrait == null:
		_fail("Avatar portrait node is missing")
		return

	for action in ACTIONS:
		avatar.call("PlayActionByName", action)
		await process_frame
		var actual_path := portrait.texture.resource_path if portrait.texture != null else ""
		var expected_prefix := "%s/%s/processed/%s-" % [AVATAR_ROOT, action, action]
		if not actual_path.begins_with(expected_prefix) or not actual_path.ends_with(".png"):
			_fail("%s action loaded an unexpected frame: %s" % [action, actual_path])
			return

	avatar.queue_free()
	await process_frame
	print("DRAGON_QUEEN_AVATAR_FRAMES_SMOKE_OK")
	quit(0)


func _validate_frame_files() -> bool:
	for action in ACTIONS:
		for frame_index in range(1, FRAME_COUNT + 1):
			var path := _frame_path(action, frame_index)
			if not ResourceLoader.exists(path):
				_fail("Missing avatar frame: %s" % path)
				return false

			var texture := load(path) as Texture2D
			if texture == null:
				_fail("Failed to load avatar frame: %s" % path)
				return false
			if Vector2i(texture.get_width(), texture.get_height()) != FRAME_SIZE:
				_fail("Unexpected avatar frame size for %s" % path)
				return false

	return true


func _frame_path(action: String, frame_index: int) -> String:
	return "%s/%s/processed/%s-%d.png" % [AVATAR_ROOT, action, action, frame_index]


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("DRAGON_QUEEN_AVATAR_FRAMES_SMOKE_FAIL %s" % message)
	quit(1)
