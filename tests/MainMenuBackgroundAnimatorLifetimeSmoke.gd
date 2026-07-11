extends SceneTree

const HARNESS_SCRIPT := preload("res://tests/MainMenuBackgroundAnimatorLifetimeHarness.cs")
const REPEAT_COUNT := 3

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	for iteration in REPEAT_COUNT:
		await _assert_freed_animator_load_does_not_resume(iteration)
		if _failed:
			return

	print("MAIN_MENU_BACKGROUND_ANIMATOR_LIFETIME_SMOKE_OK")
	quit(0)


func _assert_freed_animator_load_does_not_resume(iteration: int) -> void:
	var animator := HARNESS_SCRIPT.new() as Node
	if animator == null:
		_fail("Could not instantiate C# lifetime harness")
		return

	root.add_child(animator)
	if not bool(animator.get("DebugHasEnteredLoadBarrier")):
		_fail("Lifetime harness did not reach its frame barrier on iteration %d" % iteration)
		animator.queue_free()
		return

	animator.queue_free()
	for _frame in 2:
		await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	quit(1)
