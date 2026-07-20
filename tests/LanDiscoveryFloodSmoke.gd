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

	network.call("HostLan", 2, false, 50, "DiscoveryFloodSmoke", "")
	await process_frame
	if int(network.get("State")) != 4:
		_fail("LAN host did not enter Lobby")
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

	var process_frames := 0
	for _frame in range(60):
		await process_frame
		process_frames += 1
		var processed := int(network.call("DebugGetLanDiscoveryLastResponderProcessedPacketCount"))
		if processed > PACKET_BUDGET:
			_fail("Responder exceeded the per-frame packet budget: %d" % processed)
			return
		if int(network.call("DebugGetLanDiscoveryResponderBudgetExhaustionCount")) > 0 \
				and int(network.call("DebugGetLanDiscoveryRateLimitedSourceCount")) > 0 \
				and process_frames >= 3:
			break

	var maximum_processed := int(network.call("DebugGetLanDiscoveryMaximumResponderProcessedPacketCount"))
	if maximum_processed <= 0 or maximum_processed > PACKET_BUDGET:
		_fail("Responder maximum packet count is outside bounds: %d" % maximum_processed)
		return
	if int(network.call("DebugGetLanDiscoveryResponderBudgetExhaustionCount")) <= 0:
		_fail("Flood did not leave packets queued after exhausting one frame budget")
		return
	if int(network.call("DebugGetLanDiscoveryRateLimitedSourceCount")) <= 0:
		_fail("Flood source did not exercise the reply token bucket")
		return
	if process_frames < 2:
		_fail("Flood completed without advancing multiple process frames")
		return

	sender.close()
	network.call("LeaveSession")
	await process_frame
	await process_frame
	print("LAN_DISCOVERY_FLOOD_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	push_error(message)
	quit(1)
