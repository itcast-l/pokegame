extends SceneTree

class FakeSteam:
	extends RefCounted

	var callback_count := 0
	var networking_callback_count := 0

	func run_callbacks() -> void:
		callback_count += 1

	func runNetworkingCallbacks() -> void:
		networking_callback_count += 1


var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var manager_script := load("res://scripts/SteamManager.gd") as GDScript
	if manager_script == null:
		_fail("Failed to load SteamManager.gd")
		return
	var manager := manager_script.new() as Node
	root.add_child(manager)
	await process_frame
	manager.set_process(false)

	var fake := FakeSteam.new()
	manager.set("_steam", fake)
	manager.call("_cache_capabilities")
	manager.set("is_available", true)
	manager.set("is_initialized", true)
	var capability_queries_before := int(manager.get("capability_query_count"))
	if capability_queries_before <= 0:
		_fail("Steam capability cache did not query its fixed method list")
		return
	manager.set_process(true)
	for _frame in range(600):
		await process_frame
	manager.set_process(false)

	if int(manager.get("capability_query_count")) != capability_queries_before:
		_fail("Steam capability query count grew during 600 process frames")
		return
	if fake.callback_count < 600 or fake.networking_callback_count < 600:
		_fail("Steam callback pumps did not run throughout the 600-frame probe")
		return
	if int(manager.get("process_call_count")) < 600:
		_fail("SteamManager did not receive the expected process callbacks")
		return

	manager.set("is_initialized", false)
	manager.queue_free()
	await process_frame
	print("STEAM_CAPABILITY_CACHE_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("STEAM_CAPABILITY_CACHE_SMOKE_FAIL %s" % message)
	quit(1)
