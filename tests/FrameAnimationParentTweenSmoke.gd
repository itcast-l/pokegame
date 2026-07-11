extends SceneTree

const BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const AVATAR_SCENE_PATH := "res://scenes/character_avatar.tscn"
const TIMEOUT_SECONDS := 12.0

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _run_avatar_parent_tween_phase()
	if _failed:
		return

	var board := await _instantiate_board()
	if board == null:
		return

	for method_name in [
		"DebugHasFrameAnimationParentTweenProbeFrames",
		"DebugStartSpecialCardTriggerTimelineProbe",
		"DebugGetSpecialCardTriggerProbeElapsedSeconds",
		"DebugGetSpecialCardTriggerProbeScaleX",
		"DebugConfigureTableShuffleOwnershipProbe",
		"DebugStartTableShuffleOwnershipProbe",
		"DebugIsTableShuffleGatherProbeActive",
		"DebugInjectTableShuffleOwnershipLoss",
		"DebugTableShuffleProbeRetainedSentinelState",
		"DebugTableShuffleRenderSentinelIsUnchanged",
		"DebugTableShuffleOriginalHiddenStatesAreRestored",
		"DebugRentTableShuffleVortexReuseSentinel",
		"DebugTableShuffleVortexReuseSentinelIsIntact",
		"DebugReleaseTableShuffleVortexReuseSentinel",
	]:
		if not board.has_method(method_name):
			_fail("Missing U2 debug hook: %s" % method_name)
			return

	if not bool(board.call("DebugHasFrameAnimationParentTweenProbeFrames")):
		_fail("Required frame probe resources are unavailable")
		return

	await _run_special_timeline_phase(board)
	if _failed:
		return

	for caller in ["effect", "adventure"]:
		await _run_table_shuffle_normal_phase(board, caller)
		if _failed:
			return
		await _run_table_shuffle_abort_phase(board, caller, true, true)
		if _failed:
			return
		await _run_table_shuffle_abort_phase(board, caller, false, false)
		if _failed:
			return

	board.queue_free()
	for _frame in range(8):
		await process_frame
	print("FRAME_ANIMATION_PARENT_TWEEN_SMOKE_OK")
	quit(0)


func _run_avatar_parent_tween_phase() -> void:
	var scene := load(AVATAR_SCENE_PATH) as PackedScene
	if scene == null:
		_fail("Failed to load avatar scene")
		return
	var avatar := scene.instantiate()
	root.add_child(avatar)
	await process_frame
	if not avatar.has_method("PlayHit") or not avatar.has_method("PlayVictory"):
		_fail("Avatar is missing feedback actions")
		return
	avatar.call("PlayHit")
	avatar.call("PlayVictory")
	var feedback := avatar.get_node_or_null("FeedbackFx") as TextureRect
	if feedback == null or feedback.texture == null:
		_fail("Missing victory feedback texture")
		return
	var deadline := Time.get_ticks_msec() / 1000.0 + 0.34
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		var texture_path := feedback.texture.resource_path
		if texture_path.begins_with("res://art/fx/avatar_feedback_hit/"):
			_fail("Canceled hit frame overwrote victory feedback")
			return
		if not texture_path.begins_with("res://art/fx/avatar_feedback_victory/"):
			_fail("Victory feedback left its own frame sequence")
			return
	avatar.queue_free()
	await process_frame


func _run_special_timeline_phase(board: Node) -> void:
	var result := {}
	if not bool(board.call("DebugStartSpecialCardTriggerTimelineProbe", result)):
		_fail("Could not start special-card timeline probe")
		return
	var peak_elapsed := -1.0
	var peak_scale := -1.0
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline and not bool(result.get("completed", false)):
		await process_frame
		var scale_x := float(board.call("DebugGetSpecialCardTriggerProbeScaleX"))
		if scale_x > peak_scale:
			peak_scale = scale_x
			peak_elapsed = float(board.call("DebugGetSpecialCardTriggerProbeElapsedSeconds"))
	if not bool(result.get("completed", false)):
		_fail("Timed out waiting for special-card probe result")
		return
	if String(result.get("error", "")) != "":
		_fail("Special-card probe error: %s" % result)
		return
	if abs(peak_elapsed - 0.3024) > 0.06:
		_fail("Special-card peak changed phase: %.3f" % peak_elapsed)
	elif peak_scale < 1.75:
		_fail("Special-card peak scale was not meaningful: %.3f" % peak_scale)
	elif abs(float(result.get("elapsed_seconds", -1.0)) - 0.72) > 0.08:
		_fail("Special-card duration changed: %s" % result)
	elif float(result.get("elapsed_seconds", -1.0)) <= 0.0:
		_fail("Special-card duration changed: %s" % result)


