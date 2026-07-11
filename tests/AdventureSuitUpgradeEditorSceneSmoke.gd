extends SceneTree

const SCENE_PATH := "res://scenes/adventure_suit_upgrade_editor_scene.tscn"
const EDITOR_PATH := "Editor"
const ROWS_PATH := "Editor/RowsScroll/RowsContainer"
const EXPECTED_ROW_COUNT := 54
const KEY_EXPECTED_IDS := [
	"hearts_warm_current_supply",
	"hearts_overflow_growth",
	"diamonds_small_insurance",
	"diamonds_final_double",
	"spades_dark_harbor_reveal",
	"clubs_short_branch_return",
	"clubs_crown_canopy",
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	var packed_scene := load(SCENE_PATH) as PackedScene
	_assert(packed_scene != null, "Failed to load direct adventure suit upgrade editor scene")
	if _failed:
		return

	var root_control := packed_scene.instantiate() as Control
	_assert(root_control != null, "Failed to instantiate direct adventure suit upgrade editor scene")
	if _failed:
		return

	root.add_child(root_control)
	await process_frame
	await process_frame

	var editor := root_control.get_node_or_null(EDITOR_PATH) as Control
	_assert(editor != null, "Direct editor scene is missing the embedded adventure suit upgrades editor")
	if _failed:
		return

	var rows_container := root_control.get_node_or_null(ROWS_PATH) as VBoxContainer
	_assert(rows_container != null, "Direct editor scene is missing the rows container")
	if _failed:
		return

	_assert(rows_container.get_child_count() == EXPECTED_ROW_COUNT, "Expected %d upgrade rows, got %d" % [EXPECTED_ROW_COUNT, rows_container.get_child_count()])
	if _failed:
		return

	for expected_id in KEY_EXPECTED_IDS:
		_assert(rows_container.has_node(NodePath(expected_id)), "Missing upgrade row node for %s" % expected_id)
		if _failed:
			return

	print("ADVENTURE_SUIT_UPGRADE_EDITOR_SCENE_SMOKE_OK")
	quit(0)


func _assert(condition: bool, message: String) -> void:
	if condition or _failed:
		return

	_fail(message)


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("ADVENTURE_SUIT_UPGRADE_EDITOR_SCENE_SMOKE_FAIL %s" % message)
	quit(1)
