extends SceneTree

const IDLE_STATE := 0
const SEARCHING_STATE := 1
const LOBBY_STATE := 4
const IDLE_ALLOCATION_LIMIT := 16 * 1024
const LAN_ALLOCATION_LIMIT := 64 * 1024
const OBSERVE_FRAMES := 300

var _network: Node


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	Engine.max_fps = 60
	_network = root.get_node_or_null("NetworkManager")
	if _network == null:
		_fail("NetworkManager autoload is unavailable")
		return
	await _wait_frames(12)
	if not await _audit_idle():
		return
	if not await _audit_searching_and_pause():
		return
	if not await _audit_lobby():
		return
	print("NETWORK_MAINTENANCE_RUNTIME_SMOKE_OK")
	quit(0)


func _audit_idle() -> bool:
	if int(_network.get("State")) != IDLE_STATE:
		_network.call("LeaveSession")
		if not await _wait_until_state(IDLE_STATE, 0.5):
			return false
	if bool(_network.call("DebugIsNetworkMaintenanceSchedulerArmed")):
		_fail("Idle maintenance scheduler remained armed")
		return false
	var wake_baseline := int(_network.call("DebugGetNetworkMaintenanceWakeCount"))
	var allocated_baseline := int(_network.call("DebugGetCurrentThreadAllocatedBytes"))
	await _wait_frames(OBSERVE_FRAMES)
	var allocation_growth := int(_network.call("DebugGetCurrentThreadAllocatedBytes")) - allocated_baseline
	if int(_network.call("DebugGetNetworkMaintenanceWakeCount")) != wake_baseline:
		_fail("Idle NetworkManager woke maintenance work")
		return false
	if allocation_growth > IDLE_ALLOCATION_LIMIT:
		_fail("Idle NetworkManager allocated %d bytes" % allocation_growth)
		return false
	return true


func _audit_searching_and_pause() -> bool:
	_network.call("SearchLan")
	if not await _wait_until_state(SEARCHING_STATE, 1.0):
		return false
	await _wait_frames(8)
	var wake_baseline := int(_network.call("DebugGetNetworkMaintenanceWakeCount"))
	var allocated_baseline := int(_network.call("DebugGetCurrentThreadAllocatedBytes"))
	var started_at := Time.get_ticks_msec() / 1000.0
	await _wait_frames(OBSERVE_FRAMES)
	var elapsed := Time.get_ticks_msec() / 1000.0 - started_at
	var wake_delta := int(_network.call("DebugGetNetworkMaintenanceWakeCount")) - wake_baseline
	var allocation_growth := int(_network.call("DebugGetCurrentThreadAllocatedBytes")) - allocated_baseline
	if elapsed <= 0.0 or float(wake_delta) / elapsed > 20.5:
		_fail("LAN searching maintenance exceeded 20 Hz: wakes=%d elapsed=%.3f" % [wake_delta, elapsed])
		return false
	if allocation_growth > LAN_ALLOCATION_LIMIT:
		_fail("LAN searching allocated %d bytes (tick=%d rearm=%d browser=%d)" % [
			allocation_growth,
			int(_network.call("DebugGetNetworkMaintenanceTickAllocatedBytes")),
			int(_network.call("DebugGetNetworkMaintenanceRearmAllocatedBytes")),
			int(_network.call("DebugGetNetworkMaintenanceTaskAllocatedBytes", 1)),
		])
		return false
	var paused_wake_baseline := int(_network.call("DebugGetNetworkMaintenanceWakeCount"))
	paused = true
	await create_timer(0.25, true).timeout
	paused = false
	if int(_network.call("DebugGetNetworkMaintenanceWakeCount")) <= paused_wake_baseline:
		_fail("Paused SceneTree stopped always-process network maintenance")
		return false
	return _audit_drift()


func _audit_lobby() -> bool:
	_network.call("HostLan", 2, false, 50, "MaintenanceSmoke", "")
	if not await _wait_until_state(LOBBY_STATE, 1.0):
		return false
	await _wait_frames(8)
	var wake_baseline := int(_network.call("DebugGetNetworkMaintenanceWakeCount"))
	var allocated_baseline := int(_network.call("DebugGetCurrentThreadAllocatedBytes"))
	var started_at := Time.get_ticks_msec() / 1000.0
	await _wait_frames(OBSERVE_FRAMES)
	var elapsed := Time.get_ticks_msec() / 1000.0 - started_at
	var wake_delta := int(_network.call("DebugGetNetworkMaintenanceWakeCount")) - wake_baseline
	var allocation_growth := int(_network.call("DebugGetCurrentThreadAllocatedBytes")) - allocated_baseline
	if elapsed <= 0.0 or float(wake_delta) / elapsed > 20.5:
		_fail("LAN lobby maintenance exceeded 20 Hz: wakes=%d elapsed=%.3f" % [wake_delta, elapsed])
		return false
	if allocation_growth > LAN_ALLOCATION_LIMIT:
		_fail("LAN lobby allocated %d bytes (responder=%d latency=%d standby=%d)" % [
			allocation_growth,
			int(_network.call("DebugGetNetworkMaintenanceTaskAllocatedBytes", 0)),
			int(_network.call("DebugGetNetworkMaintenanceTaskAllocatedBytes", 9)),
			int(_network.call("DebugGetNetworkMaintenanceTaskAllocatedBytes", 10)),
		])
		return false
	if not _audit_drift():
		return false
	_network.call("LeaveSession")
	return await _wait_until_state(IDLE_STATE, 0.5)


func _audit_drift() -> bool:
	var drift := float(_network.call("DebugGetNetworkMaintenanceMaximumDriftMilliseconds"))
	if drift > 100.0:
		_fail("Maintenance deadline drift exceeded 100 ms: %.3f" % drift)
		return false
	return true


func _wait_until_state(expected: int, timeout_seconds: float) -> bool:
	var deadline := Time.get_ticks_msec() / 1000.0 + timeout_seconds
	while Time.get_ticks_msec() / 1000.0 < deadline:
		if int(_network.get("State")) == expected:
			return true
		await process_frame
	_fail("Network state did not reach %d; current=%d" % [expected, int(_network.get("State"))])
	return false


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	paused = false
	push_error(message)
	if _network != null:
		_network.call("LeaveSession")
	print("NETWORK_MAINTENANCE_RUNTIME_SMOKE_FAIL %s" % message)
	quit(1)
