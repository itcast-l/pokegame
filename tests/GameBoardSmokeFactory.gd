class_name GameBoardSmokeFactory
extends RefCounted

const HARNESS_SCRIPT := preload("res://scripts/GameBoardTestHarness.cs")


static func create_local(
		packed_scene: PackedScene,
		player_count := 2,
		item_mode := true,
		winning_score := 100,
		player_name := "") -> Node:
	return _harness().call(
		"CreateLocal",
		packed_scene,
		player_count,
		item_mode,
		winning_score,
		player_name)


static func create_solo(
		packed_scene: PackedScene,
		player_count := 2,
		item_mode := true,
		winning_score := 100,
		difficulty := 1,
		player_name := "") -> Node:
	return _harness().call(
		"CreateSolo",
		packed_scene,
		player_count,
		item_mode,
		winning_score,
		difficulty,
		player_name)


static func create_adventure(
		packed_scene: PackedScene,
		enemy_avatar_id: String,
		winning_score := 100,
		item_mode := true,
		difficulty := 1,
		player_name := "") -> Node:
	return _harness().call(
		"CreateAdventure",
		packed_scene,
		enemy_avatar_id,
		winning_score,
		item_mode,
		difficulty,
		player_name)


static func create_tutorial(
		packed_scene: PackedScene,
		enemy_avatar_id: String,
		winning_score := 100,
		item_mode := true,
		difficulty := 1,
		player_name := "") -> Node:
	return _harness().call(
		"CreateTutorial",
		packed_scene,
		enemy_avatar_id,
		winning_score,
		item_mode,
		difficulty,
		player_name)


static func create_network(
		packed_scene: PackedScene,
		player_count: int,
		item_mode: bool,
		winning_score: int,
		session_id: String,
		match_id: String,
		match_seed: int,
		local_player_index: int,
		player_names_csv := "",
		player_avatar_ids_csv := "",
		wait_for_initialize := true) -> Node:
	return _harness().call(
		"CreateNetwork",
		packed_scene,
		player_count,
		item_mode,
		winning_score,
		session_id,
		match_id,
		match_seed,
		local_player_index,
		player_names_csv,
		player_avatar_ids_csv,
		wait_for_initialize)


static func defer_initialization(board: Node) -> bool:
	return bool(_harness().call("ConfigureDeferredInitialization", board))


static func configure_recording(board: Node, setup_id: String) -> bool:
	return bool(_harness().call("ConfigureRecording", board, setup_id))


static func configure_performance_trace(board: Node, minimum_milliseconds: float) -> bool:
	return bool(_harness().call("ConfigurePerformanceTrace", board, minimum_milliseconds))


static func get_player_count(board: Node) -> int:
	return int(_harness().call("GetPlayerCount", board))


static func is_adventure_tutorial(board: Node) -> bool:
	return bool(_harness().call("IsAdventureTutorial", board))


static func get_adventure_enemy_avatar_id(board: Node) -> String:
	return String(_harness().call("GetAdventureEnemyAvatarId", board))


static func _harness() -> RefCounted:
	return HARNESS_SCRIPT.new()
