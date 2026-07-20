extends SceneTree

const TIMEOUT_SECONDS := 60.0

var _network: Node
var _started := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_network = root.get_node("/root/NetworkManager")
	_network.connect("PlayerListUpdated", Callable(self, "_on_players"))
	await process_frame
	_network.HostLan(2, true, 100, "CodexOpeningDealMigration", "")
	await create_timer(TIMEOUT_SECONDS).timeout
	push_error("Opening-deal migration host was not killed at its fault point")
	_network.LeaveSession()
	quit(1)


func _on_players(players: Array) -> void:
	if _started or players.size() != 2:
		return
	for player in players:
		if not bool(player.get("is_ready", false)):
			return
	_started = true
	_network.StartMatch()
