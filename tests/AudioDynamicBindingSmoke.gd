extends SceneTree

const STYLE_META := &"game_ui_style_bound"
const DEFERRED_STYLE_META := &"game_ui_deferred_style_bound"
const TOOLTIP_META := &"game_ui_tooltip_suppression_bound"
const BUTTON_AUDIO_META := &"game_audio_ui_bound"
const RANGE_AUDIO_META := &"game_audio_range_audio_bound"

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _wait_frames(3)
	var audio_manager := root.get_node_or_null("GameAudio")
	if audio_manager == null:
		_fail("GameAudio autoload is missing")
		return

	var subtree := VBoxContainer.new()
	subtree.name = "DynamicAudioBindingRoot"
	var nested := VBoxContainer.new()
	nested.name = "Nested"
	var button := Button.new()
	button.name = "DynamicButton"
	button.text = "Dynamic"
	button.tooltip_text = "must be suppressed"
	var slider := HSlider.new()
	slider.name = "DynamicSlider"
	nested.add_child(button)
	nested.add_child(slider)
	subtree.add_child(nested)
	root.add_child(subtree)
	await _wait_frames(3)

	if not _assert_button_bound_once(button):
		return
	if not slider.has_meta(RANGE_AUDIO_META):
		_fail("Dynamic range did not receive its one-time audio binding metadata")
		return
	var button_connections := _capture_button_connection_counts(button)
	var slider_connections := slider.get_signal_connection_list(&"value_changed").size()

	root.remove_child(subtree)
	await process_frame
	root.add_child(subtree)
	await _wait_frames(3)
	if _capture_button_connection_counts(button) != button_connections:
		_fail("Re-adding a dynamic subtree duplicated button signal bindings")
		return
	if slider.get_signal_connection_list(&"value_changed").size() != slider_connections:
		_fail("Re-adding a dynamic subtree duplicated range signal bindings")
		return

	var later_button := Button.new()
	later_button.name = "LaterDynamicButton"
	later_button.text = "Later"
	nested.add_child(later_button)
	await _wait_frames(3)
	if not _assert_button_bound_once(later_button):
		return

	print("AUDIO_DYNAMIC_BINDING_SMOKE_OK")
	subtree.queue_free()
	await process_frame
	quit(0)


func _assert_button_bound_once(button: Button) -> bool:
	for metadata_key in [STYLE_META, DEFERRED_STYLE_META, TOOLTIP_META, BUTTON_AUDIO_META]:
		if not button.has_meta(metadata_key):
			return _fail("Dynamic button is missing metadata %s" % metadata_key)
	if not button.tooltip_text.is_empty() or button.shortcut_in_tooltip:
		return _fail("Dynamic button tooltip suppression was not applied")
	var counts := _capture_button_connection_counts(button)
	if int(counts.get("mouse_entered", 0)) != 2:
		return _fail("Dynamic button expected exactly tooltip and hover mouse bindings")
	if int(counts.get("pressed", 0)) != 1:
		return _fail("Dynamic button expected exactly one pressed audio binding")
	return true


func _capture_button_connection_counts(button: Button) -> Dictionary:
	return {
		"mouse_entered": button.get_signal_connection_list(&"mouse_entered").size(),
		"pressed": button.get_signal_connection_list(&"pressed").size(),
	}


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> bool:
	if not _failed:
		_failed = true
		push_error(message)
		print("AUDIO_DYNAMIC_BINDING_SMOKE_FAIL %s" % message)
		quit(1)
	return false
