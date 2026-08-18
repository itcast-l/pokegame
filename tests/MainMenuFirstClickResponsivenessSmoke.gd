extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const MENU_LAYOUT_PATH := "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout"
const PLAYER_COUNT_ROOT_PATH := "PlayerCountDialogLayer/PlayerCountDialogRoot"
const SETTINGS_ROOT_PATH := "SettingsDialogLayer/SettingsDialogRoot"
const EXIT_ROOT_PATH := "ExitDialogLayer/ExitDialogRoot"
const AVATAR_LAYER_PATH := "AvatarEditDialogLayer"
const ADVENTURE_ROOT_PATH := "AdventureTransitionRoot"
const SKILL_ROOT_PATH := "MainMenuSkillCodexOverlay"
const VIEWPORT_SIZE := Vector2i(1280, 720)
const WAIT_FRAMES := 180

const PRIMARY_ACTIONS := [
	{"name": "single_player", "source": "settings", "frame": "SinglePlayerFrame", "result": PLAYER_COUNT_ROOT_PATH},
	{"name": "adventure", "source": "skill_codex", "frame": "AdventureFrame", "result": ADVENTURE_ROOT_PATH},
	{"name": "skill_codex", "source": "exit", "frame": "SkillCodexFrame", "result": SKILL_ROOT_PATH},
	{"name": "settings", "source": "player_count", "frame": "SettingsFrame", "result": SETTINGS_ROOT_PATH},
	{"name": "exit", "source": "avatar", "frame": "ExitFrame", "result": EXIT_ROOT_PATH},
	{"name": "avatar", "source": "adventure", "path": "AvatarEntryLayer/MainMenuAvatarEntry/Button", "result": AVATAR_LAYER_PATH},
	{"name": "multiplayer", "source": "settings", "frame": "MultiplayerFrame", "result": "scene_change"},
]

