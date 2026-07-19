#!/usr/bin/env python3
"""Build, export, and audit a release from an isolated Godot project copy."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any, Iterable, Sequence


ROOT = Path(__file__).resolve().parent.parent
STAGING_ROOT = ROOT / "outputs" / "release_staging"
BASELINE_PATH = ROOT / "tools" / "release_size_baseline.json"
AUDIT_SCRIPT = ROOT / "tools" / "export_package_content_audit.gd"
RUNTIME_SMOKE_SCRIPT = ROOT / "tools" / "release_runtime_smoke.gd"

MCP_AUTOLOADS = ("MCPScreenshot", "MCPInputService", "MCPGameInspector")
SUPPORTED_ADAPTERS = {"Windows Desktop": "windows"}

STAGING_EXCLUDED_DIRS = (
	".git",
	".godot",
	".idea",
	"Library",
	"outputs",
	"tmp",
	"generated-images",
	"addons/godot_mcp",
	"scripts/dev_mcp",
	"addons/adventure_suit_upgrades",
	"tests",
	"tools",
	"docs",
	"samples",
)
STAGING_EXCLUDED_FILES = (
	".DS_Store",
	".mcp.json",
	"AGENTS.md",
	"current_screen.png",
	"current_screen.png.import",
	"scenes/adventure_suit_upgrade_editor_scene.tscn",
	"scripts/GameBoardScene.Debug.cs",
	"scripts/GameBoardScene.Debug.cs.uid",
)
REQUIRED_EXCLUDES = (
	"addons/godot_mcp/**",
	"scripts/dev_mcp/**",
	"tmp/**",
	"generated-images/**",
	"current_screen*",
	"addons/adventure_suit_upgrades/**",
	"scenes/adventure_suit_upgrade_editor_scene.tscn",
	"scripts/GameBoardScene.Debug.cs*",
	"tests/**",
	"tools/**",
	"docs/**",
	"samples/**",
	"outputs/**",
	"Library/**",
)
TEXT_SUFFIXES = {
	".cfg",
	".cs",
	".csproj",
	".csv",
	".gd",
	".gdshader",
	".godot",
	".ini",
	".json",
	".md",
	".shader",
	".sln",
	".svg",
	".toml",
	".tres",
	".tscn",
	".txt",
	".xml",
	".yaml",
	".yml",
}
LOG_BLOCKERS = (
	"ERROR:",
	"Unicode parsing error",
	"SCRIPT ERROR",
	"Failed loading resource",
	"Failed to load resource",
	"Unable to export Pandora data",
)


class ReleaseError(RuntimeError):
	pass


def _sha256_file(path: Path) -> str:
	digest = hashlib.sha256()
	with path.open("rb") as handle:
		for chunk in iter(lambda: handle.read(1024 * 1024), b""):
			digest.update(chunk)
	return digest.hexdigest()


def _sha256_tree(path: Path) -> str:
	if not path.is_dir():
		raise ReleaseError(f"Required directory is missing: {path}")
	digest = hashlib.sha256()
	for child in sorted(item for item in path.rglob("*") if item.is_file()):
		digest.update(child.relative_to(path).as_posix().encode("utf-8"))
		digest.update(b"\0")
		digest.update(_sha256_file(child).encode("ascii"))
		digest.update(b"\n")
	return digest.hexdigest()


def capture_source_hashes(root: Path = ROOT) -> dict[str, str]:
	return {
		"project.godot": _sha256_file(root / "project.godot"),
		"export_presets.cfg": _sha256_file(root / "export_presets.cfg"),
		"addons/godot_mcp": _sha256_tree(root / "addons" / "godot_mcp"),
	}


def _parse_cfg_sections(text: str) -> dict[str, dict[str, str]]:
	sections: dict[str, dict[str, str]] = {}
	current: dict[str, str] | None = None
	for raw_line in text.splitlines():
		line = raw_line.strip()
		match = re.fullmatch(r"\[([^]]+)\]", line)
		if match:
			current = sections.setdefault(match.group(1), {})
			continue
		if current is not None and "=" in line and not line.startswith((";", "#")):
			key, value = line.split("=", 1)
			current[key.strip()] = value.strip()
	return sections


def _unquote(value: str) -> str:
	if len(value) >= 2 and value[0] == '"' and value[-1] == '"':
		return value[1:-1]
	return value


def load_presets(path: Path) -> list[dict[str, Any]]:
	sections = _parse_cfg_sections(path.read_text(encoding="utf-8"))
	presets: list[dict[str, Any]] = []
	for section_name, values in sections.items():
		match = re.fullmatch(r"preset\.(\d+)", section_name)
		if not match:
			continue
		index = match.group(1)
		options = sections.get(f"preset.{index}.options", {})
		presets.append(
			{
				"index": int(index),
				"name": _unquote(values.get("name", "")),
				"platform": _unquote(values.get("platform", "")),
				"export_filter": _unquote(values.get("export_filter", "")),
				"exclude_filter": _unquote(values.get("exclude_filter", "")),
				"export_path": _unquote(values.get("export_path", "")),
				"options": {key: _unquote(value) for key, value in options.items()},
			}
		)
	return sorted(presets, key=lambda preset: preset["index"])


def static_audit(root: Path = ROOT) -> dict[str, Any]:
	issues: list[str] = []
	presets = load_presets(root / "export_presets.cfg")
	if not presets:
		issues.append("No export presets were found")
	for preset in presets:
		label = preset["name"] or f"preset.{preset['index']}"
		if preset["platform"] not in SUPPORTED_ADAPTERS:
			issues.append(f"{label}: no actual-package adapter for platform {preset['platform']!r}")
		if preset["export_filter"] != "all_resources":
			issues.append(f"{label}: export_filter must remain all_resources")
		excludes = set(filter(None, preset["exclude_filter"].split(",")))
		for required in REQUIRED_EXCLUDES:
			if required not in excludes:
				issues.append(f"{label}: missing exclude_filter entry {required}")
		if preset["options"].get("dotnet/include_scripts_content") != "false":
			issues.append(f"{label}: dotnet/include_scripts_content must be false")
		if preset["options"].get("dotnet/include_debug_symbols") != "false":
			issues.append(f"{label}: dotnet/include_debug_symbols must be false")

	project_text = (root / "project.godot").read_text(encoding="utf-8")
	if '"res://addons/godot_mcp/plugin.cfg"' not in project_text:
		issues.append("Development project must keep the Godot MCP editor plugin enabled")
	for autoload in MCP_AUTOLOADS:
		if re.search(rf"(?m)^{re.escape(autoload)}=", project_text) is None:
			issues.append(f"Development project must keep MCP autoload {autoload}")

	return {
		"ok": not issues,
		"issues": issues,
		"presets": [
			{
				"name": preset["name"],
				"platform": preset["platform"],
				"adapter": SUPPORTED_ADAPTERS.get(preset["platform"]),
			}
			for preset in presets
		],
	}


def _should_exclude(relative_path: str) -> bool:
	path = relative_path.strip("/")
	if path in STAGING_EXCLUDED_FILES:
		return True
	return any(path == prefix or path.startswith(prefix + "/") for prefix in STAGING_EXCLUDED_DIRS)


def create_staging_project(source: Path, destination: Path) -> None:
	def ignore(directory: str, names: list[str]) -> set[str]:
		base = Path(directory).relative_to(source)
		return {
			name
			for name in names
			if _should_exclude((base / name).as_posix())
		}

	shutil.copytree(source, destination, ignore=ignore, symlinks=True)
	if (destination / "addons" / "godot_mcp").exists():
		raise ReleaseError("Staging invariant failed: MCP addon was copied")


def rewrite_staging_project(path: Path) -> None:
	text = path.read_text(encoding="utf-8")
	for autoload in MCP_AUTOLOADS:
		text = re.sub(rf"(?m)^{re.escape(autoload)}=.*(?:\n|$)", "", text)
	autoload_paths = {
		"PhantomCameraManager": "res://addons/phantom_camera/scripts/managers/phantom_camera_manager.gd",
		"SaveManager": "res://addons/savestate/save_manager.gd",
		"Pandora": "res://addons/pandora/api.gd",
	}
	for name, resource_path in autoload_paths.items():
		text = re.sub(
			rf"(?m)^{re.escape(name)}=.*$",
			f'{name}="*{resource_path}"',
			text,
		)
	text = re.sub(
		r"(?m)^enabled=PackedStringArray\(.*\)$",
		'enabled=PackedStringArray("res://addons/pandora/plugin.cfg")',
		text,
	)
	path.write_text(text, encoding="utf-8")
	for forbidden in (*MCP_AUTOLOADS, "res://addons/godot_mcp/plugin.cfg"):
		if forbidden in text:
			raise ReleaseError(f"Staging project still contains {forbidden}")


def validate_utf8_tree(root: Path) -> dict[str, Any]:
	issues: list[dict[str, Any]] = []
	checked = 0
	for path in sorted(item for item in root.rglob("*") if item.is_file()):
		if path.suffix.lower() not in TEXT_SUFFIXES and path.name not in {"project.godot", "NuGet.config"}:
			continue
		checked += 1
		data = path.read_bytes()
		try:
			data.decode("utf-8")
		except UnicodeDecodeError as error:
			issues.append(
				{
					"path": path.relative_to(root).as_posix(),
					"invalid_byte_offset": error.start,
					"reason": error.reason,
				}
			)
	return {"ok": not issues, "checked_file_count": checked, "issues": issues}


def _run(
	command: Sequence[str],
	*,
	cwd: Path,
	log_path: Path | None = None,
	timeout: int | None = None,
) -> subprocess.CompletedProcess[str]:
	result = subprocess.run(
		list(command),
		cwd=cwd,
		text=True,
		stdout=subprocess.PIPE,
		stderr=subprocess.STDOUT,
		timeout=timeout,
		check=False,
	)
	if log_path is not None:
		log_path.parent.mkdir(parents=True, exist_ok=True)
		log_path.write_text(result.stdout, encoding="utf-8")
	if result.returncode != 0:
		raise ReleaseError(
			f"Command failed with exit code {result.returncode}: {' '.join(command)}"
			+ (f" (log: {log_path})" if log_path else "")
		)
	return result


def scan_log(text: str) -> dict[str, Any]:
	counts = {marker: text.count(marker) for marker in LOG_BLOCKERS}
	return {"ok": not any(counts.values()), "blocker_counts": counts}


def _directory_metrics(path: Path) -> dict[str, int]:
	files = [item for item in path.rglob("*") if item.is_file()]
	return {"size_bytes": sum(item.stat().st_size for item in files), "file_count": len(files)}


def _find_pokegame_assembly(release_dir: Path) -> list[Path]:
	return sorted(path for path in release_dir.rglob("pokegame.dll") if path.is_file())


def _read_json(path: Path) -> dict[str, Any]:
	try:
		return json.loads(path.read_text(encoding="utf-8"))
	except (OSError, json.JSONDecodeError) as error:
		raise ReleaseError(f"Unable to read audit result {path}: {error}") from error


def _write_audit_harness(work_dir: Path) -> None:
	(work_dir / "project.godot").write_text(
		'[application]\nconfig/name="Deckland Release Audit Harness"\n', encoding="utf-8"
	)


def _package_sentinels(project_path: Path) -> tuple[str, list[str]]:
	text = project_path.read_text(encoding="utf-8")
	main_match = re.search(r'(?m)^run/main_scene="([^"]+)"$', text)
	translation_match = re.search(r'(?m)^locale/translations=PackedStringArray\((.*)\)$', text)
	if main_match is None or translation_match is None:
		raise ReleaseError("Staging project is missing main-scene or localization sentinels")
	translations = re.findall(r'"(res://[^"]+)"', translation_match.group(1))
	if not translations:
		raise ReleaseError("Staging project has no configured release translations")
	return main_match.group(1), translations


def _sentinel_arguments(main_scene: str, translations: Sequence[str]) -> list[str]:
	arguments = ["--main-scene", main_scene]
	for translation in translations:
		arguments.extend(["--translation", translation])
	return arguments


def _run_package_audit(
	godot: Path,
	pck: Path,
	work_dir: Path,
	log_path: Path,
	main_scene: str,
	translations: Sequence[str],
) -> dict[str, Any]:
	_write_audit_harness(work_dir)
	result_path = work_dir / "package-content.json"
	command = [
		str(godot),
		"--headless",
		"--path",
		str(work_dir),
		"--script",
		str(AUDIT_SCRIPT),
		"--",
		"--artifact",
		str(pck),
		"--platform",
		"windows",
		"--report",
		str(result_path),
		*_sentinel_arguments(main_scene, translations),
	]
	result = _run(command, cwd=work_dir, log_path=log_path, timeout=180)
	log_audit = scan_log(result.stdout)
	if not log_audit["ok"]:
		raise ReleaseError(f"Package audit log contains blocking errors: {log_audit['blocker_counts']}")
	report = _read_json(result_path)
	if not report.get("ok", False):
		raise ReleaseError(f"Package content audit failed; see {result_path}")
	return {"content": report, "log": log_audit}


def _runtime_smoke(
	godot: Path,
	executable: Path,
	pck: Path,
	work_dir: Path,
	log_path: Path,
	main_scene: str,
	translations: Sequence[str],
) -> dict[str, Any]:
	if os.name == "nt":
		command = [str(executable), "--headless", "--", "--release-package-smoke"]
		success_marker = "WINDOWS_RELEASE_EXECUTABLE_SMOKE_OK"
		result = _run(command, cwd=work_dir, log_path=log_path, timeout=180)
		log_audit = _validate_runtime_smoke_result(result.stdout, success_marker)
		return {
			"ok": True,
			"method": "windows_executable",
			"success_marker": success_marker,
			"log": log_audit,
		}

	_write_audit_harness(work_dir)
	harness_command = [
		str(godot),
		"--headless",
		"--path",
		str(work_dir),
		"--script",
		str(RUNTIME_SMOKE_SCRIPT),
		"--",
		"--artifact",
		str(pck),
		*_sentinel_arguments(main_scene, translations),
	]
	harness_result = _run(harness_command, cwd=work_dir, log_path=log_path, timeout=180)
	harness_log = _validate_runtime_smoke_result(harness_result.stdout, "RELEASE_RUNTIME_SMOKE_OK")
	windows_result = _run_windows_executable_on_host(executable, work_dir, log_path.parent)
	aggregate_counts = {
		marker: harness_log["blocker_counts"].get(marker, 0)
		+ windows_result["log"]["blocker_counts"].get(marker, 0)
		for marker in LOG_BLOCKERS
	}
	return {
		"ok": True,
		"method": "pack_harness_and_windows_executable",
		"success_marker": "RELEASE_RUNTIME_SMOKE_OK",
		"pack_harness": {"ok": True, "log": harness_log},
		"actual_windows_executable": windows_result,
		"log": {"ok": not any(aggregate_counts.values()), "blocker_counts": aggregate_counts},
	}


def _validate_runtime_smoke_result(text: str, success_marker: str) -> dict[str, Any]:
	log_audit = scan_log(text)
	if not log_audit["ok"]:
		raise ReleaseError(f"Runtime smoke log contains blocking errors: {log_audit['blocker_counts']}")
	if success_marker not in text:
		raise ReleaseError(f"Runtime smoke did not reach success marker: {success_marker}")
	return log_audit


def _run_windows_executable_on_host(executable: Path, work_dir: Path, logs_dir: Path) -> dict[str, Any]:
	win_args = [str(executable), "--headless", "--", "--release-package-smoke"]
	wine = shutil.which("wine64") or shutil.which("wine")
	if wine:
		result = _run(
			[wine, *win_args],
			cwd=executable.parent,
			log_path=logs_dir / "windows-executable-smoke.log",
			timeout=180,
		)
		log_audit = _validate_runtime_smoke_result(result.stdout, "WINDOWS_RELEASE_EXECUTABLE_SMOKE_OK")
		return {
			"ok": True,
			"method": "wine",
			"success_marker": "WINDOWS_RELEASE_EXECUTABLE_SMOKE_OK",
			"log": log_audit,
		}

	crossover_bin = Path(
		"/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/CrossOver-Hosted Application"
	)
	crossover_wine = crossover_bin / "wine"
	cxbottle = crossover_bin / "cxbottle"
	if not crossover_wine.is_file() or not cxbottle.is_file():
		raise ReleaseError(
			"Windows executable smoke requires Windows, Wine, or CrossOver; pack-only validation cannot publish"
		)

	bottle = f"DecklandReleaseAudit-{os.getpid()}"
	_run(
		[
			str(cxbottle),
			"--bottle",
			bottle,
			"--create",
			"--template",
			"win10_64",
			"--description",
			"Temporary Deckland release audit",
		],
		cwd=work_dir,
		log_path=logs_dir / "windows-bottle-create.log",
		timeout=300,
	)
	delete_result: subprocess.CompletedProcess[str] | None = None
	try:
		result = _run(
			[
				str(crossover_wine),
				"--bottle",
				bottle,
				"--no-gui",
				"--workdir",
				str(executable.parent),
				*win_args,
			],
			cwd=executable.parent,
			log_path=logs_dir / "windows-executable-smoke.log",
			timeout=180,
		)
		log_audit = _validate_runtime_smoke_result(result.stdout, "WINDOWS_RELEASE_EXECUTABLE_SMOKE_OK")
	finally:
		delete_result = subprocess.run(
			[str(cxbottle), "--bottle", bottle, "--delete", "--force"],
			cwd=work_dir,
			text=True,
			stdout=subprocess.PIPE,
			stderr=subprocess.STDOUT,
			check=False,
		)
		(logs_dir / "windows-bottle-delete.log").write_text(delete_result.stdout, encoding="utf-8")
	if delete_result.returncode != 0:
		raise ReleaseError(f"Temporary CrossOver audit bottle could not be deleted: {bottle}")
	return {
		"ok": True,
		"method": "CrossOver Windows 10 64-bit temporary bottle",
		"success_marker": "WINDOWS_RELEASE_EXECUTABLE_SMOKE_OK",
		"temporary_bottle_deleted": True,
		"log": log_audit,
	}


def _size_baseline(platform: str, metrics: dict[str, int], pck_size: int) -> dict[str, Any]:
	current = {"release_directory_size_bytes": metrics["size_bytes"], "pck_size_bytes": pck_size}
	if not BASELINE_PATH.exists():
		return {"status": "missing", "current": current, "warnings": [], "blockers": []}
	data = _read_json(BASELINE_PATH)
	baseline = data.get(platform)
	if not isinstance(baseline, dict):
		return {"status": "missing", "current": current, "warnings": [], "blockers": []}

	warnings: list[str] = []
	blockers: list[str] = []
	for key, value in current.items():
		old = int(baseline.get(key, 0))
		if old <= 0:
			continue
		delta = value - old
		ratio = delta / old
		if ratio > 0.15 or delta > 25 * 1024 * 1024:
			blockers.append(f"{key} grew by {delta} bytes ({ratio:.1%})")
		elif ratio > 0.05 or delta > 10 * 1024 * 1024:
			warnings.append(f"{key} grew by {delta} bytes ({ratio:.1%})")
	return {
		"status": "blocked" if blockers else ("warning" if warnings else "ok"),
		"baseline": baseline,
		"current": current,
		"warnings": warnings,
		"blockers": blockers,
	}


def _write_json(path: Path, value: Any) -> None:
	path.parent.mkdir(parents=True, exist_ok=True)
	path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _select_preset(name: str, presets: Iterable[dict[str, Any]]) -> dict[str, Any]:
	for preset in presets:
		if preset["name"] == name:
			return preset
	raise ReleaseError(f"Unknown export preset: {name}")


def _artifact_name(preset: dict[str, Any]) -> str:
	name = Path(preset["export_path"]).name
	if preset["platform"] == "Windows Desktop":
		return name if name.lower().endswith(".exe") else "Deckland.exe"
	raise ReleaseError(f"No artifact naming adapter for {preset['platform']}")


def _prepare_output(path: Path) -> None:
	if path.exists() and (not path.is_dir() or any(path.iterdir())):
		raise ReleaseError(f"Release output directory must be absent or empty: {path}")
	path.mkdir(parents=True, exist_ok=True)


def run_release(args: argparse.Namespace) -> Path:
	static_result = static_audit()
	if not static_result["ok"]:
		raise ReleaseError("Static release audit failed:\n" + "\n".join(static_result["issues"]))
	preset = _select_preset(args.preset, load_presets(ROOT / "export_presets.cfg"))
	platform = SUPPORTED_ADAPTERS[preset["platform"]]
	if platform != "windows":
		raise ReleaseError(f"Adapter is registered but not implemented: {platform}")

	output_root = args.output.resolve()
	_prepare_output(output_root)
	build_id = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ") + f"-{os.getpid()}"
	staging = STAGING_ROOT / build_id
	project_dir = staging / "project"
	artifact_dir = staging / "artifact"
	logs_dir = staging / "logs"
	audit_dir = staging / "audit-work"
	artifact_dir.mkdir(parents=True)
	audit_dir.mkdir(parents=True)
	_write_audit_harness(audit_dir)

	before_hashes = capture_source_hashes()
	report: dict[str, Any] = {
		"schema_version": 1,
		"status": "failed",
		"build_id": build_id,
		"preset": args.preset,
		"platform": platform,
		"source_root": str(ROOT),
		"staging_path": str(staging),
		"source_integrity": {"before": before_hashes},
		"preflight": {"static": static_result},
	}
	success = False
	try:
		resource_log = logs_dir / "resource-audit.log"
		_run(["bash", "tools/audit_export_resources.sh"], cwd=ROOT, log_path=resource_log)
		report["preflight"]["resource_audit_log"] = str(resource_log)

		create_staging_project(ROOT, project_dir)
		rewrite_staging_project(project_dir / "project.godot")
		utf8_result = validate_utf8_tree(project_dir)
		report["preflight"]["utf8"] = utf8_result
		if not utf8_result["ok"]:
			raise ReleaseError("UTF-8 preflight failed: " + json.dumps(utf8_result["issues"], ensure_ascii=False))

		_run(
			["dotnet", "build", "pokegame.csproj", "--configuration", "Release"],
			cwd=ROOT,
			log_path=logs_dir / "dotnet-release-build.log",
		)
		_run(
			["dotnet", "run", "--project", "tests/Rules.Tests/Rules.Tests.csproj"],
			cwd=ROOT,
			log_path=logs_dir / "rules-tests.log",
		)
		_run(
			["dotnet", "build", "pokegame.csproj", "--configuration", "Release"],
			cwd=project_dir,
			log_path=logs_dir / "staging-dotnet-release-build.log",
		)
		_run(
			["dotnet", "build", "pokegame.csproj", "--configuration", "Debug"],
			cwd=project_dir,
			log_path=logs_dir / "staging-dotnet-editor-build.log",
		)

		executable = artifact_dir / _artifact_name(preset)
		export_result = _run(
			[str(args.godot.resolve()), "--headless", "--path", str(project_dir), "--export-release", args.preset, str(executable)],
			cwd=project_dir,
			log_path=logs_dir / "godot-export.log",
			timeout=args.export_timeout,
		)
		export_log = scan_log(export_result.stdout)
		report["export_log"] = export_log
		if not export_log["ok"]:
			raise ReleaseError(f"Export log contains blocking errors: {export_log['blocker_counts']}")

		pck = executable.with_suffix(".pck")
		if not executable.is_file() or not pck.is_file():
			raise ReleaseError(f"Windows adapter did not find expected executable and PCK: {executable}, {pck}")
		assemblies = _find_pokegame_assembly(artifact_dir)
		if not assemblies:
			raise ReleaseError("Release directory does not contain pokegame.dll")
		pdbs = sorted(path for path in artifact_dir.rglob("*.pdb") if path.is_file())
		if pdbs:
			raise ReleaseError("Public release contains debug symbols: " + ", ".join(str(path) for path in pdbs))

		main_scene, translations = _package_sentinels(project_dir / "project.godot")
		package_audit = _run_package_audit(
			args.godot.resolve(), pck, audit_dir, logs_dir / "package-content-audit.log", main_scene, translations
		)
		report["package_audit"] = package_audit
		runtime = _runtime_smoke(
			args.godot.resolve(), executable, pck, audit_dir, logs_dir / "runtime-smoke.log", main_scene, translations
		)
		report["runtime_smoke"] = runtime

		metrics = _directory_metrics(artifact_dir)
		baseline = _size_baseline(platform, metrics, pck.stat().st_size)
		report["size_baseline"] = baseline
		if baseline["blockers"]:
			raise ReleaseError("Release size exceeded blocking baseline: " + "; ".join(baseline["blockers"]))

		after_hashes = capture_source_hashes()
		report["source_integrity"]["after"] = after_hashes
		report["source_integrity"]["unchanged"] = before_hashes == after_hashes
		if before_hashes != after_hashes:
			raise ReleaseError("Source project, export presets, or MCP addon changed during export")

		package_dir = output_root / "package"
		final_logs_dir = output_root / "audit-logs"
		shutil.copytree(artifact_dir, package_dir)
		shutil.copytree(logs_dir, final_logs_dir)
		final_executable = package_dir / executable.name
		final_pck = package_dir / pck.name
		final_metrics = _directory_metrics(package_dir)
		report["package_audit"]["content"]["artifact_path"] = str(final_pck)
		report["logs"] = {
			path.name: str(final_logs_dir / path.name)
			for path in sorted(logs_dir.iterdir())
			if path.is_file()
		}
		report["log_error_counts"] = {
			marker: sum(
				section["blocker_counts"].get(marker, 0)
				for section in (
					export_log,
					package_audit["log"],
					runtime["log"],
				)
			)
			for marker in LOG_BLOCKERS
		}
		report["preflight"]["resource_audit_log"] = str(final_logs_dir / resource_log.name)
		report["artifact"] = {
			"release_directory": str(package_dir),
			"release_directory_size_bytes": final_metrics["size_bytes"],
			"release_directory_file_count": final_metrics["file_count"],
			"executable_path": str(final_executable),
			"executable_sha256": _sha256_file(final_executable),
			"pck_path": str(final_pck),
			"pck_size_bytes": final_pck.stat().st_size,
			"pck_sha256": _sha256_file(final_pck),
			"assembly_paths": [str(path.relative_to(artifact_dir)) for path in assemblies],
		}
		report["status"] = "passed"
		report["completed_at"] = dt.datetime.now(dt.timezone.utc).isoformat()
		report_path = output_root / "release-audit.json"
		_write_json(report_path, report)
		success = True
		return report_path
	except Exception as error:
		report["error"] = str(error)
		after_hashes = capture_source_hashes()
		report["source_integrity"]["after"] = after_hashes
		report["source_integrity"]["unchanged"] = before_hashes == after_hashes
		_write_json(staging / "release-audit.failed.json", report)
		raise
	finally:
		if success and not args.keep_staging:
			shutil.rmtree(staging)


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("--godot", type=Path, help="Godot 4.6 .NET executable")
	parser.add_argument("--preset", help="Release export preset name")
	parser.add_argument(
		"--output",
		type=Path,
		help="New or empty output root; player files are written to package/ and audit evidence beside it",
	)
	parser.add_argument("--static-audit", action="store_true", help="Only validate release preset and development MCP invariants")
	parser.add_argument("--keep-staging", action="store_true", help="Keep staging files after a successful release")
	parser.add_argument("--export-timeout", type=int, default=1800, help="Godot export timeout in seconds")
	return parser


def main(argv: Sequence[str] | None = None) -> int:
	args = build_parser().parse_args(argv)
	if args.static_audit:
		result = static_audit()
		print(json.dumps(result, ensure_ascii=False, indent=2))
		return 0 if result["ok"] else 1
	missing = [name for name in ("godot", "preset", "output") if getattr(args, name) is None]
	if missing:
		raise ReleaseError("Missing required release arguments: " + ", ".join("--" + name for name in missing))
	if not args.godot.is_file():
		raise ReleaseError(f"Godot executable does not exist: {args.godot}")
	report = run_release(args)
	print(f"RELEASE_EXPORT_OK {report}")
	return 0


if __name__ == "__main__":
	try:
		raise SystemExit(main())
	except (ReleaseError, subprocess.TimeoutExpired) as error:
		print(f"RELEASE_EXPORT_FAILED: {error}", file=sys.stderr)
		raise SystemExit(1)
