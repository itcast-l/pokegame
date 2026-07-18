extends SceneTree

var _issues: Array[String] = []
var _file_count := 0

func _initialize() -> void:
	_scan_directory("res://")
	if _issues.is_empty():
		print("EXPORT_PACKAGE_CONTENT_AUDIT_OK %d" % _file_count)
		quit(0)
		return

	for issue: String in _issues:
		push_error(issue)
	quit(1)

func _scan_directory(path: String) -> void:
	var directory := DirAccess.open(path)
	if directory == null:
		_issues.append("Unable to inspect package directory: %s" % path)
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
			if child_path.begins_with("res://addons/godot_mcp"):
				_issues.append("MCP directory leaked into export: %s" % child_path)
			_scan_directory(child_path)
			continue

		_file_count += 1
		if _is_excluded_source_asset(child_path):
			_issues.append("Source-only asset leaked into export: %s" % child_path)
	directory.list_dir_end()

func _is_excluded_source_asset(path: String) -> bool:
	if not path.begins_with("res://art/"):
		return false
	if path.contains("/raw/") or path.contains("/generated/"):
		return true
	var file_name := path.get_file()
	return file_name in [
		"raw-sheet.png",
		"raw-sheet.png.import",
		"raw-sheet-clean.png",
		"raw-sheet-clean.png.import",
		"sheet-transparent.png",
		"sheet-transparent.png.import",
	]