const MAIN_ROW_FRAMES := [
	"SinglePlayerFrame",
	"AdventureFrame",
	"MultiplayerFrame",
	"SkillCodexFrame",
	"SettingsFrame",
	"ExitFrame",
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	root.size = VIEWPORT_SIZE
	if not await _verify_probe_lock_does_not_block_first_click():
		return
	if not await _verify_initial_adventure_return_first_click():
		return
	if not await _verify_full_row_hit_regions():
		return
	if not await _verify_fast_pointer_press_activation():
		return
	for action in PRIMARY_ACTIONS:
		if not await _verify_return_animation_first_click(action):
			return

	print("MAIN_MENU_FIRST_CLICK_RESPONSIVENESS_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _verify_probe_lock_does_not_block_first_click() -> bool:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load main menu scene")
		return false
	var menu := packed_scene.instantiate()
	if not bool(menu.call("DebugHoldPendingAvatarUnlockProbeLock")):
		_fail("Failed to hold the serialized profile write lock before menu startup")
		return false
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(4)

	if not bool(menu.call("DebugIsPendingAvatarUnlockProbeActive")):
		_fail("Pending avatar unlock probe did not wait behind the held write lock")
		menu.call("DebugReleasePendingAvatarUnlockProbeLock")
		return false
	if not _all_primary_buttons_enabled(menu):
		_fail("Pending avatar unlock probe disabled a visible primary-menu entry")
		menu.call("DebugReleasePendingAvatarUnlockProbeLock")
		return false

	var single_player_button := _get_primary_button(menu, "SinglePlayerFrame")
	if single_player_button == null or not await _click_button(single_player_button):
		menu.call("DebugReleasePendingAvatarUnlockProbeLock")
		return false
	var player_count_root := menu.get_node_or_null(PLAYER_COUNT_ROOT_PATH) as Control
	if player_count_root == null or not await _wait_for_visible(player_count_root):
		_fail("One pointer press and release did not open single-player while the probe was waiting")
		menu.call("DebugReleasePendingAvatarUnlockProbeLock")
		return false

	menu.call("DebugReleasePendingAvatarUnlockProbeLock")
	await _dispose_menu(menu)
	return not _failed


func _verify_initial_adventure_return_first_click() -> bool:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load main menu scene for adventure return")
		return false
	var menu := packed_scene.instantiate()
	menu.call("PrepareAdventureReturnTransition")
	root.add_child(menu)
	current_scene = menu
	if not _all_primary_buttons_enabled(menu) or not bool(menu.call("DebugIsMainMenuAvatarVisible")):
		_fail("Adventure return kept a visible primary entry locked for its first frame")
		return false

	var avatar_button := menu.get_node_or_null("AvatarEntryLayer/MainMenuAvatarEntry/Button") as Button
	if avatar_button == null or not await _click_button(avatar_button):
		return false
	var avatar_root := await _wait_for_node(menu, "AvatarEditDialogLayer/CharacterAvatarEditScene")
	if avatar_root == null:
		_fail("Adventure return did not accept the first avatar click")
		return false

	await _dispose_menu(menu)
	return not _failed


func _verify_full_row_hit_regions() -> bool:
	var menu := await _instantiate_menu()
	if menu == null:
		return false

	for frame_name in MAIN_ROW_FRAMES:
		var frame := menu.get_node_or_null(NodePath("%s/%s" % [MENU_LAYOUT_PATH, frame_name])) as Control
		var button := _get_primary_button(menu, frame_name)
		if frame == null or button == null:
			_fail("Missing main-menu row controls for %s" % frame_name)
			return false
		if not _rect_is_equal_approx(frame.get_global_rect(), button.get_global_rect()):
			_fail("Primary button does not fill its complete row: %s" % frame_name)
			return false
		if not (frame.get("VisualInset") as Vector2).is_equal_approx(Vector2(8.0, 5.0)):
			_fail("Primary row lost its visual plaque inset: %s" % frame_name)
			return false

		var rect := frame.get_global_rect()
		var sample_points := [
			rect.get_center(),
			rect.position + Vector2(1.0, 1.0),
			Vector2(rect.end.x - 1.0, rect.position.y + 1.0),
			Vector2(rect.position.x + 1.0, rect.end.y - 1.0),
			rect.end - Vector2(1.0, 1.0),
			Vector2(rect.get_center().x, rect.position.y + 1.0),
			Vector2(rect.get_center().x, rect.end.y - 1.0),
			Vector2(rect.position.x + 1.0, rect.get_center().y),
			Vector2(rect.end.x - 1.0, rect.get_center().y),
		]
		for point in sample_points:
			await _move_pointer(point)
			if not button.is_hovered():
				_fail("Primary row missed an interior hit point: %s at %s" % [frame_name, point])
				return false

		await _move_pointer(Vector2(rect.position.x - 2.0, rect.get_center().y))
		if button.is_hovered():
			_fail("Primary row accepted a point outside its left edge: %s" % frame_name)
			return false
		await _move_pointer(Vector2(rect.end.x + 2.0, rect.get_center().y))
		if button.is_hovered():
			_fail("Primary row accepted a point outside its right edge: %s" % frame_name)
			return false

	await _dispose_menu(menu)
	return not _failed


func _verify_fast_pointer_press_activation() -> bool:
	var menu := await _instantiate_menu()
	if menu == null:
		return false

	for frame_name in MAIN_ROW_FRAMES:
		var primary_button := _get_primary_button(menu, frame_name)
		if primary_button == null or primary_button.action_mode != BaseButton.ACTION_MODE_BUTTON_PRESS:
			_fail("Primary button does not activate on pointer press: %s" % frame_name)
			return false
	var avatar_button := menu.get_node_or_null("AvatarEntryLayer/MainMenuAvatarEntry/Button") as Button
	if avatar_button == null or avatar_button.action_mode != BaseButton.ACTION_MODE_BUTTON_PRESS:
		_fail("Avatar entry does not activate on pointer press")
		return false

	var settings_button := _get_primary_button(menu, "SettingsFrame")
	var settings_root := menu.get_node_or_null(SETTINGS_ROOT_PATH) as Control
	if settings_button == null or settings_root == null:
		_fail("Missing settings controls for fast pointer activation")
		return false
	var button_rect := _get_canvas_rect(settings_button)
	var outside_position := Vector2(button_rect.end.x + 24.0, button_rect.get_center().y)
	await _move_pointer(outside_position)

	var press_position := button_rect.get_center()
	_push_pointer_motion(press_position)
	_push_pointer_button(press_position, true)
	await process_frame
	if not settings_root.visible:
		_fail("Fast pointer entry did not activate settings on mouse-down")
		return false

	_push_pointer_motion(outside_position)
	_push_pointer_button(outside_position, false)
	await process_frame
	await _dispose_menu(menu)
	return not _failed


func _verify_return_animation_first_click(action: Dictionary) -> bool:
	var menu := await _instantiate_menu()
	if menu == null:
		return false

	var source_root := await _begin_source_return(menu, action["source"])
	if source_root == null:
		_fail("Return-animation source did not open for %s from %s" % [action["name"], action["source"]])
		return false

	var target_button: Button
	if action.has("frame"):
		target_button = _get_primary_button(menu, action["frame"])
	else:
		target_button = menu.get_node_or_null(action["path"]) as Button
	if target_button == null or target_button.disabled:
		_fail("Primary target was not available during return fade: %s" % action["name"])
		return false
	if not await _click_button(target_button):
		return false

	if not await _wait_for_action_result(menu, action):
		_fail("One pointer press and release did not start %s during return fade; state=%s" % [
			action["name"],
			menu.call("DebugGetPrimaryMenuInteractionState") if is_instance_valid(menu) else "freed",
		])
		return false
	if is_instance_valid(source_root) and _is_source_visible(menu, action["source"], source_root):
		_fail("Old return overlay remained visible after starting %s" % action["name"])
		return false

	await _wait_frames(24)
	if action["result"] != "scene_change" and not _is_action_result_visible(menu, action):
		_fail("A stale return callback replaced or hid %s" % action["name"])
		return false

	if action["result"] == "scene_change":
		await _dispose_current_scene()
	else:
		await _dispose_menu(menu)
	return not _failed


func _begin_source_return(menu: Node, source: String) -> Node:
	var source_root: Node
	var open_button: Button
	var close_button: Button
	match source:
		"settings":
			open_button = _get_primary_button(menu, "SettingsFrame")
			source_root = menu.get_node_or_null(SETTINGS_ROOT_PATH)
			if open_button == null or source_root == null:
				return null
			open_button.emit_signal("pressed")
			close_button = source_root.find_child("DoneButton", true, false) as Button
		"exit":
			open_button = _get_primary_button(menu, "ExitFrame")
			source_root = menu.get_node_or_null(EXIT_ROOT_PATH)
			if open_button == null or source_root == null:
				return null
			open_button.emit_signal("pressed")
			close_button = source_root.find_child("CancelButton", true, false) as Button
		"player_count":
			open_button = _get_primary_button(menu, "SinglePlayerFrame")
			source_root = menu.get_node_or_null(PLAYER_COUNT_ROOT_PATH)
			if open_button == null or source_root == null:
				return null
			open_button.emit_signal("pressed")
			close_button = source_root.find_child("BackButton", true, false) as Button
		"avatar":
			open_button = menu.get_node_or_null("AvatarEntryLayer/MainMenuAvatarEntry/Button") as Button
			if open_button == null:
				return null
			open_button.emit_signal("pressed")
			source_root = await _wait_for_node(menu, "AvatarEditDialogLayer/CharacterAvatarEditScene")
			if source_root == null:
				return null
			close_button = source_root.find_child("BackButton", true, false) as Button
		"skill_codex":
			open_button = _get_primary_button(menu, "SkillCodexFrame")
			if open_button == null:
				return null
			open_button.emit_signal("pressed")
			source_root = menu.get_node_or_null(SKILL_ROOT_PATH)
			for _frame in WAIT_FRAMES:
				if bool(menu.call("DebugIsSkillCodexVisible")) \
						and not bool(menu.call("DebugIsSkillCodexTransitioning")):
					break
				await process_frame
			if source_root == null or not bool(menu.call("DebugIsSkillCodexVisible")):
				return null
			menu.call("DebugHandleSkillCodexCancel")
			return source_root
		"adventure":
			open_button = _get_primary_button(menu, "AdventureFrame")
			source_root = menu.get_node_or_null(ADVENTURE_ROOT_PATH)
			if open_button == null or source_root == null:
				return null
			open_button.emit_signal("pressed")
			if not await _wait_for_node_visible(source_root):
				return null
			close_button = source_root.find_child("BackButton", true, false) as Button
		_:
			return null

	if close_button == null or not await _wait_for_node_visible(source_root):
		return null
	await _wait_frames(36)
	close_button.emit_signal("pressed")
	return source_root


func _is_source_visible(menu: Node, source: String, source_root: Node) -> bool:
	if not is_instance_valid(source_root):
		return false
	if source == "skill_codex":
		return bool(menu.call("DebugIsSkillCodexVisible"))
	return source_root is CanvasItem and (source_root as CanvasItem).visible


func _instantiate_menu() -> Node:
	var packed_scene := load(MAIN_MENU_SCENE_PATH) as PackedScene
	if packed_scene == null:
		_fail("Failed to load main menu scene")
		return null
	var menu := packed_scene.instantiate()
	root.add_child(menu)
	current_scene = menu
	await _wait_frames(4)
	return menu


func _get_primary_button(menu: Node, frame_name: String) -> Button:
	return menu.get_node_or_null(NodePath("%s/%s/Button" % [MENU_LAYOUT_PATH, frame_name])) as Button


func _all_primary_buttons_enabled(menu: Node) -> bool:
	for frame_name in MAIN_ROW_FRAMES:
		var button := _get_primary_button(menu, frame_name)
		if button == null or button.disabled:
			return false
	var avatar_button := menu.get_node_or_null("AvatarEntryLayer/MainMenuAvatarEntry/Button") as Button
	return avatar_button != null and not avatar_button.disabled


func _click_button(button: Button) -> bool:
	var canvas_rect := _get_canvas_rect(button)
	var position := canvas_rect.get_center()
	var candidate_x := [0.5, 0.85, 0.15, 0.7, 0.3]
	for x_ratio in candidate_x:
		var candidate := Vector2(
			clampf(canvas_rect.position.x + canvas_rect.size.x * x_ratio, 1.0, VIEWPORT_SIZE.x - 2.0),
			clampf(canvas_rect.get_center().y, 1.0, VIEWPORT_SIZE.y - 2.0))
		await _move_pointer(candidate)
		if button.is_hovered():
			position = candidate
			break
	if not button.is_hovered():
		_fail("Pointer did not hover the intended button before press: %s rect=%s position=%s hovered=%s" % [
			button.get_path(),
			canvas_rect,
			position,
			root.gui_get_hovered_control(),
		])
		return false
	var press := InputEventMouseButton.new()
	press.position = position
	press.global_position = position
	press.button_index = MOUSE_BUTTON_LEFT
	press.button_mask = MOUSE_BUTTON_MASK_LEFT
	press.pressed = true
	root.push_input(press)
	await process_frame
	var release := InputEventMouseButton.new()
	release.position = position
	release.global_position = position
	release.button_index = MOUSE_BUTTON_LEFT
	release.button_mask = 0
	release.pressed = false
	root.push_input(release)
	await process_frame
	return not _failed


func _move_pointer(position: Vector2) -> void:
	_push_pointer_motion(position)
	await process_frame


func _push_pointer_motion(position: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = position
	motion.global_position = position
	root.push_input(motion)


func _push_pointer_button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.global_position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.button_mask = MOUSE_BUTTON_MASK_LEFT if pressed else 0
	event.pressed = pressed
	root.push_input(event)


func _wait_for_action_result(menu: Node, action: Dictionary) -> bool:
	for _frame in WAIT_FRAMES:
		if action["result"] == "scene_change":
			if current_scene != null and current_scene != menu:
				return true
		elif _is_action_result_visible(menu, action):
			return true
		await process_frame
	return false


func _is_action_result_visible(menu: Node, action: Dictionary) -> bool:
	if not is_instance_valid(menu):
		return false
	if action["name"] == "skill_codex":
		return bool(menu.call("DebugIsSkillCodexVisible"))
	var result_node := menu.get_node_or_null(action["result"])
	if action["name"] == "avatar":
		return result_node != null
	return result_node is CanvasItem and (result_node as CanvasItem).visible


func _wait_for_visible(control: Control) -> bool:
	for _frame in WAIT_FRAMES:
		if control.visible:
			return true
		await process_frame
	return control.visible


func _wait_for_node_visible(node: Node) -> bool:
	if node is not CanvasItem:
		return false
	var canvas_item := node as CanvasItem
	for _frame in WAIT_FRAMES:
		if canvas_item.visible:
			return true
		await process_frame
	return canvas_item.visible


func _wait_for_node(parent: Node, path: NodePath) -> Node:
	for _frame in WAIT_FRAMES:
		var node := parent.get_node_or_null(path)
		if node != null:
			return node
		await process_frame
	return parent.get_node_or_null(path)


func _dispose_menu(menu: Node) -> void:
	if is_instance_valid(menu):
		menu.call("DebugReleasePendingAvatarUnlockProbeLock")
		if current_scene == menu:
			current_scene = null
		menu.queue_free()
	await _wait_frames(8)


func _dispose_current_scene() -> void:
	var scene := current_scene
	current_scene = null
	if scene != null and is_instance_valid(scene):
		scene.queue_free()
	for child in root.get_children():
		if child.get_node_or_null("CircularMask") != null:
			child.queue_free()
	await _wait_frames(8)


func _rect_is_equal_approx(left: Rect2, right: Rect2) -> bool:
	return left.position.is_equal_approx(right.position) and left.size.is_equal_approx(right.size)


func _get_canvas_rect(control: Control) -> Rect2:
	var canvas_transform := control.get_global_transform_with_canvas()
	var top_left := canvas_transform * Vector2.ZERO
	var bottom_right := canvas_transform * control.size
	return Rect2(top_left, bottom_right - top_left)


func _wait_frames(count: int) -> void:
	for _frame in count:
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("MAIN_MENU_FIRST_CLICK_RESPONSIVENESS_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
