extends SceneTree

const IDLE_STATE := 0
const FAILED_STATE := 9
const CONNECTION_CREATION_FAILED_REASON := 3


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var steam_manager := root.get_node_or_null("SteamManager")
	var network := root.get_node_or_null("NetworkManager")
	if steam_manager == null or network == null:
		_fail("SteamManager or NetworkManager autoload is unavailable")
		return
	await _wait_frames(12)
	if bool(steam_manager.get("is_initialized")) or bool(steam_manager.get("is_available")):
		_fail("Steam unexpectedly initialized while DECKLAND_DISABLE_STEAM=1")
		return
	if steam_manager.is_processing():
		_fail("Unavailable SteamManager kept processing")
		return
	if int(steam_manager.get("singleton_lookup_count")) != 1:
		_fail("Steam singleton discovery did not occur exactly once")
		return
	if int(steam_manager.get("capability_query_count")) != 0:
		_fail("Disabled SteamManager queried capabilities")
		return
	var process_count := int(steam_manager.get("process_call_count"))
	await _wait_frames(300)
	if int(steam_manager.get("process_call_count")) != process_count:
		_fail("Disabled SteamManager received process callbacks")
		return

	network.call("HostSteam", 2, false, 50, "NoSteamSmoke", "")
	await _wait_frames(3)
	if int(network.get("State")) != FAILED_STATE:
		_fail("Unavailable Steam host did not fail finitely")
		return
	if int(network.call("DebugGetActiveSessionCloseReason")) != CONNECTION_CREATION_FAILED_REASON:
		_fail("Unavailable Steam host used the wrong close reason")
		return
	network.call("LeaveSession")
	await _wait_frames(3)
	if int(network.get("State")) != IDLE_STATE:
		_fail("No-Steam failure did not return to Idle")
		return
	print("NO_STEAM_PLATFORM_SMOKE_OK")
	quit(0)


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	push_error(message)
	print("NO_STEAM_PLATFORM_SMOKE_FAIL %s" % message)
	quit(1)
