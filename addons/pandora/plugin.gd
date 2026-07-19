@tool
extends EditorPlugin

const PandoraEditor := preload("res://addons/pandora/ui/editor/pandora_editor.tscn")
const PandoraIcon := preload("res://addons/pandora/icons/pandora-icon.svg")
const PandoraEntityInspector = preload("res://addons/pandora/ui/editor/inspector/entity_instance_inspector.gd")
const Compression = preload("res://addons/pandora/util/compression.gd")

var editor_view
var entity_inspector
var _exporter: PandoraExportPlugin
var _resource_filesystem: EditorFileSystem
var _resource_change_callback := Callable()
var _inspector_registered := false
var _exporter_registered := false
var _autoload_registered := false

func _init() -> void:
	self.name = 'PandoraPlugin'


func _enter_tree() -> void:
	Engine.set_meta("PandoraEditorPlugin", self)
	_exporter = PandoraExportPlugin.new()
	if not ProjectSettings.has_setting("autoload/Pandora"):
		add_autoload_singleton("Pandora", "res://addons/pandora/api.gd")
		_autoload_registered = true
	add_export_plugin(_exporter)
	_exporter_registered = true
	PandoraSettings.initialize()

	if Engine.is_editor_hint():
			editor_view = PandoraEditor.instantiate()
			editor_view.hide()
			get_editor_interface().get_editor_main_screen().add_child(editor_view)

			_resource_filesystem = get_editor_interface().get_resource_filesystem()
			_resource_change_callback = Callable(self, "_on_resource_filesystem_changed")
			_resource_filesystem.resources_reimported.connect(_resource_change_callback)
			_resource_filesystem.sources_changed.connect(_resource_change_callback)
			_resource_filesystem.resources_reload.connect(_resource_change_callback)
			_resource_filesystem.script_classes_updated.connect(_resource_change_callback)

			entity_inspector = PandoraEntityInspector.new()
			add_inspector_plugin(entity_inspector)
			_inspector_registered = true

	_make_visible(false)


func _apply_changes() -> void:
	if Engine.is_editor_hint() and is_instance_valid(editor_view):
		if editor_view.has_method("apply_changes"):
			editor_view.apply_changes()


func _exit_tree() -> void:
	_disconnect_resource_filesystem_signals()

	if Engine.is_editor_hint() and is_instance_valid(editor_view):
		var parent: Node = editor_view.get_parent()
		if is_instance_valid(parent):
			parent.remove_child(editor_view)
		if not editor_view.is_queued_for_deletion():
			editor_view.queue_free()
	editor_view = null

	if _inspector_registered and is_instance_valid(entity_inspector):
		remove_inspector_plugin(entity_inspector)
	_inspector_registered = false
	entity_inspector = null

	if Engine.has_meta("PandoraEditorPlugin"):
		Engine.remove_meta("PandoraEditorPlugin")

	if _exporter_registered and is_instance_valid(_exporter):
		remove_export_plugin(_exporter)
	_exporter_registered = false
	_exporter = null
	if _autoload_registered:
		remove_autoload_singleton("Pandora")
	_autoload_registered = false


func _on_resource_filesystem_changed(_resources = null) -> void:
	if is_instance_valid(editor_view) and editor_view.has_method("reattempt_load_on_error"):
		editor_view.reattempt_load_on_error()


func _disconnect_resource_filesystem_signals() -> void:
	if not is_instance_valid(_resource_filesystem) or not _resource_change_callback.is_valid():
		_resource_filesystem = null
		_resource_change_callback = Callable()
		return
	for signal_name: StringName in [
		&"resources_reimported",
		&"sources_changed",
		&"resources_reload",
		&"script_classes_updated",
	]:
		if _resource_filesystem.is_connected(signal_name, _resource_change_callback):
			_resource_filesystem.disconnect(signal_name, _resource_change_callback)
	_resource_filesystem = null
	_resource_change_callback = Callable()


func _make_visible(visible:bool) -> void:
	if Engine.is_editor_hint() and is_instance_valid(editor_view):
		editor_view.visible = visible


func _has_main_screen() -> bool:
	return Engine.is_editor_hint()


func _get_plugin_name() -> String:
	return "Pandora"


func _get_plugin_icon() -> Texture2D:
	return PandoraIcon

class PandoraExportPlugin extends EditorExportPlugin:
	# Override the _export_begin method to add the data.pandora file during export
	func _export_begin(features: PackedStringArray, is_debug: bool, path: String, flags: int):
		var pandora_path = "res://data.pandora"
		var file = FileAccess.open(pandora_path, FileAccess.READ)
		if not file:
			printerr("Unable to export Pandora data: ",  FileAccess.get_open_error())
			return
		var data:PackedByteArray = file.get_buffer(file.get_length())
		if not is_debug:
			var text = file.get_as_text()
			data = Compression.compress(text)
		add_file(pandora_path, data, false)

	func _get_name() -> String:
		return "PandoraExporter"
