extends SceneTree

const CSV_PATH := "res://localization/ui.csv"
const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1280, 720)
const TIMEOUT_SECONDS := 20.0
const OVERFLOW_TOLERANCE := 1.0
const VIEWPORT_TOLERANCE := 1.0
const SAVED_RUN_AVATAR_ID := "shrimp"
const RESTART_TARGET_AVATAR_ID := "crab"
const STRESS_LOCALE := "stress_long"

var _failed := false
var _translations := {}
var _locales: PackedStringArray = []
var _issues: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = VIEWPORT_SIZE
	await process_frame

	var saved_board := await _create_saved_run_board()
	if _failed or saved_board == null:
		return

	if saved_board is CanvasItem:
		(saved_board as CanvasItem).visible = false
	saved_board.process_mode = Node.PROCESS_MODE_DISABLED

	_load_translations()
	_install_stress_translation()
	if _failed:
		return

	for locale in _locales:
		await _audit_locale(locale)
		if _failed:
			return

	if not _issues.is_empty():
		for issue in _issues:
			print("ADVENTURE_RUN_RESUME_LOCALIZATION_LAYOUT_ISSUE %s" % issue)
		_fail("Found %d localization layout issues" % _issues.size())
		return

	saved_board.queue_free()
	await process_frame
	await process_frame

	print("ADVENTURE_RUN_RESUME_LOCALIZATION_LAYOUT_SMOKE_OK %d" % _locales.size())
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
		if locale.is_empty() or _translations.has(locale):
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


func _install_stress_translation() -> void:
	var translation := Translation.new()
	translation.locale = STRESS_LOCALE
	translation.add_message(
		"ADVENTURE_RESUME_RUN_TITLE",
		"Resume the unfinished enhancement expedition before discarding its stored progress and starting again from the currently highlighted enemy route?",
		"")
	translation.add_message(
		"ADVENTURE_RESUME_RUN_BODY",
		"This profile already contains an unfinished enhancement expedition. Continue from the stored checkpoint, abandon that run entirely, or go back and keep the highlighted selection without changing the saved expedition state.",
		"")
	translation.add_message("ADVENTURE_CONTINUE_RUN", "Continue the stored expedition", "")
	translation.add_message("ADVENTURE_RESTART_RUN", "Abandon and restart from this highlighted selection", "")
	translation.add_message("ADVENTURE_BACK", "Return without changing the stored expedition", "")
	TranslationServer.add_translation(translation)
	_translations[STRESS_LOCALE] = translation
	_locales.append(STRESS_LOCALE)


func _normalize_locale(locale: String) -> String:
	var normalized := locale.strip_edges().replace("-", "_")
	match normalized:
		"nb", "nn":
			return "no"
		_:
			return normalized


func _create_saved_run_board() -> Node:
	var board := await _instantiate_board(SAVED_RUN_AVATAR_ID)
	if _failed or board == null:
		return null

	if not board.has_method("DebugIsAdventureUpgradeSelectionVisible") \
		or not board.has_method("DebugSelectAdventureUpgradeOption") \
		or not board.has_method("DebugGetAdventureAcquiredUpgradeCount") \
		or not board.has_method("DebugGetGameLoopPhaseName"):
		_fail("Missing adventure upgrade run-start debug hooks")
		return null

	if not await _wait_for_upgrade_selection(board):
		return null

	if not bool(board.call("DebugSelectAdventureUpgradeOption", 0)):
		_fail("Failed to select the first upgrade while creating a saved run")
		return null

	if not await _wait_for_actionable_state(board):
		return null

	if int(board.call("DebugGetAdventureAcquiredUpgradeCount")) != 1:
		_fail("Expected exactly one acquired upgrade after seeding the saved run")
		return null

	return board


func _instantiate_board(adventure_avatar_id: String) -> Node:
	await process_frame

	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % BOARD_SCENE_PATH)
		return null

	var board := packed_scene.instantiate()
	if board == null:
		_fail("Failed to instantiate scene: %s" % BOARD_SCENE_PATH)
		return null

	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", adventure_avatar_id)

	root.add_child(board)
	await process_frame
	return board


func _instantiate_menu(selected_avatar_id: String) -> Node:
	await process_frame

	var packed_scene := load(MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % MENU_SCENE_PATH)
		return null

	var menu := packed_scene.instantiate()
	if menu == null:
		_fail("Failed to instantiate scene: %s" % MENU_SCENE_PATH)
		return null

	menu.set("selected_avatar_id", selected_avatar_id)
	root.add_child(menu)
	await process_frame

	if not menu.has_method("DebugIsAdventureRunResumeDialogVisible"):
		_fail("Missing adventure run resume dialog debug hook")
		return null

	return menu


func _audit_locale(locale: String) -> void:
	TranslationServer.set_locale(locale)
	await process_frame
	await process_frame

	var menu := await _instantiate_menu(RESTART_TARGET_AVATAR_ID)
	if _failed or menu == null:
		return

	if not await _open_resume_dialog(menu):
		return

	var dialog := menu.get_node_or_null("AdventureRunResumeDialog") as Control
	if dialog == null:
		_fail("Adventure run resume dialog node was not created for locale %s" % locale)
		return

	_assert_visual_structure(dialog, locale)
	if _failed:
		return

	await process_frame
	await process_frame
	_audit_node(dialog, locale)

	menu.queue_free()
	await process_frame
	await process_frame


