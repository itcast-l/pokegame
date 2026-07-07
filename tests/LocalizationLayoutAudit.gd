extends SceneTree

const CSV_PATH := "res://localization/ui.csv"
const SCENE_ROOT := "res://scenes"
const VIEWPORT_SIZE := Vector2(1280.0, 720.0)
const OVERFLOW_TOLERANCE := 2.0
const VIEWPORT_TOLERANCE := 1.0
const ISSUE_PRINT_LIMIT := 400
const SCENE_ALLOWLIST_ENV := "LOCALIZATION_LAYOUT_SCENE_ALLOWLIST"

var _translations := {}
var _translation_keys := {}
var _locales: PackedStringArray = []
var _scenes: PackedStringArray = []
var _issues: Array[String] = []
var _warnings: Array[String] = []
var _scene_allowlist := {}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = Vector2i(int(VIEWPORT_SIZE.x), int(VIEWPORT_SIZE.y))
	await process_frame

	_load_translations()
	_load_scene_allowlist()
	_collect_scenes(SCENE_ROOT)
	_scenes.sort()

	if _locales.is_empty():
		_fail("No locales found in %s" % CSV_PATH)
		return

	if _scenes.is_empty():
		_fail("No scenes found under %s" % SCENE_ROOT)
		return

	for scene_path in _scenes:
		await _audit_scene(scene_path)

	print("LOCALIZATION_LAYOUT_AUDIT_SCENES %d" % _scenes.size())
	print("LOCALIZATION_LAYOUT_AUDIT_LOCALES %d %s" % [_locales.size(), ",".join(_locales)])
	print("LOCALIZATION_LAYOUT_AUDIT_WARNINGS %d" % _warnings.size())
	for warning in _warnings:
		print("LOCALIZATION_LAYOUT_AUDIT_WARNING %s" % warning)

	if not _issues.is_empty():
		print("LOCALIZATION_LAYOUT_AUDIT_ISSUES %d" % _issues.size())
		for index in range(min(_issues.size(), ISSUE_PRINT_LIMIT)):
			print("LOCALIZATION_LAYOUT_AUDIT_ISSUE %s" % _issues[index])
		if _issues.size() > ISSUE_PRINT_LIMIT:
			print("LOCALIZATION_LAYOUT_AUDIT_ISSUE_TRUNCATED %d" % (_issues.size() - ISSUE_PRINT_LIMIT))
		quit(1)
		return

	print("LOCALIZATION_LAYOUT_AUDIT_OK")
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
			_warnings.append("Duplicate locale column: %s" % locale)
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
		_translation_keys[key] = true
		for index in range(_locales.size()):
			var locale := _locales[index]
			var column_index := index + 1
			var text := ""
			if column_index < row.size():
				text = String(row[column_index])
			if text.is_empty():
				_issues.append("missing_translation_cell key=%s locale=%s" % [key, locale])
				continue
			(_translations[locale] as Translation).add_message(key, text, "")

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
		_warnings.append("Cannot open scene directory: %s" % path)
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
		elif entry.ends_with(".tscn") and (_scene_allowlist.is_empty() or _scene_allowlist.has(child_path)):
			_scenes.append(child_path)
	dir.list_dir_end()


func _load_scene_allowlist() -> void:
	var raw_allowlist := OS.get_environment(SCENE_ALLOWLIST_ENV).strip_edges()
	if raw_allowlist.is_empty():
		return

	for entry in raw_allowlist.split(";", false):
		var scene_path := String(entry).strip_edges()
		if not scene_path.is_empty():
			_scene_allowlist[scene_path] = true


func _prepare_scene_for_audit(scene_path: String, node: Node) -> void:
	if scene_path.ends_with("multiplayer_lobby_room_row.tscn") and node.has_method("Configure") and node is Control:
		var sample_room := {
			"room_name": "Sample Room",
			"host_name": "Host",
			"address": "192.168.1.20",
			"transport": "Lan",
			"status": "Lobby",
			"player_count": 2,
			"winning_score": 100,
			"item_mode": true,
			"has_password": false,
		}
		(node as Control).size = Vector2(420.0, 76.0)
		node.call("Configure", sample_room)
		return

	if scene_path.ends_with("multiplayer_lobby_player_row.tscn") and node.has_method("Configure") and node is Control:
		var sample_player := {
			"seat_index": 0,
			"display_name": "Player",
			"is_host": true,
			"is_local": true,
			"is_ready": true,
			"is_connected": true,
		}
		(node as Control).size = Vector2(420.0, 40.0)
		node.call("Configure", sample_player)


func _audit_scene(scene_path: String) -> void:
	var packed_scene := load(scene_path) as PackedScene
	if packed_scene == null:
		_issues.append("load_failed scene=%s" % scene_path)
		return

	var node := packed_scene.instantiate()
	if node == null:
		_issues.append("instantiate_failed scene=%s" % scene_path)
		return

	root.add_child(node)
	await process_frame
	await process_frame
	_prepare_scene_for_audit(scene_path, node)
	await process_frame
	await process_frame

	for locale in _locales:
		TranslationServer.set_locale(locale)
		await process_frame
		await process_frame
		_audit_node(node, scene_path, locale)

	if is_instance_valid(node) and node.get_parent() != null:
		node.queue_free()
		await process_frame


func _audit_node(node: Node, scene_path: String, locale: String) -> void:
	if node is Control:
		_audit_control(node as Control, scene_path, locale)

	for child in node.get_children():
		_audit_node(child, scene_path, locale)


