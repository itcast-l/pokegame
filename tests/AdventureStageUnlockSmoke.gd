extends SceneTree

const HARNESS_SCRIPT := preload("res://tests/AdventureStageUnlockHarness.cs")

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var harness := HARNESS_SCRIPT.new() as Node
	if harness == null:
		_fail("Could not instantiate adventure stage unlock harness")
		return
	root.add_child(harness)
	await process_frame

	if not bool(harness.call("SetHighestCompletedStage", 0)):
		_fail("Could not reset active profile adventure progress")
		return

	var stage_count := int(harness.call("GetStageCount"))
	if stage_count < 3:
		_fail("Expected at least three adventure stages")
		return
	_assert_locked(harness, 0, "stage zero on a clean profile")
	_assert_unlocked(harness, 1, "stage one on a clean profile")
	_assert_locked(harness, 2, "stage two on a clean profile")
	_assert_locked(harness, stage_count + 1, "stage count plus one on a clean profile")
	if _failed:
		return
	if bool(harness.call("RecordStageVictory", 3)):
		_fail("A clean profile accepted a victory from a locked later stage")
		return
	_assert_locked(harness, 2, "stage two after rejecting a locked stage three victory")
	if _failed:
		return

	if not bool(harness.call("RecordStageVictory", 1)):
		_fail("Could not record stage one victory")
		return
	_assert_unlocked(harness, 1, "completed stage one")
	_assert_unlocked(harness, 2, "next stage after stage one victory")
	_assert_locked(harness, 3, "stage three after stage one victory")
	if _failed:
		return

	if bool(harness.call("TryStartMenuStage", 3)):
		_fail("Menu accepted a locked stage instead of rejecting the start request")
		return

	if not bool(harness.call("SetHighestCompletedStage", stage_count)):
		_fail("Could not seed full adventure completion")
		return
	_assert_locked(harness, 0, "stage zero after full completion")
	for stage_number in range(1, stage_count + 1):
		_assert_unlocked(harness, stage_number, "valid stage after full completion")
	_assert_locked(harness, stage_count + 1, "stage count plus one after full completion")
	if _failed:
		return

	print("ADVENTURE_STAGE_UNLOCK_SMOKE_OK")
	quit(0)


func _assert_unlocked(harness: Node, stage_number: int, context: String) -> void:
	if not bool(harness.call("IsStageUnlocked", stage_number)):
		_fail("Expected %s to be unlocked" % context)


func _assert_locked(harness: Node, stage_number: int, context: String) -> void:
	if bool(harness.call("IsStageUnlocked", stage_number)):
		_fail("Expected %s to be locked" % context)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error("ADVENTURE_STAGE_UNLOCK_SMOKE_FAIL %s" % message)
	quit(1)
