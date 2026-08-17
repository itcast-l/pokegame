extends Node

const GameBoardSmokeFactory := preload("res://tests/GameBoardSmokeFactory.gd")

const GAME_BOARD_SCENE := preload("res://scenes/game_board_scene.tscn")


func _ready() -> void:
	var board := GameBoardSmokeFactory.create_adventure(GAME_BOARD_SCENE, "shrimp")
	board.name = "PreviewBoard"
	add_child(board)
