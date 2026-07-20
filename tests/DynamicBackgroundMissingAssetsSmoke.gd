extends SceneTree

const OBSERVE_FRAMES := 300
const LAYER_CASES := [
	{
		"name": "AdventureMapDynamicLayer",
		"script": "res://scripts/AdventureMapDynamicLayer.cs",
	},
	{
		"name": "UnderwaterAmbientLayer",
		"script": "res://scripts/UnderwaterAmbientLayer.cs",
	},
	{
		"name": "MainMenuOceanDynamics",
		"script": "res://scripts/MainMenuOceanDynamics.cs",
	},
]

var _failed := false


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	var layers: Array[Control] = []
	for layer_case in LAYER_CASES:
		var layer_script := load(String(layer_case["script"])) as Script
		var layer := layer_script.new() as Control if layer_script != null else null
		if layer == null:
			_fail("Could not instantiate %s" % String(layer_case["name"]))
			return

		layer.name = StringName(layer_case["name"])
		layer.size = Vector2(1280.0, 720.0)
		layer.set("RenderMode", 1)
		layer.set("debug_force_missing_image_assets", true)
		root.add_child(layer)
		layers.append(layer)

	for _frame in 10:
		await process_frame

	var baselines: Array[Dictionary] = []
	for layer in layers:
		if String(layer.call("DebugGetImageAssetStateName")) != "PermanentFallback":
			_fail("%s did not enter PermanentFallback" % layer.name)
			return
		baselines.append({
			"loads": int(layer.call("DebugGetResourceLoadCount")),
			"rebuilds": int(layer.call("DebugGetRebuildCount")),
		})

	for _frame in OBSERVE_FRAMES:
		await process_frame

	for index in layers.size():
		var layer := layers[index]
		var baseline := baselines[index]
		if int(layer.call("DebugGetResourceLoadCount")) != int(baseline["loads"]):
			_fail("%s retried resource loading in PermanentFallback" % layer.name)
			return
		if int(layer.call("DebugGetRebuildCount")) != int(baseline["rebuilds"]):
			_fail("%s rebuilt during a stable fallback window" % layer.name)
			return

	for layer in layers:
		layer.queue_free()
	await process_frame
	await process_frame
	print("DYNAMIC_BACKGROUND_MISSING_ASSETS_SMOKE_OK")
	quit(0)


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("DYNAMIC_BACKGROUND_MISSING_ASSETS_SMOKE_FAIL %s" % message)
	quit(1)
