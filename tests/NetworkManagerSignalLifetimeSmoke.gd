extends SceneTree

const NETWORK_MANAGER_SCRIPT_PATH := "res://scripts/NetworkManager.cs"
const MULTIPLAYER_SIGNALS := [
	"peer_connected",
	"peer_disconnected",
	"connected_to_server",
	"connection_failed",
	"server_disconnected",
]
const STEAM_SIGNALS := [
	"steam_ready",
	"lobby_created",
	"lobby_joined",
	"lobby_match_list",
	"join_requested",
	"lobby_data_update",
]


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var steam_manager := root.get_node_or_null("SteamManager")
	if steam_manager == null:
		_fail("SteamManager autoload is unavailable")
		return
	var network_script := load(NETWORK_MANAGER_SCRIPT_PATH)
	if network_script == null:
		_fail("NetworkManager script is unavailable")
		return

	var multiplayer_api := root.multiplayer
	var multiplayer_baseline := _capture_signal_counts(multiplayer_api, MULTIPLAYER_SIGNALS)
	var steam_baseline := _capture_signal_counts(steam_manager, STEAM_SIGNALS)
	var temporary_network := network_script.new() as Node
	temporary_network.name = "NetworkManagerSignalLifetimeProbe"
	root.add_child(temporary_network)
	await _wait_frames(3)

	if not bool(temporary_network.call("DebugAreMultiplayerSignalsConnected")):
		_fail("Temporary NetworkManager did not mark Multiplayer signals connected")
		return
	if not bool(temporary_network.call("DebugAreSteamSignalsConnected")):
		_fail("Temporary NetworkManager did not mark Steam signals connected")
		return
	if not _assert_signal_delta(multiplayer_api, MULTIPLAYER_SIGNALS, multiplayer_baseline, 1, "Multiplayer"):
		return
	if not _assert_signal_delta(steam_manager, STEAM_SIGNALS, steam_baseline, 1, "SteamManager"):
		return

	root.remove_child(temporary_network)
	await process_frame
	if bool(temporary_network.call("DebugAreMultiplayerSignalsConnected")):
		_fail("Temporary NetworkManager retained Multiplayer signal state after exit")
		return
	if bool(temporary_network.call("DebugAreSteamSignalsConnected")):
		_fail("Temporary NetworkManager retained Steam signal state after exit")
		return
	if not _assert_signal_delta(multiplayer_api, MULTIPLAYER_SIGNALS, multiplayer_baseline, 0, "Multiplayer"):
		return
	if not _assert_signal_delta(steam_manager, STEAM_SIGNALS, steam_baseline, 0, "SteamManager"):
		return

	temporary_network.call("_ExitTree")
	await process_frame
	if not _assert_signal_delta(multiplayer_api, MULTIPLAYER_SIGNALS, multiplayer_baseline, 0, "Multiplayer repeat exit"):
		return
	if not _assert_signal_delta(steam_manager, STEAM_SIGNALS, steam_baseline, 0, "SteamManager repeat exit"):
		return
	temporary_network.free()
	print("NETWORK_MANAGER_SIGNAL_LIFETIME_SMOKE_OK")
	quit(0)


func _capture_signal_counts(emitter: Object, signal_names: Array) -> Dictionary:
	var counts := {}
	for signal_name in signal_names:
		counts[signal_name] = emitter.get_signal_connection_list(signal_name).size()
	return counts


func _assert_signal_delta(
		emitter: Object,
		signal_names: Array,
		baseline: Dictionary,
		expected_delta: int,
		label: String) -> bool:
	for signal_name in signal_names:
		var actual := emitter.get_signal_connection_list(signal_name).size()
		var expected := int(baseline[signal_name]) + expected_delta
		if actual != expected:
			_fail("%s signal %s connection count was %d, expected %d" % [
				label,
				signal_name,
				actual,
				expected,
			])
			return false
	return true


func _wait_frames(count: int) -> void:
	for _frame in range(count):
		await process_frame


func _fail(message: String) -> void:
	push_error(message)
	print("NETWORK_MANAGER_SIGNAL_LIFETIME_SMOKE_FAIL %s" % message)
	quit(1)