func _assert_visual_structure(dialog: Control, locale: String) -> void:
	var panel := dialog.get_node_or_null("DialogMotion/Panel") as Control
	var glass_surface := panel.get_node_or_null("GlassSurface") as ColorRect if panel != null else null
	var glass_frame := panel.get_node_or_null("GlassFrame") as Panel if panel != null else null
	if panel == null or glass_surface == null or glass_frame == null:
		_fail("Resume dialog is missing its unified glass panel structure for locale %s" % locale)
		return
	if dialog.get_node_or_null("DialogMotion/Panel/ContentMargin/Layout/BodyPanel") != null:
		_fail("Resume dialog should not render a descriptive body panel for locale %s" % locale)
		return

	if not dialog.has_meta("secondary_menu_visual_profile") \
			or str(dialog.get_meta("secondary_menu_visual_profile")) != "secondary-menu-unified":
		_fail("Resume dialog should use the shared secondary-menu glass profile")
		return

	_assert_button_frame(dialog, "DialogMotion/Panel/ContentMargin/Layout/ButtonRow/ContinueFrame", Color(0.42, 0.92, 0.94, 0.96), locale)
	if _failed:
		return
	_assert_button_frame(dialog, "DialogMotion/Panel/ContentMargin/Layout/ButtonRow/RestartFrame", Color(0.9, 0.52, 0.54, 0.84), locale)
	if _failed:
		return
	_assert_button_frame(dialog, "DialogMotion/Panel/ContentMargin/Layout/ButtonRow/CancelFrame", Color(0.62, 1, 0.94, 0.84), locale)


func _assert_button_frame(dialog: Control, node_path: String, expected_accent: Color, locale: String) -> void:
	var frame := dialog.get_node_or_null(node_path) as Control
	if frame == null:
		_fail("Missing button frame %s for locale %s" % [node_path, locale])
		return

	var script := frame.get_script() as Script
	var script_path := "" if script == null else String(script.resource_path)
	if script_path != "res://scripts/OceanMenuButtonFrame.cs":
		_fail("Resume dialog button frame %s should use OceanMenuButtonFrame, got %s" % [node_path, script_path])
		return

	if int(frame.get("FrameStyle")) != 12 or not (frame.get("AccentColor") as Color).is_equal_approx(expected_accent):
		_fail("Resume dialog button frame %s does not use the shared action treatment" % node_path)


func _open_resume_dialog(menu: Node) -> bool:
	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null:
		_fail("Adventure avatar menu is missing StartButton")
		return false

	start_button.emit_signal("pressed")
	return await _wait_for_resume_dialog(menu)


func _wait_for_resume_dialog(menu: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(menu):
			_fail("Adventure avatar menu freed unexpectedly while waiting for resume dialog")
			return false

		if bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
			return true

	_fail("Timed out waiting for the adventure run resume dialog")
	return false


func _wait_for_upgrade_selection(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for upgrade selection")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			return true

	_fail("Timed out waiting for the adventure upgrade selection")
	return false


func _wait_for_actionable_state(board: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame

		if not is_instance_valid(board):
			_fail("Board freed unexpectedly while waiting for actionable state")
			return false

		if bool(board.call("DebugIsAdventureUpgradeSelectionVisible")):
			continue

		if String(board.call("DebugGetGameLoopPhaseName")) == "AwaitingMainPlayerAction":
			return true

	_fail("Timed out waiting for the adventure board to become actionable")
	return false


func _audit_node(node: Node, locale: String) -> void:
	if node is Control:
		_audit_control(node as Control, locale)

	for child in node.get_children():
		_audit_node(child, locale)


func _audit_control(control: Control, locale: String) -> void:
	if not control.is_visible_in_tree():
		return

	var text_entries := _get_control_text_entries(control)
	if text_entries.is_empty():
		if not _viewport_contains(control.get_global_rect()):
			_issues.append("visible_control_outside_viewport locale=%s node=%s rect=%s" % [
				locale,
				str(control.get_path()),
				str(control.get_global_rect()),
			])
		return

	var rect := control.get_global_rect()
	var size := control.size
	for entry in text_entries:
		var text := String(entry.get("text", ""))
		var source := String(entry.get("source", "text"))
		if text.strip_edges().is_empty():
			continue

		var minimum := _get_required_text_size(control, text)
		if minimum.x > size.x + OVERFLOW_TOLERANCE or minimum.y > size.y + OVERFLOW_TOLERANCE:
			_issues.append("text_overflow locale=%s node=%s source=%s size=%s required=%s text=%s" % [
				locale,
				str(control.get_path()),
				source,
				str(size),
				str(minimum),
				_shorten(text),
			])

	if not _viewport_contains(rect):
		_issues.append("visible_control_outside_viewport locale=%s node=%s rect=%s text=%s" % [
			locale,
			str(control.get_path()),
			str(rect),
			_shorten(String(text_entries[0].get("text", ""))),
		])


func _get_control_text_entries(control: Control) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if control is Button:
		entries.append({"source": "text", "text": (control as Button).text})
	elif control is Label:
		entries.append({"source": "text", "text": (control as Label).text})
	return entries


func _get_required_text_size(control: Control, text: String) -> Vector2:
	if control is Label:
		return _get_label_required_size(control as Label, text)
	return control.get_combined_minimum_size()


func _get_label_required_size(label: Label, text: String) -> Vector2:
	if label.autowrap_mode != TextServer.AUTOWRAP_OFF:
		return label.get_combined_minimum_size()

	var font := label.get_theme_font("font")
	var font_size := label.get_theme_font_size("font_size")
	var text_size := font.get_string_size(text, label.horizontal_alignment, -1.0, font_size)
	var minimum := label.get_combined_minimum_size()
	return Vector2(max(text_size.x, minimum.x), max(text_size.y, minimum.y))


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
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_RUN_RESUME_LOCALIZATION_LAYOUT_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(32):
		await process_frame
