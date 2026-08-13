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

	if not bool(harness.call("ResetProgress")):
		_fail("Could not reset active profile adventure progress")
		return

	var stage_count := int(harness.call("GetStageCount"))
	if stage_count != 12:
		_fail("Expected exactly twelve main adventure stages")
		return
	_assert_locked(harness, 0, "stage zero on a clean profile")
	_assert_unlocked(harness, 1, "stage one on a clean profile")
	_assert_locked(harness, 2, "stage two on a clean profile")
	_assert_locked(harness, stage_count + 1, "stage count plus one on a clean profile")
	if _failed:
		return

	if not bool(harness.call("RecordTutorialVictory", "fish")):
		_fail("Could not complete the fish tutorial")
		return
	if not bool(harness.call("IsTutorialCompleted", "fish")) \
		or bool(harness.call("IsTutorialCompleted", "lanternfish")) \
		or int(harness.call("GetHighestCompletedMainStage")) != 0:
		_fail("Fish tutorial completion leaked into the other tutorial or main progress")
		return
	if not bool(harness.call("RecordTutorialVictory", "lanternfish")):
		_fail("Could not complete the lanternfish tutorial")
		return
	if not bool(harness.call("IsTutorialCompleted", "fish")) \
		or not bool(harness.call("IsTutorialCompleted", "lanternfish")) \
		or int(harness.call("GetHighestCompletedMainStage")) != 0:
		_fail("Lanternfish tutorial completion did not remain independent")
		return

	if bool(harness.call("RecordStageVictory", 2)):
		_fail("A clean main profile accepted a victory from a locked later stage")
		return
	_assert_locked(harness, 2, "stage two after rejecting a locked stage three victory")
	if _failed:
		return

	for stage_number in range(1, stage_count + 1):
		if not bool(harness.call("RecordStageVictory", stage_number)):
			_fail("Could not record sequential main stage %d victory" % stage_number)
			return
		_assert_unlocked(harness, stage_number, "completed sequential main stage")
		if stage_number < stage_count:
			_assert_unlocked(harness, stage_number + 1, "next sequential main stage")
			_assert_locked(harness, stage_number + 2, "stage beyond the next sequential main stage")
		if _failed:
			return

	if bool(harness.call("TryStartMenuStage", stage_count + 1)):
		_fail("Menu accepted an out-of-range main stage")
		return

	if not bool(harness.call("NetworkStandardLeavesResidualAdventureRunUntouched")):
		_fail("NetworkStandard touched a residual adventure run")
		return
	_assert_locked(harness, 0, "stage zero after full completion")
	for stage_number in range(1, stage_count + 1):
		_assert_unlocked(harness, stage_number, "valid stage after full completion")
	_assert_locked(harness, stage_count + 1, "stage count plus one after full completion")
	if _failed:
		return

	harness.queue_free()
	harness = null
	for _frame in 6:
		await process_frame
	var cleanup_script := load("res://tests/ManagedTestCleanupHarness.cs") as Script
	var cleanup_harness: RefCounted = cleanup_script.new() as RefCounted if cleanup_script != null else null
	if cleanup_harness == null:
		_fail("Managed test cleanup harness is unavailable")
		return
	cleanup_harness.call("DrainFinalizers")
	cleanup_harness = null
	cleanup_script = null
	for _frame in 3:
		await process_frame
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