func _run_table_shuffle_normal_phase(board: Node, caller: String) -> void:
	if not bool(board.call("DebugConfigureTableShuffleOwnershipProbe")):
		_fail("Could not configure %s normal shuffle" % caller)
		return
	var result := {}
	if not bool(board.call("DebugStartTableShuffleOwnershipProbe", caller, result)):
		_fail("Could not start %s normal shuffle" % caller)
		return
	if not await _wait_for_result(board, result):
		return
	if String(result.get("error", "")) != "" or not bool(result.get("caller_succeeded", false)):
		_fail("Normal %s shuffle failed: %s" % [caller, result])
	elif abs(float(board.call("DebugGetTableShuffleOwnershipProbeElapsedSeconds")) - 0.66) > 0.08:
		_fail("Normal %s shuffle duration changed" % caller)
	elif not bool(board.call("DebugTableShuffleOriginalHiddenStatesAreRestored")):
		_fail("Normal %s shuffle did not restore owned hidden states" % caller)


func _run_table_shuffle_abort_phase(board: Node, caller: String, advance_generation: bool, replace_all: bool) -> void:
	if not bool(board.call("DebugConfigureTableShuffleOwnershipProbe")):
		_fail("Could not configure %s abort shuffle" % caller)
		return
	var result := {}
	if not bool(board.call("DebugStartTableShuffleOwnershipProbe", caller, result)):
		_fail("Could not start %s abort shuffle" % caller)
		return
	var injected := false
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline and not bool(result.get("completed", false)):
		await process_frame
		if not injected and bool(board.call("DebugIsTableShuffleGatherProbeActive")):
			injected = bool(board.call("DebugInjectTableShuffleOwnershipLoss", advance_generation, replace_all))
			if not injected:
				_fail("Could not inject %s ownership loss" % caller)
				return
	if not injected:
		_fail("%s ownership-loss window was never active" % caller)
		return
	if not bool(result.get("completed", false)):
		_fail("Timed out waiting for %s abort result" % caller)
		return
	if String(result.get("error", "")) != "" or bool(result.get("caller_succeeded", true)):
		_fail("Aborted %s shuffle unexpectedly succeeded: %s" % [caller, result])
		return
	if not bool(board.call("DebugTableShuffleProbeRetainedSentinelState")) \
		or not bool(board.call("DebugTableShuffleRenderSentinelIsUnchanged")):
		_fail("Aborted %s shuffle overwrote replacement state" % caller)
		return
	if not bool(board.call("DebugRentTableShuffleVortexReuseSentinel")):
		_fail("Could not reuse %s vortex" % caller)
		return
	await create_timer(0.82).timeout
	if not bool(board.call("DebugTableShuffleVortexReuseSentinelIsIntact")):
		_fail("Late %s vortex frame overwrote pooled node" % caller)
		return
	board.call("DebugReleaseTableShuffleVortexReuseSentinel")


func _wait_for_result(board: Node, result: Dictionary, sampler: Callable = Callable()) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + TIMEOUT_SECONDS
	while Time.get_ticks_msec() / 1000.0 < deadline:
		await process_frame
		if sampler.is_valid():
			sampler.call()
		if bool(result.get("completed", false)):
			return true
	_fail("Timed out waiting for probe result")
	return false


func _instantiate_board() -> Node:
	await process_frame
	var scene := load(BOARD_SCENE_PATH) as PackedScene
	if scene == null:
		_fail("Failed to load board scene")
		return null
	var board := scene.instantiate()
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	root.add_child(board)
	await process_frame
	return board


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("FRAME_ANIMATION_PARENT_TWEEN_SMOKE_FAIL %s" % message)
	quit(1)
