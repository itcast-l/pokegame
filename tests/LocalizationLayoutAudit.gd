extends SceneTree

const CSV_PATH := "res://localization/ui.csv"
const SCENE_ROOT := "res://scenes"
const VIEWPORT_SIZES: Array[Vector2i] = [
	Vector2i(960, 540),
	Vector2i(1280, 720),
	Vector2i(1920, 1080),
]
const OVERFLOW_TOLERANCE := 2.0
const VIEWPORT_TOLERANCE := 1.0
const ISSUE_PRINT_LIMIT := 400
const SCENE_FILTER_ENV := "LOCALIZATION_LAYOUT_SCENES"
const EXCLUDED_SCENES := {
	"res://scenes/adventure_suit_upgrade_editor_scene.tscn": true,
	"res://scenes/card_gallery.tscn": true,
	"res://scenes/character_avatar_gallery_scene.tscn": true,
	"res://scenes/character_avatar_preview.tscn": true,
	"res://scenes/upgrade_choice_popup_asset_scene.tscn": true,
}
const LOCALE_FONT_PATHS := {
	"ar": "res://resources/fonts/noto_arabic.tres",
	"th": "res://resources/fonts/noto_thai.tres",
	"zh_CN": "res://resources/fonts/noto_sc.tres",
	"zh_TW": "res://resources/fonts/noto_tc.tres",
	"ja": "res://resources/fonts/noto_jp.tres",
	"ko": "res://resources/fonts/noto_kr.tres",
}

var _translations := {}
var _translation_keys := {}
var _translation_texts := {}
var _locales: PackedStringArray = []
var _scenes: PackedStringArray = []
var _issues: Array[String] = []
var _warnings: Array[String] = []
var _scene_filter := {}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = VIEWPORT_SIZES[1]
	await process_frame

	_load_translations()
	_load_scene_filter()
	_collect_scenes(SCENE_ROOT)
	_scenes.sort()

	if _locales.is_empty():
		_fail("No locales found in %s" % CSV_PATH)
		return

	if _scenes.is_empty():
		_fail("No scenes found under %s" % SCENE_ROOT)
		return

	_audit_font_coverage()
	for viewport_size in VIEWPORT_SIZES:
		for scene_path in _scenes:
			for locale in _locales:
				await _audit_scene(scene_path, locale, viewport_size)

	print("LOCALIZATION_LAYOUT_AUDIT_SCENES %d" % _scenes.size())
	print("LOCALIZATION_LAYOUT_AUDIT_LOCALES %d %s" % [_locales.size(), ",".join(_locales)])
	print("LOCALIZATION_LAYOUT_AUDIT_VIEWPORTS %d %s" % [VIEWPORT_SIZES.size(), _format_viewports()])
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
		_translation_texts[locale] = PackedStringArray()
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
			(_translation_texts[locale] as PackedStringArray).append(text)

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
		elif entry.ends_with(".tscn") \
				and not EXCLUDED_SCENES.has(child_path) \
				and (_scene_filter.is_empty() or _scene_filter.has(child_path)) \
				and (_scene_source_has_text_control(child_path) or child_path.ends_with("game_board_scene.tscn")):
			_scenes.append(child_path)
	dir.list_dir_end()


func _load_scene_filter() -> void:
	var raw_filter := OS.get_environment(SCENE_FILTER_ENV).strip_edges()
	if raw_filter.is_empty():
		return

	for entry in raw_filter.split(";", false):
		var scene_path := String(entry).strip_edges()
		if not scene_path.is_empty():
			_scene_filter[scene_path] = true


func _scene_source_has_text_control(scene_path: String) -> bool:
	var file := FileAccess.open(scene_path, FileAccess.READ)
	if file == null:
		_warnings.append("Cannot inspect scene source: %s" % scene_path)
		return false

	var source := file.get_as_text()
	for type_name in ["Label", "RichTextLabel", "Button", "LineEdit", "TextEdit", "OptionButton"]:
		if source.contains('type="%s"' % type_name):
			return true
	return false


func _prepare_scene_for_audit(scene_path: String, node: Node) -> void:
	_show_scene_root_for_audit(node)
	_hide_transient_board_intro(scene_path, node)

	if scene_path.ends_with("main_menu_skill_codex_overlay.tscn") and node.has_method("Open"):
		node.call("Open")
		return

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


func _hide_transient_board_intro(scene_path: String, node: Node) -> void:
	if not scene_path.contains("game_board_scene"):
		return
	var opening_intro := node.get_node_or_null("OpeningIntroAnimation") as CanvasItem
	if opening_intro != null:
		opening_intro.visible = false


