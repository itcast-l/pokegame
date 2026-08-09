extends SceneTree

const AVATAR_SCENE_PATH := "res://scenes/character_avatar.tscn"
const AVATAR_ROOT := "res://art/characters/boss_k_avatar/run-003"
const FRAME_COUNT := 12
const FRAME_SIZE := Vector2i(128, 128)
const FRAME_ADVANCE_SECONDS := 0.14
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
		var first_frame_path := portrait.texture.resource_path if portrait.texture != null else ""
		var expected_prefix := "%s/%s/processed/%s-" % [AVATAR_ROOT, action, action]
		if not first_frame_path.begins_with(expected_prefix) or not first_frame_path.ends_with(".png"):
			_fail("%s action loaded an unexpected frame: %s" % [action, first_frame_path])
			return

		await create_timer(FRAME_ADVANCE_SECONDS).timeout
		var advanced_frame_path := portrait.texture.resource_path if portrait.texture != null else ""
		if not advanced_frame_path.begins_with(expected_prefix) or not advanced_frame_path.ends_with(".png"):
			_fail("%s action advanced to an unexpected frame: %s" % [action, advanced_frame_path])
			return
		if advanced_frame_path == first_frame_path:
			_fail("%s action did not advance from its first frame" % action)
			return

	avatar.queue_free()
	await process_frame
	print("BOSS_K_AVATAR_FRAMES_SMOKE_OK")
	quit(0)


func _validate_frame_files() -> bool:
	var idle_used_rect := Rect2i()
	for action in ACTIONS:
		for frame_index in range(1, FRAME_COUNT + 1):
			var path := _frame_path(action, frame_index)
			if not ResourceLoader.exists(path):
				_fail("Missing avatar frame: %s" % path)
				return false

			var texture := load(path) as Texture2D
			if texture == null:
				_fail("Failed to load avatar texture: %s" % path)
				return false

			var image := texture.get_image()
			if image == null or image.is_empty():
				_fail("Failed to load avatar image: %s" % path)
				return false
			if Vector2i(image.get_width(), image.get_height()) != FRAME_SIZE:
				_fail("Unexpected avatar frame size for %s" % path)
				return false
			for corner in [Vector2i.ZERO, Vector2i(FRAME_SIZE.x - 1, 0), Vector2i(0, FRAME_SIZE.y - 1), FRAME_SIZE - Vector2i.ONE]:
				if image.get_pixelv(corner).a > 0.01:
					_fail("Avatar frame corner is not transparent: %s" % path)
					return false

			if action == "idle":
				var used_rect := image.get_used_rect()
				if frame_index == 1:
					idle_used_rect = used_rect
				elif used_rect != idle_used_rect:
					_fail("Idle frame bounds are unstable for %s: expected %s, got %s" % [path, idle_used_rect, used_rect])
					return false

	return true


func _frame_path(action: String, frame_index: int) -> String:
	return "%s/%s/processed/%s-%d.png" % [AVATAR_ROOT, action, action, frame_index]


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("BOSS_K_AVATAR_FRAMES_SMOKE_FAIL %s" % message)
	quit(1)
