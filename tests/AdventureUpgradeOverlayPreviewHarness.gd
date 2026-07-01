extends Node

const GAME_BOARD_SCENE := preload("res://scenes/game_board_scene.tscn")


func _ready() -> void:
	var board := GAME_BOARD_SCENE.instantiate()
	board.name = "PreviewBoard"
	board.set("player_count", 2)
	board.set("item_mode", true)
	board.set("winning_score", 100)
	board.set("solo_mode", true)
	board.set("adventure_mode", true)
	board.set("adventure_enemy_avatar_id", "shrimp")
	add_child(board)
