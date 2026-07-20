extends SceneTree

const FORBIDDEN_PATH_PREFIXES := [
	"addons/godot_mcp/",
	"scripts/dev_mcp/",
	"scripts/GameBoardScene.Debug.",
	"tmp/",
	"generated-images/",
	"addons/adventure_suit_upgrades/",
	"tests/",
	"tools/",
	"docs/",
	"samples/",
	"outputs/",
	"Library/",
]
const FORBIDDEN_EXACT_PATHS := [
	"scenes/adventure_suit_upgrade_editor_scene.tscn",
	"scripts/GameBoardScene.Debug.cs",
	"scripts/GameBoardScene.Debug.cs.uid",
]
const MCP_AUTOLOADS := [
	"MCPScreenshot",
	"MCPInputService",
	"MCPGameInspector",
]
const PANDORA_RUNTIME_SENTINELS := [
	"res://data.pandora",
	"res://addons/pandora/api.gd",
	"res://addons/pandora/backend/entity_backend.gd",
	"res://addons/pandora/model/entity.gd",
	"res://addons/pandora/model/type.gd",
]

var _issues: Array[String] = []
var _entries: Array[String] = []
var _forbidden_matches: Array[String] = []
var _cs_source_entries: Array[String] = []
var _cs_placeholder_entries: Array[String] = []
var _artifact_path := ""
var _platform := ""
var _report_path := ""
var _main_scene := ""
var _translations: Array[String] = []


func _initialize() -> void:
	_parse_arguments()
	if _artifact_path.is_empty():
		_fail("Missing required --artifact PCK path")
	if _report_path.is_empty():
		_fail("Missing required --report path")
	if _platform != "windows":
		_fail("Unsupported or missing package audit platform: %s" % _platform)
	if not _artifact_path.is_absolute_path() or _artifact_path.get_extension().to_lower() != "pck":
		_fail("Artifact must be an absolute .pck path: %s" % _artifact_path)
	if not FileAccess.file_exists(_artifact_path):
		_fail("Artifact does not exist: %s" % _artifact_path)
	if _main_scene.is_empty():
		_fail("Missing required --main-scene package sentinel")
	if _translations.is_empty():
		_fail("Missing required --translation package sentinels")

	if _issues.is_empty():
		_remove_audit_harness_project()
		if not ProjectSettings.load_resource_pack(_artifact_path, true):
			_fail("Unable to mount actual release PCK: %s" % _artifact_path)
	if _issues.is_empty():
		_scan_directory("res://")
		_audit_project_settings()
		_audit_runtime_sentinels()

	_entries.sort()
	_forbidden_matches.sort()
	_cs_source_entries.sort()
	_cs_placeholder_entries.sort()
	_write_report()
	if _issues.is_empty():
		print("EXPORT_PACKAGE_CONTENT_AUDIT_OK %d" % _entries.size())
		quit(0)
		return

	for issue: String in _issues:
		push_error(issue)
	quit(1)


func _parse_arguments() -> void:
	var arguments := OS.get_cmdline_user_args()
	var index := 0
	while index < arguments.size():
		var argument := arguments[index]
		if argument in ["--artifact", "--platform", "--report", "--main-scene", "--translation"]:
			if index + 1 >= arguments.size():
				_fail("Missing value for %s" % argument)
				return
			var value := arguments[index + 1]
			if argument == "--artifact":
				_artifact_path = value
			elif argument == "--platform":
				_platform = value
			elif argument == "--report":
				_report_path = value
			elif argument == "--main-scene":
				_main_scene = value
			else:
				_translations.append(value)
			index += 2
			continue
		_fail("Unknown package audit argument: %s" % argument)
		index += 1


func _scan_directory(path: String) -> void:
	var directory := DirAccess.open(path)
	if directory == null:
		_fail("Unable to inspect mounted package directory: %s" % path)
		return

	directory.list_dir_begin()
	while true:
		var entry := directory.get_next()
		if entry.is_empty():
			break
		if entry == "." or entry == "..":
			continue
		var child_path := path.path_join(entry)
		if directory.current_is_dir():
			_scan_directory(child_path)
			continue

		var relative_path := child_path.trim_prefix("res://")
		_entries.append(relative_path)
		if _is_forbidden_path(relative_path):
			_forbidden_matches.append(relative_path)
			_fail("Forbidden source-only path leaked into package: %s" % relative_path)
		if relative_path.get_extension().to_lower() == "cs":
			_audit_csharp_entry(child_path, relative_path)
	directory.list_dir_end()


