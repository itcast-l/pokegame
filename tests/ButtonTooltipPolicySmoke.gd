extends SceneTree

const SUCCESS_MARKER := "BUTTON_TOOLTIP_POLICY_SMOKE_OK"
const FAILURE_MARKER := "BUTTON_TOOLTIP_POLICY_SMOKE_FAIL"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null:
		_fail("GameAudio autoload is unavailable")
		return

	var host := Control.new()
	host.name = "ButtonTooltipPolicySmokeHost"
	root.add_child(host)
	await process_frame

	var buttons: Array[BaseButton] = [
		Button.new(),
		TextureButton.new(),
		CheckButton.new(),
		OptionButton.new(),
	]
	for index in range(buttons.size()):
		var button := buttons[index]
		button.name = "PolicyButton%d" % index
		button.tooltip_text = "initial tooltip"
		button.shortcut_in_tooltip = true
		host.add_child(button)

	await process_frame
	await process_frame
	for button in buttons:
		if not button.tooltip_text.is_empty():
			_fail("%s retained its initial tooltip" % button.name)
			return
		if button.shortcut_in_tooltip:
			_fail("%s retained shortcut tooltip generation" % button.name)
			return

		button.tooltip_text = "late tooltip"
		button.shortcut_in_tooltip = true
		button.emit_signal("mouse_entered")
		if not button.tooltip_text.is_empty() or button.shortcut_in_tooltip:
			_fail("%s exposed a tooltip after a late assignment" % button.name)
			return

	print(SUCCESS_MARKER)
	host.queue_free()
	await process_frame
	if game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	quit(0)


func _fail(message: String) -> void:
	push_error("%s: %s" % [FAILURE_MARKER, message])
	quit(1)
