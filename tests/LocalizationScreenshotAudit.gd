extends SceneTree

const CSV_PATH := "res://localization/ui.csv"
const SCENE_ROOT := "res://scenes"
const VIEWPORT_SIZE := Vector2i(1280, 720)
const DEFAULT_SETTLE_FRAMES := 4

var _translations := {}
var _locales: PackedStringArray = []
var _scenes: PackedStringArray = []
var _issues: Array[String] = []
var _manifest_lines: PackedStringArray = []
var _output_dir := ""
var _captured := 0
var _visible_text_captures := 0
var _visible_text_scenes := {}
var _settle_frames := DEFAULT_SETTLE_FRAMES


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = VIEWPORT_SIZE
	await process_frame

	_output_dir = OS.get_environment("CODEX_LOCALIZATION_SCREENSHOT_DIR").strip_edges()
	if _output_dir.is_empty():
		_output_dir = ProjectSettings.globalize_path("res://outputs/localization_screenshots/latest")

	var settle_env := OS.get_environment("CODEX_LOCALIZATION_SCREENSHOT_SETTLE_FRAMES").strip_edges()
	if not settle_env.is_empty():
		_settle_frames = max(1, int(settle_env))

	_load_translations()
	_collect_scenes(SCENE_ROOT)
	_scenes.sort()
	_apply_filters()

	if _locales.is_empty():
		_fail("No locales found in %s" % CSV_PATH)
		return

	if _scenes.is_empty():
		_fail("No scenes selected under %s" % SCENE_ROOT)
		return

	var make_dir_error := DirAccess.make_dir_recursive_absolute(_output_dir)
	if make_dir_error != OK:
		_fail("Cannot create screenshot directory %s: %s" % [_output_dir, error_string(make_dir_error)])
		return

	for scene_path in _scenes:
		for locale in _locales:
			await _capture_scene_locale(scene_path, locale)

	_write_manifest()

	print("LOCALIZATION_SCREENSHOT_AUDIT_OUTPUT %s" % _output_dir)
	print("LOCALIZATION_SCREENSHOT_AUDIT_SCENES %d" % _scenes.size())
	print("LOCALIZATION_SCREENSHOT_AUDIT_LOCALES %d %s" % [_locales.size(), ",".join(_locales)])
	print("LOCALIZATION_SCREENSHOT_AUDIT_CAPTURED %d" % _captured)
	print("LOCALIZATION_SCREENSHOT_AUDIT_VISIBLE_TEXT_SCENES %d" % _visible_text_scenes.size())
	print("LOCALIZATION_SCREENSHOT_AUDIT_VISIBLE_TEXT_CAPTURES %d" % _visible_text_captures)

	if not _issues.is_empty():
		print("LOCALIZATION_SCREENSHOT_AUDIT_ISSUES %d" % _issues.size())
		for issue in _issues:
			print("LOCALIZATION_SCREENSHOT_AUDIT_ISSUE %s" % issue)
		quit(1)
		return

	print("LOCALIZATION_SCREENSHOT_AUDIT_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _load_translations() -> void:
	var file := FileAccess.open(CSV_PATH, FileAccess.READ)
	if file == null:
		_fail("Missing localization table: %s" % CSV_PATH)
		return

	var header := file.get_csv_line()
	if header.size() < 2 or header[0] != "key":
		_fail("Invalid localization header in %s" % CSV_PATH)
		return

	for index in range(1, header.size()):
		var locale := _normalize_locale(header[index])
		if locale.is_empty():
			continue
		if _translations.has(locale):
			continue
		var translation := Translation.new()
		translation.locale = locale
		_translations[locale] = translation
		_locales.append(locale)

	while not file.eof_reached():
		var row := file.get_csv_line()
		if row.is_empty() or String(row[0]).strip_edges().is_empty():
			continue

		var key := String(row[0]).strip_edges()
		for index in range(_locales.size()):
			var column_index := index + 1
			if column_index >= row.size():
				continue
			var text := String(row[column_index])
			if text.is_empty():
				continue
			(_translations[_locales[index]] as Translation).add_message(key, text, "")

	for locale in _locales:
		TranslationServer.add_translation(_translations[locale])


func _normalize_locale(locale: String) -> String:
	var normalized := locale.strip_edges().replace("-", "_")
	match normalized:
		"nb", "nn":
			return "no"
		_:
			return normalized


func _collect_scenes(path: String) -> void:
	var dir := DirAccess.open(path)
	if dir == null:
		_issues.append("cannot_open_scene_directory path=%s" % path)
		return

	dir.list_dir_begin()
	while true:
		var entry := dir.get_next()
		if entry.is_empty():
			break
		if entry.begins_with("."):
			continue
		var child_path := "%s/%s" % [path, entry]
		if dir.current_is_dir():
			_collect_scenes(child_path)
		elif entry.ends_with(".tscn"):
			_scenes.append(child_path)
	dir.list_dir_end()


func _apply_filters() -> void:
	_locales = _filter_locales(_locales, OS.get_environment("CODEX_LOCALIZATION_SCREENSHOT_LOCALES"))
	_scenes = _filter_scenes(_scenes, OS.get_environment("CODEX_LOCALIZATION_SCREENSHOT_SCENES"))


func _filter_locales(values: PackedStringArray, filter: String) -> PackedStringArray:
	var accepted := _split_filter(filter)
	if accepted.is_empty():
		return values

	var filtered: PackedStringArray = []
	for value in values:
		if accepted.has(value):
			filtered.append(value)
	return filtered


func _filter_scenes(values: PackedStringArray, filter: String) -> PackedStringArray:
	var accepted := _split_filter(filter)
	if accepted.is_empty():
		return values

	var filtered: PackedStringArray = []
	for value in values:
		var basename := value.get_file().get_basename()
		var filesystem_path := value.replace("res://", "")
		if accepted.has(value) or accepted.has(filesystem_path) or accepted.has(basename) or accepted.has(_scene_slug(value)):
			filtered.append(value)
	return filtered


func _split_filter(filter: String) -> Dictionary:
	var accepted := {}
	for raw in filter.split(",", false):
		var value := raw.strip_edges()
		if not value.is_empty():
			accepted[value] = true
	return accepted


func _capture_scene_locale(scene_path: String, locale: String) -> void:
	TranslationServer.set_locale(locale)

	var packed_scene := load(scene_path) as PackedScene
	if packed_scene == null:
		_issues.append("load_failed scene=%s locale=%s" % [scene_path, locale])
		return

	var node := packed_scene.instantiate()
	if node == null:
		_issues.append("instantiate_failed scene=%s locale=%s" % [scene_path, locale])
		return

	var viewport := SubViewport.new()
	viewport.size = VIEWPORT_SIZE
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	viewport.add_child(node)

	for _frame in range(_settle_frames):
		await process_frame

	TranslationServer.set_locale(locale)
	for _frame in range(_settle_frames):
		await process_frame
	await RenderingServer.frame_post_draw

	var has_visible_text := _has_visible_text(node)
	if has_visible_text:
		_visible_text_scenes[scene_path] = true
		_visible_text_captures += 1

	var image := viewport.get_texture().get_image()
	if image == null:
		_issues.append("image_null scene=%s locale=%s" % [scene_path, locale])
		_free_viewport(viewport)
		return

	if image.get_width() != VIEWPORT_SIZE.x or image.get_height() != VIEWPORT_SIZE.y:
		_issues.append("wrong_image_size scene=%s locale=%s size=%dx%d" % [
			scene_path,
			locale,
			image.get_width(),
			image.get_height(),
		])

	if has_visible_text and not _image_has_pixel_variation(image):
		_issues.append("blank_visible_text_screenshot scene=%s locale=%s" % [scene_path, locale])

	var scene_dir := _output_dir.path_join(_scene_slug(scene_path))
	var make_dir_error := DirAccess.make_dir_recursive_absolute(scene_dir)
	if make_dir_error != OK:
		_issues.append("cannot_create_scene_screenshot_dir scene=%s locale=%s path=%s error=%s" % [
			scene_path,
			locale,
			scene_dir,
			error_string(make_dir_error),
		])
		_free_viewport(viewport)
		return

	var save_path := scene_dir.path_join("%s.png" % locale)
	var save_error := image.save_png(save_path)
	if save_error != OK:
		_issues.append("save_failed scene=%s locale=%s path=%s error=%s" % [
			scene_path,
			locale,
			save_path,
			error_string(save_error),
		])
	else:
		_captured += 1
		_manifest_lines.append("%s\t%s\t%s\t%s" % [
			scene_path,
			locale,
			"visible_text" if has_visible_text else "no_visible_text",
			save_path,
		])

	_free_viewport(viewport)
	await process_frame
	await RenderingServer.frame_post_draw


func _free_viewport(viewport: SubViewport) -> void:
	if is_instance_valid(viewport) and viewport.get_parent() != null:
		viewport.queue_free()


func _has_visible_text(node: Node) -> bool:
	if node is Control:
		var control := node as Control
		if control.is_visible_in_tree() and _control_has_text(control):
			return true

	for child in node.get_children():
		if _has_visible_text(child):
			return true
	return false


func _control_has_text(control: Control) -> bool:
	if control is OptionButton:
		var option := control as OptionButton
		for index in range(option.item_count):
			if not option.get_item_text(index).strip_edges().is_empty():
				return true
	elif control is Button:
		return not (control as Button).text.strip_edges().is_empty()
	elif control is Label:
		return not (control as Label).text.strip_edges().is_empty()
	elif control is RichTextLabel:
		return not (control as RichTextLabel).text.strip_edges().is_empty()
	elif control is LineEdit:
		var line_edit := control as LineEdit
		return not line_edit.text.strip_edges().is_empty() or not line_edit.placeholder_text.strip_edges().is_empty()
	elif control is TextEdit:
		var text_edit := control as TextEdit
		return not text_edit.text.strip_edges().is_empty() or not text_edit.placeholder_text.strip_edges().is_empty()
	return false


func _image_has_pixel_variation(image: Image) -> bool:
	var width: int = image.get_width()
	var height: int = image.get_height()
	if width <= 0 or height <= 0:
		return false

	var baseline: Color = image.get_pixel(0, 0)
	var step_x: int = maxi(1, width / 32)
	var step_y: int = maxi(1, height / 18)
	for y in range(0, height, step_y):
		for x in range(0, width, step_x):
			if image.get_pixel(x, y) != baseline:
				return true
	return false


func _scene_slug(scene_path: String) -> String:
	return scene_path.replace("res://", "").trim_suffix(".tscn").replace("/", "__").replace(":", "_")


func _write_manifest() -> void:
	var manifest_path := _output_dir.path_join("manifest.tsv")
	var file := FileAccess.open(manifest_path, FileAccess.WRITE)
	if file == null:
		_issues.append("cannot_write_manifest path=%s" % manifest_path)
		return

	file.store_line("scene\tlocale\tvisibility\tscreenshot")
	for line in _manifest_lines:
		file.store_line(line)


func _fail(message: String) -> void:
	push_error(message)
	print("LOCALIZATION_SCREENSHOT_AUDIT_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