func _is_forbidden_path(path: String) -> bool:
	if path in FORBIDDEN_EXACT_PATHS:
		return true
	if path.get_file().begins_with("current_screen"):
		return true
	for prefix: String in FORBIDDEN_PATH_PREFIXES:
		if path.begins_with(prefix):
			return true
	return false


func _audit_csharp_entry(path: String, relative_path: String) -> void:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_fail("Unable to inspect C# package entry: %s" % relative_path)
		return
	var content := file.get_buffer(file.get_length())
	if not _has_csharp_source_content(content):
		_cs_placeholder_entries.append(relative_path)
		return
	_cs_source_entries.append(relative_path)
	_fail("Recoverable C# source content leaked into package: %s (%d bytes)" % [relative_path, content.size()])


func _has_csharp_source_content(content: PackedByteArray) -> bool:
	for byte: int in content:
		if byte not in [0, 9, 10, 13, 32]:
			return true
	return false


func _audit_project_settings() -> void:
	var project_binary := FileAccess.open("res://project.binary", FileAccess.READ)
	if project_binary == null:
		_fail("Mounted package is missing project.binary")
		return
	var project_bytes := project_binary.get_buffer(project_binary.get_length())
	for autoload: String in MCP_AUTOLOADS:
		if _bytes_contain(project_bytes, autoload.to_utf8_buffer()):
			_fail("MCP autoload leaked into package project settings: %s" % autoload)


func _audit_runtime_sentinels() -> void:
	if not ResourceLoader.exists(_main_scene):
		_fail("Configured main scene is missing from package: %s" % _main_scene)
	else:
		var scene := ResourceLoader.load(_main_scene)
		if scene == null:
			_fail("Configured main scene and its base dependencies did not load: %s" % _main_scene)

	for path: String in PANDORA_RUNTIME_SENTINELS:
		if not FileAccess.file_exists(path) and not ResourceLoader.exists(path):
			_fail("Required Pandora runtime sentinel is missing: %s" % path)

	for translation_path: String in _translations:
		if not ResourceLoader.exists(translation_path):
			_fail("Configured localization resource is missing: %s" % translation_path)

	var pandora_file := FileAccess.open_compressed("res://data.pandora", FileAccess.READ)
	if pandora_file == null:
		_fail("Release Pandora data cannot be opened as a compressed runtime file")
	else:
		var pandora_data = JSON.parse_string(pandora_file.get_as_text())
		if not pandora_data is Dictionary or pandora_data.is_empty():
			_fail("Release Pandora data did not decode to a non-empty JSON object")


func _remove_audit_harness_project() -> void:
	var harness_project := ProjectSettings.globalize_path("res://project.godot")
	if FileAccess.file_exists(harness_project):
		DirAccess.remove_absolute(harness_project)


func _bytes_contain(haystack: PackedByteArray, needle: PackedByteArray) -> bool:
	if needle.is_empty() or needle.size() > haystack.size():
		return false
	for start in range(haystack.size() - needle.size() + 1):
		var matches := true
		for offset in range(needle.size()):
			if haystack[start + offset] != needle[offset]:
				matches = false
				break
		if matches:
			return true
	return false


func _write_report() -> void:
	if _report_path.is_empty():
		return
	var report := {
		"ok": _issues.is_empty(),
		"platform": _platform,
		"artifact_path": _artifact_path,
		"file_count": _entries.size(),
		"file_entries": _entries,
		"forbidden_matches": _forbidden_matches,
		"csharp": {
			"source_content_count": _cs_source_entries.size(),
			"source_content_entries": _cs_source_entries,
			"placeholder_count": _cs_placeholder_entries.size(),
			"placeholder_entries": _cs_placeholder_entries,
		},
		"main_scene": _main_scene,
		"configured_translations": _translations,
		"runtime_localization_csv_present": FileAccess.file_exists("res://localization/ui.csv"),
		"issues": _issues,
	}
	var file := FileAccess.open(_report_path, FileAccess.WRITE)
	if file == null:
		push_error("Unable to write package audit report: %s" % _report_path)
		return
	file.store_string(JSON.stringify(report, "  ") + "\n")


func _fail(message: String) -> void:
	if message not in _issues:
		_issues.append(message)