func _show_scene_root_for_audit(node: Node) -> void:
	if not node is CanvasItem:
		return

	var canvas_item := node as CanvasItem
	canvas_item.visible = true
	var visible_modulate := canvas_item.modulate
	visible_modulate.a = 1.0
	canvas_item.modulate = visible_modulate


func _audit_scene(scene_path: String, locale: String, viewport_size: Vector2i) -> void:
	TranslationServer.set_locale(locale)

	var packed_scene := load(scene_path) as PackedScene
	if packed_scene == null:
		_issues.append("load_failed scene=%s locale=%s viewport=%s" % [scene_path, locale, _format_viewport(viewport_size)])
		return

	var node := packed_scene.instantiate()
	if node == null:
		_issues.append("instantiate_failed scene=%s locale=%s viewport=%s" % [scene_path, locale, _format_viewport(viewport_size)])
		return

	var viewport := SubViewport.new()
	viewport.size = viewport_size
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var audit_host := Control.new()
	audit_host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	audit_host.layout_direction = Control.LAYOUT_DIRECTION_LTR
	viewport.add_child(audit_host)
	audit_host.add_child(node)
	if _is_standalone_lobby_row(scene_path) and node is Control:
		var row := node as Control
		row.layout_direction = Control.LAYOUT_DIRECTION_APPLICATION_LOCALE
		row.position = Vector2(24.0, 24.0)
	await process_frame
	await process_frame
	TranslationServer.set_locale(locale)
	await process_frame
	await process_frame
	_prepare_scene_for_audit(scene_path, node)
	await process_frame
	await process_frame

	_audit_node(node, scene_path, locale, viewport_size)

	if is_instance_valid(viewport) and viewport.get_parent() != null:
		viewport.queue_free()
		await process_frame


func _audit_node(node: Node, scene_path: String, locale: String, viewport_size: Vector2i) -> void:
	if node is Control:
		_audit_control(node as Control, scene_path, locale, viewport_size)

	for child in node.get_children():
		_audit_node(child, scene_path, locale, viewport_size)


