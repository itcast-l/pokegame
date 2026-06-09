extends SceneTree

const SCENES := [
	{
		"name": "logo_intro_to_main_menu",
		"path": "res://scenes/logo_intro_scene.tscn",
		"settle_seconds": 0.1,
		"transition_to_main_menu": true,
	},
	{
		"name": "main_menu_scene",
		"path": "res://scenes/main_menu_scene.tscn",
		"settle_seconds": 0.2,
	},
	{
		"name": "multiplayer_lobby_scene",
		"path": "res://scenes/multiplayer_lobby_scene.tscn",
		"settle_seconds": 0.2,
	},
	{
		"name": "game_board_scene",
		"path": "res://scenes/game_board_scene.tscn",
		"settle_seconds": 0.2,
	},
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	for spec in SCENES:
		if _failed:
			return
		await _smoke_scene(spec)

	if not _failed:
		print("GODOT_SCENE_SMOKE_OK")
		quit(0)


func _smoke_scene(spec: Dictionary) -> void:
	var path := String(spec.get("path", ""))
	var scene_name := String(spec.get("name", path))
	var packed_scene := load(path) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % path)
		return

	var node := packed_scene.instantiate()
	if node == null:
		_fail("Failed to instantiate scene: %s" % path)
		return

	root.add_child(node)
	await process_frame
	await create_timer(float(spec.get("settle_seconds", 0.1))).timeout

	if bool(spec.get("transition_to_main_menu", false)):
		if not node.has_method("_crossfade_to_main_menu"):
			_fail("Scene is missing _crossfade_to_main_menu: %s" % path)
			return
		node.call("_crossfade_to_main_menu")
		await create_timer(0.6).timeout

	if is_instance_valid(node) and node.get_parent() != null:
		node.queue_free()
		await process_frame

	print("SCENE_OK %s" % scene_name)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("GODOT_SCENE_SMOKE_FAIL %s" % message)
	quit(1)