func _audit_control(control: Control, scene_path: String, locale: String) -> void:
	if not control.is_visible_in_tree():
		return

	var text_entries := _get_control_text_entries(control)
	if text_entries.is_empty():
		return

	var rect := control.get_global_rect()
	var size := control.size
	for entry in text_entries:
		var text := String(entry.get("text", ""))
		var source := String(entry.get("source", "text"))
		if text.strip_edges().is_empty():
			continue

		var display_text := TranslationServer.translate(text)
		if _looks_like_untranslated_key(text) and display_text == text:
			_issues.append("untranslated_key locale=%s scene=%s node=%s source=%s text=%s" % [
				locale,
				scene_path,
				str(control.get_path()),
				source,
				text,
			])

		if size.x <= 0.0 or size.y <= 0.0:
			_issues.append("zero_sized_text_control locale=%s scene=%s node=%s source=%s size=%s text=%s" % [
				locale,
				scene_path,
				str(control.get_path()),
				source,
				str(size),
				_shorten(display_text),
			])
			continue

		if _is_clipped_by_scroll_container(control, rect):
			continue

		var minimum := _get_required_text_size(control, display_text)
		if minimum.x > size.x + OVERFLOW_TOLERANCE or minimum.y > size.y + OVERFLOW_TOLERANCE:
			_issues.append("text_overflow locale=%s scene=%s node=%s source=%s size=%s required=%s text=%s" % [
				locale,
				scene_path,
				str(control.get_path()),
				source,
				str(size),
				str(minimum),
				_shorten(display_text),
			])

	if not _is_clipped_by_scroll_container(control, rect) and not _viewport_contains(rect):
		_issues.append("visible_control_outside_viewport locale=%s scene=%s node=%s rect=%s text=%s" % [
			locale,
			scene_path,
			str(control.get_path()),
			str(rect),
			_shorten(TranslationServer.translate(String(text_entries[0].get("text", "")))),
		])


func _get_control_text_entries(control: Control) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if control is OptionButton:
		var option := control as OptionButton
		for index in range(option.item_count):
			entries.append({"source": "item_%d" % index, "text": option.get_item_text(index)})
	elif control is Button:
		entries.append({"source": "text", "text": (control as Button).text})
	elif control is Label:
		entries.append({"source": "text", "text": (control as Label).text})
	elif control is RichTextLabel:
		entries.append({"source": "text", "text": (control as RichTextLabel).text})
	elif control is LineEdit:
		var line_edit := control as LineEdit
		entries.append({"source": "text", "text": line_edit.text})
		entries.append({"source": "placeholder", "text": line_edit.placeholder_text})
	elif control is TextEdit:
		var text_edit := control as TextEdit
		entries.append({"source": "text", "text": text_edit.text})
		entries.append({"source": "placeholder", "text": text_edit.placeholder_text})
	return entries


func _get_required_text_size(control: Control, text: String) -> Vector2:
	if control is Label:
		return _get_label_required_size(control as Label, text)
	if control is RichTextLabel:
		return _get_rich_text_required_size(control as RichTextLabel, text)
	if control is Button:
		return control.get_combined_minimum_size()
	if control is LineEdit or control is TextEdit:
		return control.get_combined_minimum_size()
	return control.get_combined_minimum_size()


func _get_label_required_size(label: Label, text: String) -> Vector2:
	if label.autowrap_mode != TextServer.AUTOWRAP_OFF:
		return label.get_combined_minimum_size()

	var font := label.get_theme_font("font")
	var font_size := label.get_theme_font_size("font_size")
	var text_size := font.get_string_size(text, label.horizontal_alignment, -1.0, font_size)
	var minimum := label.get_combined_minimum_size()
	return Vector2(max(text_size.x, minimum.x), max(text_size.y, minimum.y))


func _get_rich_text_required_size(label: RichTextLabel, _text: String) -> Vector2:
	var minimum := label.get_combined_minimum_size()
	if label.fit_content:
		minimum.y = max(minimum.y, label.get_content_height())
	return minimum


func _looks_like_untranslated_key(text: String) -> bool:
	var value := text.strip_edges()
	if not _translation_keys.has(value):
		return false
	return value.to_upper() == value and value.contains("_")


func _is_clipped_by_scroll_container(control: Control, rect: Rect2) -> bool:
	var parent := control.get_parent()
	while parent != null:
		if parent is ScrollContainer:
			var scroll_rect := (parent as ScrollContainer).get_global_rect()
			if not rect.intersects(scroll_rect):
				return true
		parent = parent.get_parent()
	return false


func _viewport_contains(rect: Rect2) -> bool:
	if rect.position.x < -VIEWPORT_TOLERANCE or rect.position.y < -VIEWPORT_TOLERANCE:
		return false
	if rect.end.x > VIEWPORT_SIZE.x + VIEWPORT_TOLERANCE:
		return false
	if rect.end.y > VIEWPORT_SIZE.y + VIEWPORT_TOLERANCE:
		return false
	return true


func _shorten(text: String) -> String:
	var sanitized := text.replace("\n", "\\n").replace("\r", "\\r")
	if sanitized.length() <= 120:
		return sanitized
	return "%s..." % sanitized.substr(0, 117)


func _fail(message: String) -> void:
	push_error(message)
	print("LOCALIZATION_LAYOUT_AUDIT_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
