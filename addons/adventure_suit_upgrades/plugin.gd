@tool
extends EditorPlugin

const DOCK_SCENE := preload("res://addons/adventure_suit_upgrades/adventure_suit_upgrades_dock.tscn")

var _dock: Control


func _enter_tree() -> void:
	_dock = DOCK_SCENE.instantiate()
	_dock.name = "Adventure Suit Upgrades"
	add_control_to_dock(DOCK_SLOT_LEFT_UL, _dock)


func _exit_tree() -> void:
	if _dock != null:
		remove_control_from_docks(_dock)
		_dock.queue_free()
		_dock = null
