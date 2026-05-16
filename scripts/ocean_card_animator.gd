extends Sprite2D

@export_range(1.0, 30.0, 0.5) var animation_fps := 6.0
@export_range(1, 12, 1) var frame_count := 4

var _frames: Array[Texture2D] = []
var _elapsed := 0.0


func _ready() -> void:
	_frames = _load_sibling_frames()
	if _frames.size() <= 1:
		set_process(false)
		return

	var phase := float(get_instance_id() % 1000) / 1000.0
	_elapsed = phase * float(_frames.size()) / animation_fps


func _process(delta: float) -> void:
	_elapsed += delta
	var frame_index := int(_elapsed * animation_fps) % _frames.size()
	texture = _frames[frame_index]


func _load_sibling_frames() -> Array[Texture2D]:
	var loaded_frames: Array[Texture2D] = []
	if texture == null:
		return loaded_frames

	loaded_frames.append(texture)
	var frame_path := texture.resource_path
	if frame_path.is_empty():
		return loaded_frames

	var frame_dir := frame_path.get_base_dir()
	for index in range(2, frame_count + 1):
		var sibling_path := "%s/frame-%03d.png" % [frame_dir, index]
		var sibling := load(sibling_path)
		if sibling is Texture2D:
			loaded_frames.append(sibling)

	return loaded_frames
