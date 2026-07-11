extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const TIMEOUT_SECONDS := 8.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var menu := await _instantiate_menu()
	if menu == null:
		return

	for method_name in [
		"DebugShowAdventureAvatarUnlockPrompt",
		"DebugIsAdventureAvatarUnlockPromptVisible",
		"DebugShowAdventureRunResumeDialog",
		"DebugIsAdventureRunResumeDialogVisible",
		"DebugAreAdventureMenuBackgroundButtonsDisabled",
	]:
		if not menu.has_method(method_name):
			_fail("Missing adventure cancel modal debug hook: %s" % method_name)
			return

	# A deferred focus queued before a modal exists must not steal modal focus.
	menu.call("FocusDefaultControlDeferred")
	if not bool(menu.call("DebugShowAdventureRunResumeDialog")):
		_fail("Failed to show run resume dialog")
		return
	await process_frame
	_assert_run_dialog_state(menu)
	if _failed:
		return

	if not bool(menu.call("DebugShowAdventureAvatarUnlockPrompt", "fish")):
		_fail("Failed to show avatar unlock prompt")
		return
	await process_frame
	_assert_overlap_state(menu)
	if _failed:
		return

	# The lower dialog can also be dismissed by its own normal button while the
	# avatar prompt remains above it. That must restore focus to Confirm instead
	# of the background menu.
	var cancel_button := menu.get_node_or_null("AdventureRunResumeDialog/DialogMotion/Panel/ContentMargin/Layout/ButtonRow/CancelFrame/CancelButton") as Button
	if cancel_button == null:
		_fail("Missing run resume Cancel button")
		return
	cancel_button.emit_signal("pressed")
	await process_frame
	if bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
		_fail("Programmatic run resume hide should close its dialog")
		return
	_assert_avatar_prompt_state(menu)
	if _failed:
		return

	if not bool(menu.call("DebugShowAdventureRunResumeDialog")):
		_fail("Failed to restore run resume dialog beneath avatar prompt")
		return
	await process_frame
	_assert_overlap_state(menu)
	if _failed:
		return

	# Showing the lower dialog again while the avatar prompt owns the top layer
	# must leave focus on the avatar prompt.
	menu.call("DebugShowAdventureRunResumeDialog")
	await process_frame
	_assert_overlap_state(menu)
	if _failed:
		return

	_press_escape()
	await process_frame
	if bool(menu.call("DebugIsAdventureAvatarUnlockPromptVisible")):
		_fail("First Cancel should close only the avatar unlock prompt")
		return
	_assert_run_dialog_state(menu)
	if _failed:
		return

	_press_escape()
	await process_frame
	if bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
		_fail("Second Cancel should close only the run resume dialog")
		return
	if bool(menu.call("DebugAreAdventureMenuBackgroundButtonsDisabled")):
		_fail("Background should be enabled after both modal layers close")
		return

	_press_escape()
	if not await _wait_for_menu_exit(menu):
		return

	print("ADVENTURE_AVATAR_MENU_CANCEL_MODAL_SMOKE_OK")
	quit(0)


func _instantiate_menu() -> Node:
	await process_frame
	var packed_scene := load(MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load adventure avatar menu")
		return null
	var menu := packed_scene.instantiate()
	if menu == null:
		_fail("Failed to instantiate adventure avatar menu")
		return null
	root.add_child(menu)
	current_scene = menu
	await process_frame
	return menu


func _assert_overlap_state(menu: Node) -> void:
	if not bool(menu.call("DebugIsAdventureAvatarUnlockPromptVisible")) \
		or not bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
		_fail("Expected both modal layers to be visible")
		return
	var avatar_prompt := menu.get_node_or_null("AvatarUnlockPromptRoot") as Control
	var run_dialog := menu.get_node_or_null("AdventureRunResumeDialog") as Control
	if avatar_prompt == null or run_dialog == null:
		_fail("Missing modal roots")
		return
	if avatar_prompt.mouse_filter != Control.MOUSE_FILTER_STOP or run_dialog.mouse_filter != Control.MOUSE_FILTER_STOP:
		_fail("Visible modal roots must stop mouse input")
		return
	if avatar_prompt.z_index <= run_dialog.z_index:
		_fail("Avatar prompt must remain above run resume dialog")
		return
	var confirm := avatar_prompt.get_node_or_null("PromptCenter/AvatarUnlockPromptPanel/PromptMargin/PromptLayout/PromptActions/ConfirmFrame/ConfirmButton") as Button
	if confirm == null or not confirm.has_focus():
		_fail("Avatar prompt confirm button should own focus")
		return
	if not bool(menu.call("DebugAreAdventureMenuBackgroundButtonsDisabled")):
		_fail("Background buttons must be disabled while modal layers are visible")


func _assert_avatar_prompt_state(menu: Node) -> void:
	if not bool(menu.call("DebugIsAdventureAvatarUnlockPromptVisible")):
		_fail("Avatar unlock prompt should remain visible over programmatically hidden run dialog")
		return
	var confirm := menu.get_node_or_null("AvatarUnlockPromptRoot/PromptCenter/AvatarUnlockPromptPanel/PromptMargin/PromptLayout/PromptActions/ConfirmFrame/ConfirmButton") as Button
	if confirm == null or not confirm.has_focus():
		_fail("Avatar prompt Confirm should regain focus after run dialog hides")
		return
	if not bool(menu.call("DebugAreAdventureMenuBackgroundButtonsDisabled")):
		_fail("Background buttons must remain disabled under avatar prompt")


func _assert_run_dialog_state(menu: Node) -> void:
	if not bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
		_fail("Expected run resume dialog to remain visible")
		return
	var continue_button := menu.get_node_or_null("AdventureRunResumeDialog/DialogMotion/Panel/ContentMargin/Layout/ButtonRow/ContinueFrame/ContinueButton") as Button
	if continue_button == null or not continue_button.has_focus():
		_fail("Run resume Continue button should own focus")
		return
	if not bool(menu.call("DebugAreAdventureMenuBackgroundButtonsDisabled")):
		_fail("Background buttons must stay disabled under run resume dialog")


func _press_escape() -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_ESCAPE
	event.physical_keycode = KEY_ESCAPE
	event.pressed = true
	root.push_input(event, true)


func _wait_for_menu_exit(menu: Node) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if not is_instance_valid(menu):
			return true
	_fail("Third Cancel did not leave the adventure avatar menu")
	return false


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_AVATAR_MENU_CANCEL_MODAL_SMOKE_FAIL %s" % message)
	quit(1)
