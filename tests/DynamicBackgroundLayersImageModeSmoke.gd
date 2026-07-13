extends SceneTree

const MAIN_MENU_SCENE_PATH := "res://scenes/main_menu_scene.tscn"
const MULTIPLAYER_LOBBY_SCENE_PATH := "res://scenes/multiplayer_lobby_scene.tscn"
const GAME_BOARD_SCENE_PATH := "res://scenes/game_board_scene.tscn"
const ADVENTURE_MAP_SCENE_PATH := "res://scenes/adventure_map_scene.tscn"
const VIEWPORT_SIZE := Vector2i(1600, 900)
const IMAGE_BASED_RENDER_MODE := 1
const BUBBLE_TEXTURE_PATH := "res://art/fx/environment/main_menu_bubble/run-001/processed/fx-1.png"
const MAIN_MENU_BACKGROUND_ANIMATION := &"main_menu_ocean_background"
const MAIN_MENU_BACKGROUND_FRAME_COUNT := 24
const MAIN_MENU_BACKGROUND_LOAD_TIMEOUT_FRAMES := 120

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame

	await _assert_main_menu_layers()
	if _failed:
		return

	await _assert_multiplayer_lobby_layers()
	if _failed:
		return

	await _assert_game_board_layers()
	if _failed:
		return

	await _assert_adventure_map_layers()
	if _failed:
		return

	print("DYNAMIC_BACKGROUND_LAYERS_IMAGE_MODE_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _assert_main_menu_layers() -> void:
	var scene := await _instantiate_scene(MAIN_MENU_SCENE_PATH)
	if scene.is_empty():
		return

	var main_menu := scene["instance"] as Node
	var background_art := _require_node(main_menu, "BackgroundArt", "main menu animated background")
	var ocean_layer := _require_node(main_menu, "OceanDynamicsLayer", "main menu ocean dynamics layer")
	var ambient_layer := _require_node(main_menu, "UnderwaterAmbientLayer", "main menu underwater ambient layer")
	if _failed:
		_cleanup_scene(scene)
		return

	await _assert_main_menu_background_animation(background_art)
	if _failed:
		_cleanup_scene(scene)
		return

	_assert_image_based(ocean_layer, "main menu ocean dynamics")
	_assert_image_based(ambient_layer, "main menu underwater ambient")
	_assert_layer_has_children(ocean_layer, "SunbeamLayer", "main menu ocean sunbeams")
	_assert_layer_has_children(ocean_layer, "SurfaceWakeLayer", "main menu surface wake")
	_assert_layer_has_children(ocean_layer, "CurrentRibbonLayer", "main menu current ribbons")
	_assert_layer_has_children(ocean_layer, "CausticRibbonLayer", "main menu caustic ribbons")
	_assert_layer_has_children(ocean_layer, "GlintLayer", "main menu glints")
	_assert_layer_has_children(ambient_layer, "SeagrassLayer", "main menu ambient seagrass")
	_assert_layer_has_children_when_enabled(ambient_layer, "SunbeamLayer", "sunbeam_intensity", "main menu ambient sunbeams")
	_assert_layer_has_children(ambient_layer, "WaterCurrentLayer", "main menu ambient water currents")
	_assert_layer_has_children_when_enabled(ambient_layer, "CenterCausticLayer", "center_caustic_intensity", "main menu ambient center caustics")
	_assert_layer_has_children(ambient_layer, "MoteLayer", "main menu ambient motes")
	if _failed:
		_cleanup_scene(scene)
		return

	var bubble_texture := load(BUBBLE_TEXTURE_PATH) as Texture2D
	if bubble_texture != null:
		var bubble_layer := _require_node(ocean_layer, "BubbleLayer", "main menu bubble layer")
		if _failed:
			_cleanup_scene(scene)
			return

		var bubble_nodes := bubble_layer.get_children()
		if bubble_nodes.is_empty():
			_fail("Main menu bubble layer did not create image nodes while bubble texture exists")
			_cleanup_scene(scene)
			return

		var bubble_node := bubble_nodes[0] as Control
		if bubble_node == null:
			_fail("Main menu bubble layer child is not a Control")
			_cleanup_scene(scene)
			return

		await _assert_node_stops_and_resumes(ocean_layer, bubble_node, "main menu ocean bubble image node")
		if _failed:
			_cleanup_scene(scene)
			return

	_cleanup_scene(scene)


func _assert_main_menu_background_animation(background_art: Node) -> void:
	var animation_player := background_art.get_node_or_null("AnimationPlayer") as AnimationPlayer
	for _frame in MAIN_MENU_BACKGROUND_LOAD_TIMEOUT_FRAMES:
		if animation_player != null and animation_player.has_animation(MAIN_MENU_BACKGROUND_ANIMATION):
			break
		await process_frame
		animation_player = background_art.get_node_or_null("AnimationPlayer") as AnimationPlayer

	if animation_player == null:
		_fail("Main menu background did not create AnimationPlayer after loading its 24 frames")
		return
	if not animation_player.has_animation(MAIN_MENU_BACKGROUND_ANIMATION):
		_fail("Main menu background did not configure its frame animation")
		return

	var animation := animation_player.get_animation(MAIN_MENU_BACKGROUND_ANIMATION)
	if animation == null:
		_fail("Main menu background animation resource is missing")
		return
	if animation.get_track_count() != 1 or animation.track_get_key_count(0) != MAIN_MENU_BACKGROUND_FRAME_COUNT:
		_fail("Main menu background expected %d configured frame keys, got tracks=%d keys=%d" % [
			MAIN_MENU_BACKGROUND_FRAME_COUNT,
			animation.get_track_count(),
			animation.track_get_key_count(0) if animation.get_track_count() > 0 else 0,
		])
		return
	if not animation_player.is_playing():
		_fail("Main menu background frame animation is configured but not playing")


func _assert_multiplayer_lobby_layers() -> void:
	var scene := await _instantiate_scene(MULTIPLAYER_LOBBY_SCENE_PATH)
	if scene.is_empty():
		return

	var lobby := scene["instance"] as Node
	var ocean_layer := _require_node(lobby, "OceanDynamicsLayer", "multiplayer lobby ocean dynamics layer")
	if _failed:
		_cleanup_scene(scene)
		return

	_assert_image_based(ocean_layer, "multiplayer lobby ocean dynamics")
	_assert_layer_has_children(ocean_layer, "SunbeamLayer", "multiplayer lobby sunbeams")
	_assert_layer_has_children(ocean_layer, "SurfaceWakeLayer", "multiplayer lobby surface wake")
	_assert_layer_has_children(ocean_layer, "CurrentRibbonLayer", "multiplayer lobby current ribbons")
	_assert_layer_has_children(ocean_layer, "CausticRibbonLayer", "multiplayer lobby caustic ribbons")
	_assert_layer_has_children(ocean_layer, "GlintLayer", "multiplayer lobby glints")
	_cleanup_scene(scene)


func _assert_game_board_layers() -> void:
	var scene := await _instantiate_scene(GAME_BOARD_SCENE_PATH, {
		"player_count": 3,
		"item_mode": false,
		"winning_score": 100,
		"solo_mode": true,
		"adventure_mode": false,
	})
	if scene.is_empty():
		return

	var board := scene["instance"] as Node
	var ambient_layer := _require_node(board, "UnderwaterAmbientLayer", "game board underwater ambient layer")
	if _failed:
		_cleanup_scene(scene)
		return

	_assert_image_based(ambient_layer, "game board underwater ambient")
	_assert_layer_has_children(ambient_layer, "SeagrassLayer", "game board ambient seagrass")
	_assert_layer_has_children_when_enabled(ambient_layer, "SunbeamLayer", "sunbeam_intensity", "game board ambient sunbeams")
	_assert_layer_has_children(ambient_layer, "WaterCurrentLayer", "game board ambient water currents")
	_assert_layer_has_children_when_enabled(ambient_layer, "CenterCausticLayer", "center_caustic_intensity", "game board ambient center caustics")
	_assert_layer_has_children(ambient_layer, "MoteLayer", "game board ambient motes")
	if _failed:
		_cleanup_scene(scene)
		return

		var seagrass_layer := ambient_layer.get_node_or_null("SeagrassLayer") as Control
		if seagrass_layer == null or seagrass_layer.get_child_count() == 0:
			_fail("Game board seagrass layer did not create image nodes")
			_cleanup_scene(scene)
			return

		var seagrass_node := seagrass_layer.get_child(0) as Control
		if seagrass_node == null:
			_fail("Game board seagrass child is not a Control")
			_cleanup_scene(scene)
			return

		await _assert_node_stops_and_resumes(ambient_layer, seagrass_node, "game board seagrass image node")
		if _failed:
			_cleanup_scene(scene)
			return

	_cleanup_scene(scene)


func _assert_adventure_map_layers() -> void:
	var scene := await _instantiate_scene(ADVENTURE_MAP_SCENE_PATH)
	if scene.is_empty():
		return

	var adventure_map := scene["instance"] as Node
	var dynamic_layer := _require_node(adventure_map, "MapViewport/MapContent/MapDynamicLayer", "adventure map dynamic layer")
	var ambient_layer := _require_node(adventure_map, "MapViewport/MapContent/MapAmbientLayer", "adventure map ambient layer")
	if _failed:
		_cleanup_scene(scene)
		return

	_assert_image_based(dynamic_layer, "adventure map dynamic layer")
	_assert_image_based(ambient_layer, "adventure map ambient layer")
	_assert_layer_has_children(dynamic_layer, "CurrentRibbonLayer", "adventure map dynamic current ribbons")
	_assert_layer_has_children(dynamic_layer, "SparkleLayer", "adventure map dynamic sparkles")
	_assert_layer_has_children(ambient_layer, "SeagrassLayer", "adventure map ambient seagrass")
	_assert_layer_has_children_when_enabled(ambient_layer, "SunbeamLayer", "sunbeam_intensity", "adventure map ambient sunbeams")
	_assert_layer_has_children(ambient_layer, "WaterCurrentLayer", "adventure map ambient water currents")
	_assert_layer_has_children_when_enabled(ambient_layer, "CenterCausticLayer", "center_caustic_intensity", "adventure map ambient center caustics")
	_assert_layer_has_children(ambient_layer, "MoteLayer", "adventure map ambient motes")
	if _failed:
		_cleanup_scene(scene)
		return

	var ribbon_layer := dynamic_layer.get_node_or_null("CurrentRibbonLayer") as Control
	if ribbon_layer == null or ribbon_layer.get_child_count() == 0:
		_fail("Adventure map current ribbon layer did not create image nodes")
		_cleanup_scene(scene)
		return

	var ribbon_node := ribbon_layer.get_child(0) as Control
	if ribbon_node == null:
		_fail("Adventure map current ribbon child is not a Control")
		_cleanup_scene(scene)
		return

		await _assert_node_stops_and_resumes(dynamic_layer, ribbon_node, "adventure map current ribbon image node")
	_cleanup_scene(scene)


func _instantiate_scene(scene_path: String, property_overrides: Dictionary = {}) -> Dictionary:
	var viewport := SubViewport.new()
	viewport.size = VIEWPORT_SIZE
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	await process_frame

	var packed_scene := load(scene_path) as PackedScene
	if packed_scene == null:
		_fail("Failed to load scene: %s" % scene_path)
		viewport.queue_free()
		return {}

	var instance := packed_scene.instantiate()
	if instance == null:
		_fail("Failed to instantiate scene: %s" % scene_path)
		viewport.queue_free()
		return {}

	for property_name in property_overrides.keys():
		instance.set(property_name, property_overrides[property_name])

	viewport.add_child(instance)
	await _await_frames(4)
	return {
		"viewport": viewport,
		"instance": instance,
	}


func _cleanup_scene(scene: Dictionary) -> void:
	var instance := scene.get("instance") as Node
	if instance != null:
		instance.queue_free()

	var viewport := scene.get("viewport") as Node
	if viewport != null:
		viewport.queue_free()


func _await_frames(count: int) -> void:
	for _index in count:
		await process_frame


func _assert_image_based(node: Node, label: String) -> void:
	if _failed:
		return

	var render_mode := int(node.get("RenderMode"))
	if render_mode != IMAGE_BASED_RENDER_MODE:
		_fail("%s expected RenderMode=%d, got %d" % [label, IMAGE_BASED_RENDER_MODE, render_mode])


func _assert_layer_has_children(owner: Node, layer_name: String, label: String) -> void:
	if _failed:
		return

	var layer := owner.get_node_or_null(layer_name) as Node
	if layer == null:
		_fail("%s is missing layer node %s" % [label, layer_name])
		return

	if layer.get_child_count() == 0:
		_fail("%s layer %s did not create image children" % [label, layer_name])


func _assert_layer_has_children_when_enabled(owner: Node, layer_name: String, property_name: String, label: String) -> void:
	if _failed:
		return

	var enabled_value := float(owner.get(property_name))
	if enabled_value <= 0.0:
		return

	_assert_layer_has_children(owner, layer_name, label)


func _assert_node_stops_and_resumes(owner: CanvasItem, node: Control, label: String) -> void:
	if _failed:
		return

	var start_position := node.position
	await _await_seconds(1.0)
	var end_position := node.position
	if end_position.distance_to(start_position) <= 0.01:
		_fail("%s did not move after one second start=%s end=%s" % [
			label,
			str(start_position),
			str(end_position),
		])
		return

	owner.visible = false
	await _await_seconds(0.25)
	var hidden_position := node.position
	await _await_seconds(0.5)
	if node.position.distance_to(hidden_position) > 0.01:
		_fail("%s kept moving while its owner was hidden" % label)
		owner.visible = true
		return

	owner.visible = true
	await _await_seconds(1.0)
	if node.position.distance_to(hidden_position) <= 0.01:
		_fail("%s did not resume after its owner became visible" % label)


func _await_seconds(seconds: float) -> void:
	await create_timer(seconds).timeout


func _require_node(owner: Node, node_path: String, label: String) -> Node:
	var node := owner.get_node_or_null(node_path)
	if node == null:
		_fail("Missing %s at %s" % [label, node_path])
	return node


func _fail(message: String) -> void:
	if _failed:
		return

	_failed = true
	push_error(message)
	print("DYNAMIC_BACKGROUND_LAYERS_IMAGE_MODE_SMOKE_FAIL %s" % message)
	quit(1)


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return

	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame
