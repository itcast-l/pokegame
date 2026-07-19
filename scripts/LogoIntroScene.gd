extends Control

const NEXT_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const SKIP_DELAY_SECONDS := 0.75
const CROSSFADE_SECONDS := 0.35
const RELEASE_PACKAGE_SMOKE_ARGUMENT := "--release-package-smoke"

@onready var _video_player: VideoStreamPlayer = %LogoVideoPlayer
@onready var _audio_player: AudioStreamPlayer = %LogoAudioPlayer
@onready var _logo_visuals: CanvasItem = %LogoVisuals

var _skip_enabled := false
var _finishing := false


func _ready() -> void:
	if RELEASE_PACKAGE_SMOKE_ARGUMENT in OS.get_cmdline_user_args():
		_video_player.autoplay = false
		_audio_player.autoplay = false
		_video_player.stream = null
		_audio_player.stream = null
		call_deferred("_run_release_package_smoke")
		return
	_video_player.finished.connect(_on_video_finished)
	get_tree().create_timer(SKIP_DELAY_SECONDS).timeout.connect(_enable_skip)
	_video_player.play()
	_audio_player.play()


func _run_release_package_smoke() -> void:
	for _frame in range(5):
		await get_tree().process_frame
	print("WINDOWS_RELEASE_EXECUTABLE_SMOKE_OK")
	get_tree().quit(0)


func _input(event: InputEvent) -> void:
	if not _skip_enabled or _finishing:
		return

	if event is InputEventKey and event.pressed and not event.echo:
		_finish(true)
	elif event is InputEventMouseButton and event.pressed:
		_finish(true)
	elif event is InputEventJoypadButton and event.pressed:
		_finish(true)
	elif event is InputEventScreenTouch and event.pressed:
		_finish(true)


func _enable_skip() -> void:
	_skip_enabled = true


func _on_video_finished() -> void:
	_finish(false)


func _finish(stop_media: bool) -> void:
	if _finishing:
		return

	_finishing = true
	set_process_input(false)

	if stop_media:
		_video_player.stop()
		_audio_player.stop()

	_crossfade_to_main_menu()


func _crossfade_to_main_menu() -> void:
	var main_menu := _instantiate_main_menu()
	if main_menu == null:
		get_tree().change_scene_to_file(NEXT_SCENE_PATH)
		return

	add_child(main_menu)
	move_child(main_menu, 0)

	var tween := create_tween()
	tween.tween_property(_logo_visuals, "modulate:a", 0.0, CROSSFADE_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.finished.connect(_complete_handoff.bind(main_menu))


func _instantiate_main_menu() -> Node:
	var packed_scene := _get_main_menu_packed_scene()
	if packed_scene == null:
		return null

	return packed_scene.instantiate()


func _get_main_menu_packed_scene() -> PackedScene:
	return load(NEXT_SCENE_PATH) as PackedScene


func _complete_handoff(main_menu: Node) -> void:
	if not is_instance_valid(main_menu):
		return

	if main_menu.get_parent() == self:
		remove_child(main_menu)
		get_tree().root.add_child(main_menu)

	get_tree().current_scene = main_menu
	queue_free()
