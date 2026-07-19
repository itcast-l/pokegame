extends SceneTree

const Compression = preload("res://addons/pandora/util/compression.gd")


func _initialize() -> void:
	var expected := "UTF-8 Pandora: 稳定补牌 / 繁體中文 / English"
	var path := "user://pandora-compression-utf8-smoke.pandora"
	var output := FileAccess.open(path, FileAccess.WRITE)
	if output == null:
		_fail("Unable to create compressed Pandora smoke fixture")
		return
	output.store_buffer(Compression.compress(expected))
	output.close()

	var input := FileAccess.open_compressed(path, FileAccess.READ)
	if input == null:
		_fail("Unable to reopen compressed Pandora smoke fixture")
		return
	var actual := input.get_as_text()
	input.close()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	if actual != expected:
		_fail("Pandora UTF-8 compression roundtrip mismatch")
		return

	print("PANDORA_COMPRESSION_UTF8_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
