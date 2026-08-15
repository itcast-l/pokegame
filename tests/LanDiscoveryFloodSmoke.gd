extends SceneTree

const DISCOVERY_PORT := 37145
const PACKET_BUDGET := 32
const FLOOD_PACKET_COUNT := 128


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var network := root.get_node_or_null("NetworkManager")
	if network == null:
		_fail("NetworkManager autoload is unavailable")
		return

	var advertisement_build_baseline := int(network.call("DebugGetLanRoomAdvertisementBuildCount"))
	network.call("HostLan", 2, false, 50, "DiscoveryFloodSmoke", "")
	await process_frame
	if int(network.get("State")) != 4:
		_fail("LAN host did not enter Lobby")
		return
	var advertisement_build_count := int(network.call("DebugGetLanRoomAdvertisementBuildCount"))
	if advertisement_build_count != advertisement_build_baseline + 1:
		_fail("LAN room advertisement was not built exactly once at responder startup")
		return

	var sender := PacketPeerUDP.new()
	var bind_error := sender.bind(0, "127.0.0.1")
	if bind_error != OK:
		_fail("Could not bind UDP flood sender: %s" % error_string(bind_error))
		return
	sender.set_dest_address("127.0.0.1", DISCOVERY_PORT)

	var oversized_packet := PackedByteArray()
	oversized_packet.resize(2048)
	if sender.put_packet(oversized_packet) != OK:
		_fail("Could not send oversized discovery probe")
		return
	if sender.put_packet("not-a-discovery-request".to_utf8_buffer()) != OK:
		_fail("Could not send malformed discovery probe")
		return

	for _frame in range(30):
		await process_frame
		if int(network.call("DebugGetLanDiscoveryDroppedPacketCount")) > 0 \
				and int(network.call("DebugGetLanDiscoveryParseFailureCount")) > 0:
			break
	if int(network.call("DebugGetLanDiscoveryDroppedPacketCount")) <= 0:
		_fail("Oversized discovery packet was not counted")
		return
	if int(network.call("DebugGetLanDiscoveryParseFailureCount")) <= 0:
		_fail("Malformed discovery packet was not counted")
		return

	var request := "deckland_discover".to_utf8_buffer()
	for _packet_index in range(FLOOD_PACKET_COUNT):
		if sender.put_packet(request) != OK:
			_fail("Could not enqueue discovery flood")
			return

	var maintenance_ticks := 0
	var last_responder_execution_count := int(network.call("DebugGetNetworkMaintenanceTaskExecutionCount", 0))
	var migration_latency_execution_count := int(network.call("DebugGetNetworkMaintenanceTaskExecutionCount", 9))
	var hot_standby_execution_count := int(network.call("DebugGetNetworkMaintenanceTaskExecutionCount", 10))
	for _frame in range(60):
		await process_frame
		var responder_execution_count := int(network.call("DebugGetNetworkMaintenanceTaskExecutionCount", 0))
		if responder_execution_count == last_responder_execution_count:
			continue
		maintenance_ticks += responder_execution_count - last_responder_execution_count
		last_responder_execution_count = responder_execution_count
		var processed := int(network.call("DebugGetLanDiscoveryLastResponderProcessedPacketCount"))
		if processed > PACKET_BUDGET:
			_fail("Responder exceeded the per-maintenance-tick packet budget: %d" % processed)
			return
		if int(network.call("DebugGetLanDiscoveryResponderBudgetExhaustionCount")) > 0 \
				and int(network.call("DebugGetLanDiscoveryRateLimitedSourceCount")) > 0 \
				and maintenance_ticks >= 3:
			break

	var maximum_processed := int(network.call("DebugGetLanDiscoveryMaximumResponderProcessedPacketCount"))
	if maximum_processed <= 0 or maximum_processed > PACKET_BUDGET:
		_fail("Responder maximum packet count is outside bounds: %d" % maximum_processed)
		return
	if int(network.call("DebugGetLanDiscoveryResponderBudgetExhaustionCount")) <= 0:
		_fail("Flood did not leave packets queued after exhausting one maintenance-tick budget")
		return
	if int(network.call("DebugGetLanDiscoveryRateLimitedSourceCount")) <= 0:
		_fail("Flood source did not exercise the reply token bucket")
		return
	if maintenance_ticks < 2:
		_fail("Flood completed without advancing multiple maintenance ticks")
		return
	if int(network.call("DebugGetNetworkMaintenanceTaskExecutionCount", 9)) <= migration_latency_execution_count:
		_fail("Flood starved migration-latency maintenance")
		return
	if int(network.call("DebugGetNetworkMaintenanceTaskExecutionCount", 10)) <= hot_standby_execution_count:
		_fail("Flood starved hot-standby maintenance")
		return
	if float(network.call("DebugGetNetworkMaintenanceMaximumTickMilliseconds")) >= 4.0:
		_fail("Flood maintenance tick exceeded the 4 ms budget")
		return
	if int(network.call("DebugGetLanRoomAdvertisementBuildCount")) != advertisement_build_count:
		_fail("Discovery flood rebuilt the cached LAN room advertisement")
		return

	sender.close()
	var close_started_at := Time.get_ticks_msec()
	network.call("LeaveSession")
	while int(network.get("State")) != 0 and Time.get_ticks_msec() - close_started_at < 500:
		await process_frame
	if int(network.get("State")) != 0:
		_fail("Flood starved the session-close deadline")
		return
	if int(network.call("DebugGetLanRoomAdvertisementBuildCount")) != advertisement_build_count:
		_fail("Stopping the responder unexpectedly rebuilt the LAN room advertisement")
		return
	print("LAN_DISCOVERY_FLOOD_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
