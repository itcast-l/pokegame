extends SceneTree

const CSV_PATH := "res://localization/ui.csv"
const OVERLAY_SCENE_PATH := "res://scenes/adventure_tutorial_overlay.tscn"
const BOARD_SCENE_PATH := "res://scenes/adventure_tutorial_game_board_scene.tscn"
const VIEWPORT_SIZES := [Vector2i(1280, 720), Vector2i(360, 640)]
const STRESS_LOCALE := "stress_tutorial_long"
const TOLERANCE := 1.0
const TIMEOUT_SECONDS := 20.0

var _failed := false
var _translations := {}
var _locales: PackedStringArray = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_load_translations()
	_install_stress_translation()
	if _failed:
		return

	var packed_scene := load(OVERLAY_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Missing tutorial overlay scene")
		return

	for viewport_size in VIEWPORT_SIZES:
		root.size = viewport_size
		await process_frame
		for locale in _locales:
			await _audit_overlay(packed_scene, locale, viewport_size)
			if _failed:
				return

	await _audit_board_integration()
	if _failed:
		return

	print("ADVENTURE_TUTORIAL_OVERLAY_LOCALIZATION_LAYOUT_SMOKE_OK %d" % (_locales.size() * VIEWPORT_SIZES.size()))
	await _dispose_audio_before_quit()
	quit(0)


func _load_translations() -> void:
	var file := FileAccess.open(CSV_PATH, FileAccess.READ)
	if file == null:
		_fail("Missing localization table: %s" % CSV_PATH)
		return

	var header := file.get_csv_line()
	if header.size() < 2 or header[0] != "key":
		_fail("Invalid localization header")
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
		if key != "GAME_TUTORIAL_BASIC_INTRO" and key != "GAME_TUTORIAL_CONTINUE":
			continue
		for index in range(_locales.size()):
			var column_index := index + 1
			if column_index < row.size() and not String(row[column_index]).is_empty():
				(_translations[_locales[index]] as Translation).add_message(key, String(row[column_index]), "")

	for locale in _locales:
		TranslationServer.add_translation(_translations[locale])


func _install_stress_translation() -> void:
	var translation := Translation.new()
	translation.locale = STRESS_LOCALE
	translation.add_message(
		"GAME_TUTORIAL_BASIC_INTRO",
		"Review the highlighted play area carefully before continuing through the complete guided expedition sequence and choosing the next available action.",
		"")
	translation.add_message(
		"GAME_TUTORIAL_CONTINUE",
		"Continue the guided expedition",
		"")
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


func _audit_overlay(packed_scene: PackedScene, locale: String, viewport_size: Vector2i) -> void:
	TranslationServer.set_locale(locale)
	var overlay := packed_scene.instantiate() as Control
	if overlay == null:
		_fail("Could not instantiate tutorial overlay")
		return

	root.add_child(overlay)
	await process_frame

	var blocker := overlay.get_node_or_null("AdventureTutorialInputBlocker") as ColorRect
	var pointer := overlay.get_node_or_null("AdventureTutorialPointer") as Line2D
	var target := overlay.get_node_or_null("AdventureTutorialTargetOutline") as Panel
	var callout := overlay.get_node_or_null("AdventureTutorialCallout") as Panel
	var label := overlay.get_node_or_null("AdventureTutorialCallout/AdventureTutorialText") as Label
	var button := overlay.get_node_or_null("AdventureTutorialCallout/AdventureTutorialContinueButton") as Button
	if blocker == null or pointer == null or target == null or callout == null or label == null or button == null:
		_fail("Tutorial overlay scene contract is incomplete")
		return

	var target_rect := Rect2(
		Vector2(viewport_size.x * 0.38, viewport_size.y * 0.42),
		Vector2(minf(180.0, viewport_size.x * 0.34), 72.0))
	overlay.call("SetTargetRect", target_rect)
	overlay.call(
		"SetContent",
		tr("GAME_TUTORIAL_BASIC_INTRO"),
		tr("GAME_TUTORIAL_CONTINUE"),
		true)
	overlay.call("SetContinueEnabled", true)
	overlay.call("ShowOverlay")
	await process_frame
	await process_frame

	if not overlay.visible or not blocker.visible or blocker.mouse_filter != Control.MOUSE_FILTER_STOP:
		_fail("Continue mode did not preserve the input blocker for %s at %s" % [locale, viewport_size])
		return
	if button.disabled or not button.visible or button.focus_mode != Control.FOCUS_NONE:
		_fail("Continue button state changed for %s at %s" % [locale, viewport_size])
		return
	if label.text.is_empty() or button.text.is_empty():
		_fail("Tutorial text was empty for %s" % locale)
		return
	if target.position.distance_to(target_rect.position - Vector2(10, 10)) > TOLERANCE \
			or target.size.distance_to(target_rect.size + Vector2(20, 20)) > TOLERANCE:
		_fail("Target padding changed for %s at %s" % [locale, viewport_size])
		return
	if pointer.points.size() != 2:
		_fail("Tutorial pointer was not laid out for %s at %s" % [locale, viewport_size])
		return
	if not _rect_fits(Rect2(callout.position, callout.size), viewport_size):
		_fail("Callout overflowed the viewport for %s at %s: %s" % [locale, viewport_size, Rect2(callout.position, callout.size)])
		return
	if not _rect_fits(Rect2(callout.position + button.position, button.size), viewport_size):
		_fail("Continue button overflowed the viewport for %s at %s" % [locale, viewport_size])
		return

	overlay.call("SetContent", label.text, button.text, false)
	await process_frame
	if blocker.visible or blocker.mouse_filter != Control.MOUSE_FILTER_IGNORE or button.visible:
		_fail("Action mode did not release the input blocker for %s" % locale)
		return

	overlay.call("Pulse")
	overlay.call("HideOverlay")
	if overlay.visible:
		_fail("HideOverlay left the tutorial overlay visible")
		return

	overlay.queue_free()
	await process_frame
	await process_frame


func _rect_fits(rect: Rect2, viewport_size: Vector2i) -> bool:
	return rect.position.x >= -TOLERANCE \
		and rect.position.y >= -TOLERANCE \
		and rect.end.x <= viewport_size.x + TOLERANCE \
		and rect.end.y <= viewport_size.y + TOLERANCE


func _audit_board_integration() -> void:
	TranslationServer.set_locale("en")
	root.size = Vector2i(1280, 720)
	var packed_scene := load(BOARD_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Missing adventure tutorial board scene")
		return

	var board := packed_scene.instantiate()
	root.add_child(board)
	var overlay: Control = null
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		overlay = board.get_node_or_null("AdventureTutorialOverlay") as Control
		if overlay != null and overlay.visible:
			break
	if overlay == null or not overlay.visible:
		_fail("Tutorial board did not show the extracted overlay")
		return

	var blocker := overlay.get_node_or_null("AdventureTutorialInputBlocker") as ColorRect
	var label := overlay.get_node_or_null("AdventureTutorialCallout/AdventureTutorialText") as Label
	var button := overlay.get_node_or_null("AdventureTutorialCallout/AdventureTutorialContinueButton") as Button
	if blocker == null or label == null or button == null:
		_fail("Tutorial board overlay paths changed")
		return
	if not blocker.visible or blocker.mouse_filter != Control.MOUSE_FILTER_STOP:
		_fail("Tutorial intro no longer blocks board input")
		return

	var tween_count_before := int(board.call("DebugGetBoardTrackedTweenCount"))
	overlay.call("Pulse")
	var tween_count_after := int(board.call("DebugGetBoardTrackedTweenCount"))
	if tween_count_after <= tween_count_before:
		_fail("Tutorial pulse was not registered with BoardAsyncLifetime")
		return

	var intro_text := label.text
	await create_timer(1.3).timeout
	if button.disabled:
		_fail("Tutorial Continue button did not unlock after the reading delay")
		return
	button.emit_signal("pressed")
	await process_frame
	await process_frame
	if label.text.is_empty() or label.text == intro_text:
		_fail("Tutorial Continue signal did not advance the board phase")
		return

	overlay.call("Pulse")
	board.queue_free()
	for _frame in range(4):
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_TUTORIAL_OVERLAY_LOCALIZATION_LAYOUT_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	for _frame in range(32):
		await process_frame