func _audit_control(control: Control, scene_path: String, locale: String, viewport_size: Vector2i) -> void:
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
			_issues.append("untranslated_key locale=%s viewport=%s scene=%s node=%s source=%s text=%s" % [
				locale,
				_format_viewport(viewport_size),
				scene_path,
				str(control.get_path()),
				source,
				text,
			])

		if size.x <= 0.0 or size.y <= 0.0:
			_issues.append("zero_sized_text_control locale=%s viewport=%s scene=%s node=%s source=%s size=%s text=%s" % [
				locale,
				_format_viewport(viewport_size),
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
		var width_overflows := minimum.x > size.x + OVERFLOW_TOLERANCE \
			and not _allows_horizontal_text_clipping(control)
		if width_overflows or minimum.y > size.y + OVERFLOW_TOLERANCE:
			_issues.append("text_overflow locale=%s viewport=%s scene=%s node=%s source=%s size=%s required=%s text=%s" % [
				locale,
				_format_viewport(viewport_size),
				scene_path,
				str(control.get_path()),
				source,
				str(size),
				str(minimum),
				_shorten(display_text),
			])

	if locale == "ar":
		_audit_arabic_control(control, text_entries, scene_path, viewport_size)

	if not _has_scroll_container_ancestor(control) and not _viewport_contains(rect, viewport_size):
		_issues.append("visible_control_outside_viewport locale=%s viewport=%s scene=%s node=%s rect=%s text=%s" % [
			locale,
			_format_viewport(viewport_size),
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


func _allows_horizontal_text_clipping(control: Control) -> bool:
	if control is Label:
		var label := control as Label
		return label.clip_text or label.text_overrun_behavior != TextServer.OVERRUN_NO_TRIMMING
	if control is Button:
		var button := control as Button
		return button.clip_text or button.text_overrun_behavior != TextServer.OVERRUN_NO_TRIMMING
	return false


func _is_standalone_lobby_row(scene_path: String) -> bool:
	return scene_path.ends_with("multiplayer_lobby_room_row.tscn") \
		or scene_path.ends_with("multiplayer_lobby_player_row.tscn")


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


func _has_scroll_container_ancestor(control: Control) -> bool:
	var parent := control.get_parent()
	while parent != null:
		if parent is ScrollContainer:
			return true
		parent = parent.get_parent()
	return false


func _viewport_contains(rect: Rect2, viewport_size: Vector2i) -> bool:
	if rect.position.x < -VIEWPORT_TOLERANCE or rect.position.y < -VIEWPORT_TOLERANCE:
		return false
	if rect.end.x > viewport_size.x + VIEWPORT_TOLERANCE:
		return false
	if rect.end.y > viewport_size.y + VIEWPORT_TOLERANCE:
		return false
	return true


func _audit_font_coverage() -> void:
	for locale in _locales:
		var font_path := String(LOCALE_FONT_PATHS.get(locale, "res://resources/fonts/noto_latin.tres"))
		var font := load(font_path) as Font
		if font == null:
			_issues.append("font_load_failed locale=%s path=%s" % [locale, font_path])
			continue

		var missing := {}
		for text in _translation_texts[locale] as PackedStringArray:
			for index in range(text.length()):
				var codepoint := text.unicode_at(index)
				if _is_ignorable_font_codepoint(codepoint) or font.has_char(codepoint):
					continue
				missing[String.chr(codepoint)] = codepoint

		if not missing.is_empty():
			var samples: PackedStringArray = []
			for character in missing:
				samples.append("%s(U+%04X)" % [character, int(missing[character])])
				if samples.size() >= 20:
					break
			_issues.append("font_missing_glyphs locale=%s path=%s count=%d samples=%s" % [
				locale,
				font_path,
				missing.size(),
				",".join(samples),
			])


func _is_ignorable_font_codepoint(codepoint: int) -> bool:
	return codepoint <= 0x20 \
		or codepoint >= 0x200B and codepoint <= 0x200F \
		or codepoint >= 0x202A and codepoint <= 0x202E \
		or codepoint >= 0x2060 and codepoint <= 0x2069 \
		or codepoint >= 0xFE00 and codepoint <= 0xFE0F


func _audit_arabic_control(
	control: Control,
	text_entries: Array[Dictionary],
	scene_path: String,
	viewport_size: Vector2i
) -> void:
	var arabic_text := ""
	for entry in text_entries:
		var display_text := TranslationServer.translate(String(entry.get("text", "")))
		if _contains_arabic(display_text):
			arabic_text = display_text
			break
	if arabic_text.is_empty():
		return

	if _scene_manages_typography(scene_path):
		var direction := int(control.get("text_direction"))
		if direction != int(Control.TEXT_DIRECTION_AUTO):
			_issues.append("arabic_text_direction_not_auto viewport=%s scene=%s node=%s direction=%d" % [
				_format_viewport(viewport_size), scene_path, str(control.get_path()), direction])

		if _scene_uses_locale_layout(scene_path) and not control.is_layout_rtl():
			_issues.append("arabic_layout_not_rtl viewport=%s scene=%s node=%s" % [
				_format_viewport(viewport_size), scene_path, str(control.get_path())])

	if control is LineEdit:
		var line_edit := control as LineEdit
		if line_edit.alignment == HORIZONTAL_ALIGNMENT_LEFT:
			_issues.append("arabic_input_not_right_aligned viewport=%s scene=%s node=%s" % [
				_format_viewport(viewport_size), scene_path, str(control.get_path())])

	var font := control.get_theme_font("font")
	var font_size := control.get_theme_font_size("font_size")
	var shaped_size := font.get_string_size(arabic_text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size)
	if shaped_size.x <= 0.0 or shaped_size.y <= 0.0:
		_issues.append("arabic_shaping_failed viewport=%s scene=%s node=%s text=%s" % [
			_format_viewport(viewport_size), scene_path, str(control.get_path()), _shorten(arabic_text)])


func _contains_arabic(text: String) -> bool:
	for index in range(text.length()):
		var codepoint := text.unicode_at(index)
		if codepoint >= 0x0600 and codepoint <= 0x06FF:
			return true
	return false


func _scene_manages_typography(scene_path: String) -> bool:
	return scene_path.ends_with("main_menu_scene.tscn") \
		or scene_path.ends_with("adventure_avatar_menu_scene.tscn") \
		or scene_path.ends_with("character_avatar_edit_scene.tscn") \
		or scene_path.ends_with("multiplayer_lobby_scene.tscn") \
		or scene_path.ends_with("game_board_scene.tscn") \
		or scene_path.ends_with("adventure_tutorial_game_board_scene.tscn") \
		or scene_path.ends_with("adventure_item_tutorial_game_board_scene.tscn")


func _scene_uses_locale_layout(scene_path: String) -> bool:
	return not scene_path.contains("game_board_scene")


func _format_viewport(viewport_size: Vector2i) -> String:
	return "%dx%d" % [viewport_size.x, viewport_size.y]


func _format_viewports() -> String:
	var values: PackedStringArray = []
	for viewport_size in VIEWPORT_SIZES:
		values.append(_format_viewport(viewport_size))
	return ",".join(values)


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
