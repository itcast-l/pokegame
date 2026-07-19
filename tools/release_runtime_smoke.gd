extends SceneTree

var _artifact_path := ""
var _main_scene := ""
var _translations: Array[String] = []


func _initialize() -> void:
	call_deferred("_run_smoke")


func _run_smoke() -> void:
	_parse_arguments()
	if _artifact_path.is_empty() or _main_scene.is_empty() or _translations.is_empty():
		_fail("Release runtime smoke requires --artifact, --main-scene, and --translation")
		return
	var harness_project := ProjectSettings.globalize_path("res://project.godot")
	if FileAccess.file_exists(harness_project):
		DirAccess.remove_absolute(harness_project)
	if not ProjectSettings.load_resource_pack(_artifact_path, true):
		_fail("Unable to mount release PCK for runtime smoke")
		return
	var packed_scene := ResourceLoader.load(_main_scene) as PackedScene
	if packed_scene == null:
		_fail("Unable to load release main scene: %s" % _main_scene)
		return
	var scene := packed_scene.instantiate()
	if scene == null:
		_fail("Unable to instantiate release main scene: %s" % _main_scene)
		return
	_disable_media_autoplay(scene)
	# The cross-platform adapter validates the scene graph without target-only startup side effects.
	scene.set_script(null)
	root.add_child(scene)
	for translation_path: String in _translations:
		if ResourceLoader.load(translation_path) == null:
			_fail("Unable to load release translation: %s" % translation_path)
			return
	var pandora_file := FileAccess.open_compressed("res://data.pandora", FileAccess.READ)
	if pandora_file == null:
		_fail("Unable to open release Pandora data")
		return
	var pandora_data = JSON.parse_string(pandora_file.get_as_text())
	pandora_file.close()
	if not pandora_data is Dictionary or pandora_data.is_empty():
		_fail("Release Pandora data is invalid or empty")
		return
	for _frame in range(5):
		await process_frame
	_stop_media(scene)
	scene.free()
	packed_scene = null
	pandora_file = null
	pandora_data = null
	for _frame in range(3):
		await process_frame
	call_deferred("_finish_success")


func _finish_success() -> void:
	print("RELEASE_RUNTIME_SMOKE_OK")
	quit(0)


func _stop_media(node: Node) -> void:
	if node is AudioStreamPlayer:
		var player := node as AudioStreamPlayer
		player.stop()
		player.stream = null
	elif node is AudioStreamPlayer2D:
		var player := node as AudioStreamPlayer2D
		player.stop()
		player.stream = null
	elif node is AudioStreamPlayer3D:
		var player := node as AudioStreamPlayer3D
		player.stop()
		player.stream = null
	elif node is VideoStreamPlayer:
		var player := node as VideoStreamPlayer
		player.stop()
		player.stream = null
	for child: Node in node.get_children():
		_stop_media(child)


func _disable_media_autoplay(node: Node) -> void:
	if node is AudioStreamPlayer:
		(node as AudioStreamPlayer).autoplay = false
	elif node is AudioStreamPlayer2D:
		(node as AudioStreamPlayer2D).autoplay = false
	elif node is AudioStreamPlayer3D:
		(node as AudioStreamPlayer3D).autoplay = false
	elif node is VideoStreamPlayer:
		(node as VideoStreamPlayer).autoplay = false
	for child: Node in node.get_children():
		_disable_media_autoplay(child)


func _parse_arguments() -> void:
	var arguments := OS.get_cmdline_user_args()
	var index := 0
	while index < arguments.size():
		if index + 1 >= arguments.size():
			return
		var argument := arguments[index]
		var value := arguments[index + 1]
		if argument == "--artifact":
			_artifact_path = value
		elif argument == "--main-scene":
			_main_scene = value
		elif argument == "--translation":
			_translations.append(value)
		index += 2


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
