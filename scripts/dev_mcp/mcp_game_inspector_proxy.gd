extends Node

const SERVICE_PATH := "res://addons/godot_mcp/mcp_game_inspector_service.gd"

func _ready() -> void:
	if not OS.has_feature("editor"):
		return
	var service_script := load(SERVICE_PATH) as Script
	if service_script == null:
		push_warning("[MCP] Game inspector service is unavailable")
		return
	add_child(service_script.new())
