extends SceneTree

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var network := root.get_node_or_null("NetworkManager")
	var board_scene := load(BOARD_SCENE_PATH) as PackedScene
	if network == null or board_scene == null:
		_fail("NetworkManager or game board scene is unavailable")
		return
	var registrations_before := int(network.call("DebugGetMatchEndpointRegistrationCount"))
	var replacements_before := int(network.call("DebugGetMatchEndpointReplacementCount"))
	var stale_before := int(network.call("DebugGetStaleMatchEndpointUnregistrationCount"))

	var old_board := _instantiate_network_board(board_scene)
	await _wait_frames(3)
	if not bool(network.call("DebugHasActiveMatchEndpointRegistration")):
		_fail("First network board did not register an endpoint")
		return
	var replacement_board := _instantiate_network_board(board_scene)
	await _wait_frames(3)
	if int(network.call("DebugGetMatchEndpointReplacementCount")) != replacements_before + 1:
		_fail("Replacement board did not replace the active endpoint")
		return
	old_board.queue_free()
	await _wait_frames(3)
	if not bool(network.call("DebugHasActiveMatchEndpointRegistration")):
		_fail("Stale board exit unregistered the replacement endpoint")
		return
	if int(network.call("DebugGetStaleMatchEndpointUnregistrationCount")) != stale_before + 1:
		_fail("Stale endpoint unregistration was not diagnosed")
		return
	replacement_board.queue_free()
	await _wait_frames(3)
	if bool(network.call("DebugHasActiveMatchEndpointRegistration")):
		_fail("Final board exit left an active endpoint")
		return

	for _index in range(3):
		var board := _instantiate_network_board(board_scene)
		await process_frame
		board.queue_free()
		await _wait_frames(2)
		if bool(network.call("DebugHasActiveMatchEndpointRegistration")):
			_fail("Rapid scene switch left a stale active endpoint")
			return
	if int(network.call("DebugGetMatchEndpointRegistrationCount")) != registrations_before + 5:
		_fail("Unexpected endpoint registration count after rapid switches")
		return
	await _dispose_audio_before_quit()
	print("NETWORK_ENDPOINT_RAPID_SCENE_SWITCH_SMOKE_OK")
	quit(0)


func _instantiate_network_board(board_scene: PackedScene) -> Node:
	var board := board_scene.instantiate()
	board.set("network_session_id", "rapid-scene-session")
	board.set("network_match_id", "rapid-scene-match")
	board.set("network_wait_for_initialize", true)
	root.add_child(board)
	return board


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio != null and game_audio.has_method("DisposeAllAudioImmediately"):
		game_audio.call("DisposeAllAudioImmediately")
		await _wait_frames(3)


func _fail(message: String) -> void:
	push_error(message)
	print("NETWORK_ENDPOINT_RAPID_SCENE_SWITCH_SMOKE_FAIL %s" % message)
	quit(1)
