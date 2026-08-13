#!/usr/bin/env python3

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))

import release_export  # noqa: E402


DEFAULT_GODOT = Path("/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot")
GODOT = Path(os.environ.get("GODOT_BIN", str(DEFAULT_GODOT)))


class ReleaseExportUnitTests(unittest.TestCase):
	def test_network_protocol_version_requires_one_positive_constant(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			(root / "scripts").mkdir()
			protocol = root / "scripts/NetworkProtocol.cs"
			protocol.write_text("public const int CurrentVersion = 11;\n", encoding="utf-8")
			self.assertEqual(11, release_export._network_protocol_version(root))
			protocol.write_text("public const int CurrentVersion = 0;\n", encoding="utf-8")
			with self.assertRaises(release_export.ReleaseError):
				release_export._network_protocol_version(root)

	def test_static_audit_accepts_three_platform_specific_steam_runtimes(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			(root / "project.godot").write_text(
				'[autoload]\nMCPScreenshot="*res://mcp.gd"\n'
				'MCPInputService="*res://mcp.gd"\nMCPGameInspector="*res://mcp.gd"\n'
				'[editor_plugins]\nenabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")\n',
				encoding="utf-8",
			)
			sections = []
			platforms = (("Windows", "Windows Desktop", "windows"), ("Linux", "Linux", "linux"), ("macOS", "macOS", "macos"))
			for index, (name, platform_name, adapter) in enumerate(platforms):
				excludes = set(release_export.REQUIRED_EXCLUDES)
				excludes.update(
					path
					for other_adapter, path in release_export.STEAM_RUNTIME_EXCLUDES.items()
					if other_adapter != adapter
				)
				sections.append(
					f'''[preset.{index}]
name="{name}"
platform="{platform_name}"
export_filter="all_resources"
exclude_filter="{','.join(sorted(excludes))}"
[preset.{index}.options]
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
'''
				)
			(root / "export_presets.cfg").write_text("\n".join(sections), encoding="utf-8")
			result = release_export.static_audit(root, audit_legal=False)
			self.assertTrue(result["ok"], result["issues"])
			self.assertEqual(["windows", "linux", "macos"], [preset["adapter"] for preset in result["presets"]])

	def test_export_artifact_resolvers_cover_windows_linux_and_macos(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			windows_executable = root / "Deckland.exe"
			windows_pck = root / "Deckland.pck"
			windows_executable.write_bytes(b"exe")
			windows_pck.write_bytes(b"pck")
			self.assertEqual(
				(windows_executable, windows_pck),
				release_export._resolve_export_artifacts("windows", windows_executable),
			)

			linux_executable = root / "Deckland.x86_64"
			linux_pck = root / "Deckland.pck"
			linux_executable.write_bytes(b"elf")
			self.assertEqual(
				(linux_executable, linux_pck),
				release_export._resolve_export_artifacts("linux", linux_executable),
			)

			app = root / "Deckland.app"
			macos_executable = app / "Contents/MacOS/Deckland"
			macos_pck = app / "Contents/Resources/Deckland.pck"
			macos_executable.parent.mkdir(parents=True)
			macos_pck.parent.mkdir(parents=True)
			macos_executable.write_bytes(b"mach-o")
			macos_pck.write_bytes(b"pck")
			self.assertEqual(
				(macos_executable, macos_pck),
				release_export._resolve_export_artifacts("macos", app),
			)

	def test_static_audit_checks_every_preset_and_unknown_platform(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			(root / "project.godot").write_text(
				'[autoload]\nMCPScreenshot="*res://mcp.gd"\n'
				'MCPInputService="*res://mcp.gd"\nMCPGameInspector="*res://mcp.gd"\n'
				'[editor_plugins]\nenabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")\n',
				encoding="utf-8",
			)
			required = ",".join(release_export.REQUIRED_EXCLUDES)
			(root / "export_presets.cfg").write_text(
				f'''[preset.0]
name="Windows"
platform="Windows Desktop"
export_filter="all_resources"
exclude_filter="{required}"
[preset.0.options]
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
[preset.1]
name="Future Linux"
platform="Linux/X11"
export_filter="all_resources"
exclude_filter="{required}"
[preset.1.options]
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
''',
				encoding="utf-8",
			)
			result = release_export.static_audit(root, audit_legal=False)
			self.assertFalse(result["ok"])
			self.assertTrue(any("Future Linux" in issue and "adapter" in issue for issue in result["issues"]))

	def test_staging_copy_omits_development_files_and_preserves_source(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			base = Path(temp)
			source = base / "source"
			destination = base / "staging"
			(source / "addons/godot_mcp").mkdir(parents=True)
			(source / "scripts/dev_mcp").mkdir(parents=True)
			(source / "tmp").mkdir()
			(source / "scripts").mkdir(exist_ok=True)
			(source / "runtime").mkdir()
			(source / "legal/distribution/licenses").mkdir(parents=True)
			(source / "addons/godot_mcp/plugin.gd").write_text("extends EditorPlugin\n", encoding="utf-8")
			(source / "scripts/dev_mcp/proxy.gd").write_text("extends Node\n", encoding="utf-8")
			(source / "tmp/leak.txt").write_text("leak\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.Debug.cs").write_text("debug\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.Debug.Core.cs").write_text("debug core\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.Debug.Core.cs.uid").write_text("uid://debug-core\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.Debug.Network.cs").write_text("debug network\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.Debug.Network.cs.uid").write_text("uid://debug-network\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.DebugHooks.cs").write_text("debug hooks\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.DebugRecording.cs").write_text("debug recording\n", encoding="utf-8")
			(source / "scripts/GameBoardScene.NetworkDebugActions.cs").write_text("release QA surface\n", encoding="utf-8")
			(source / "runtime/keep.txt").write_text("keep\n", encoding="utf-8")
			(source / "legal/distribution/THIRD_PARTY_NOTICES.txt").write_text("public notice\n", encoding="utf-8")
			(source / "legal/ASSET_RIGHTS_REGISTER.csv").write_text("internal register\n", encoding="utf-8")
			project = (
				'[autoload]\nMCPScreenshot="*res://scripts/dev_mcp/proxy.gd"\n'
				'MCPInputService="*res://scripts/dev_mcp/proxy.gd"\n'
				'MCPGameInspector="*res://scripts/dev_mcp/proxy.gd"\n'
				'[editor_plugins]\nenabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg", '
				'"res://addons/adventure_suit_upgrades/plugin.cfg", "res://addons/pandora/plugin.cfg")\n'
			)
			(source / "project.godot").write_text(project, encoding="utf-8")
			source_hash = release_export._sha256_file(source / "project.godot")

			release_export.create_staging_project(source, destination)
			release_export.rewrite_staging_project(destination / "project.godot", "release-fixture", 11)

			self.assertTrue((destination / "runtime/keep.txt").is_file())
			self.assertFalse((destination / "addons/godot_mcp").exists())
			self.assertFalse((destination / "scripts/dev_mcp").exists())
			self.assertFalse((destination / "tmp").exists())
			self.assertFalse((destination / "legal").exists())
			self.assertFalse((destination / "scripts/GameBoardScene.Debug.cs").exists())
			self.assertFalse((destination / "scripts/GameBoardScene.Debug.Core.cs").exists())
			self.assertFalse((destination / "scripts/GameBoardScene.Debug.Core.cs.uid").exists())
			self.assertFalse((destination / "scripts/GameBoardScene.Debug.Network.cs").exists())
			self.assertFalse((destination / "scripts/GameBoardScene.Debug.Network.cs.uid").exists())
			self.assertTrue((destination / "scripts/GameBoardScene.DebugHooks.cs").is_file())
			self.assertTrue((destination / "scripts/GameBoardScene.DebugRecording.cs").is_file())
			self.assertTrue((destination / "scripts/GameBoardScene.NetworkDebugActions.cs").is_file())
			staged_project = (destination / "project.godot").read_text(encoding="utf-8")
			self.assertNotIn("MCPScreenshot", staged_project)
			self.assertNotIn("godot_mcp", staged_project)
			self.assertIn("addons/pandora/plugin.cfg", staged_project)
			self.assertIn('build_id="release-fixture"', staged_project)
			self.assertIn("network_protocol=11", staged_project)
			self.assertEqual(source_hash, release_export._sha256_file(source / "project.godot"))

	def test_committed_legal_sources_and_asset_register_pass(self) -> None:
		result = release_export.legal_source_audit(ROOT)
		self.assertTrue(result["ok"], result["issues"])
		self.assertEqual(12, result["component_count"])
		self.assertEqual(15, result["public_file_count"])

	def test_legal_source_audit_rejects_missing_notice_and_stale_asset_register(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			(root / "art").mkdir()
			asset = root / "art/card.png"
			asset.write_bytes(b"image-v1")
			subprocess.run(["git", "init", "-q"], cwd=root, check=True)
			subprocess.run(["git", "add", "art/card.png"], cwd=root, check=True)

			(root / "legal/distribution/licenses").mkdir(parents=True)
			for relative in ("README.md", "OWNED_ASSET_RIGHTS_DECLARATION.md", "AI_CONTENT_RELEASE_RECORD.md"):
				(root / "legal" / relative).write_text("internal\n", encoding="utf-8")
			(root / "legal/distribution/THIRD_PARTY_NOTICES.txt").write_text("index\n", encoding="utf-8")
			(root / "legal/distribution/licenses/FIXTURE.txt").write_text("license\n", encoding="utf-8")
			(root / "legal/THIRD_PARTY_COMPONENTS.csv").write_text(
				"component,version_or_revision,category,shipped,license_or_terms,public_notice_files,upstream_source,notes\n"
				"Fixture,1,runtime,yes,MIT,FIXTURE.txt,local,test\n",
				encoding="utf-8",
			)
			register = root / "legal/ASSET_RIGHTS_REGISTER.csv"
			register.write_text(release_export.update_asset_rights_register.render_register(root), encoding="utf-8")

			passing = release_export.legal_source_audit(root, verify_pinned_notices=False)
			self.assertTrue(passing["ok"], passing["issues"])

			(root / "legal/distribution/licenses/FIXTURE.txt").unlink()
			asset.write_bytes(b"image-v2")
			failing = release_export.legal_source_audit(root, verify_pinned_notices=False)
			self.assertFalse(failing["ok"])
			self.assertTrue(any("FIXTURE.txt" in issue for issue in failing["issues"]))
			self.assertTrue(any("register is stale" in issue for issue in failing["issues"]))

	def test_legal_distribution_layout_and_hash_audit_cover_all_platforms(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			source = root / "distribution"
			(source / "licenses").mkdir(parents=True)
			(source / "THIRD_PARTY_NOTICES.txt").write_text("index\n", encoding="utf-8")
			(source / "licenses/ENGINE.txt").write_text("license\n", encoding="utf-8")

			for platform, executable_relative, expected_relative in (
				("windows", "Deckland.exe", "Legal"),
				("linux", "Deckland.x86_64", "Legal"),
				("macos", "Deckland.app/Contents/MacOS/Deckland", "Deckland.app/Contents/Resources/Legal"),
			):
				with self.subTest(platform=platform):
					package = root / platform
					executable = package / executable_relative
					executable.parent.mkdir(parents=True)
					executable.write_bytes(b"executable")
					destination, audit = release_export._install_legal_distribution(
						source, platform, package, executable
					)
					self.assertEqual(package / expected_relative, destination)
					self.assertTrue(audit["ok"])
					self.assertEqual(2, audit["file_count"])
					self.assertTrue(all(len(entry["sha256"]) == 64 for entry in audit["files"]))

	def test_packaged_legal_audit_blocks_missing_or_modified_files(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			source = root / "source"
			destination = root / "package/Legal"
			source.mkdir()
			destination.mkdir(parents=True)
			(source / "NOTICE.txt").write_text("expected\n", encoding="utf-8")
			with self.assertRaises(release_export.ReleaseError):
				release_export._audit_packaged_legal(source, destination)
			(destination / "NOTICE.txt").write_text("modified\n", encoding="utf-8")
			with self.assertRaises(release_export.ReleaseError):
				release_export._audit_packaged_legal(source, destination)

	def test_asset_register_blocks_untracked_unignored_media(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			(root / "art").mkdir()
			subprocess.run(["git", "init", "-q"], cwd=root, check=True)
			(root / "art/untracked.png").write_bytes(b"unreviewed")
			(root / "legal").mkdir()
			(root / "legal/ASSET_RIGHTS_REGISTER.csv").write_text("placeholder\n", encoding="utf-8")
			ok, message = release_export.update_asset_rights_register.check_register(root)
			self.assertFalse(ok)
			self.assertIn("Untracked, unignored media", message)

	def test_utf8_validator_reports_first_invalid_byte(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			(root / "valid.gd").write_text("extends Node\n", encoding="utf-8")
			(root / "invalid.tscn").write_bytes(b"abc\xffdef")
			result = release_export.validate_utf8_tree(root)
			self.assertFalse(result["ok"])
			self.assertEqual(3, result["issues"][0]["invalid_byte_offset"])

	def test_release_log_blockers_fail_even_if_process_would_succeed(self) -> None:
		for marker in release_export.LOG_BLOCKERS:
			with self.subTest(marker=marker):
				result = release_export.scan_log(f"normal output\n{marker}\n")
				self.assertFalse(result["ok"])
				self.assertEqual(1, result["blocker_counts"][marker])

	def test_runtime_smoke_requires_explicit_success_marker(self) -> None:
		with self.assertRaises(release_export.ReleaseError):
			release_export._validate_runtime_smoke_result("clean but incomplete\n", "EXPECTED_OK")
		with self.assertRaises(release_export.ReleaseError):
			release_export._validate_runtime_smoke_result(
				"EXPECTED_OK\nWRONG_BUILD\n",
				"EXPECTED_OK",
				required_exact_markers=("EXPECTED_BUILD",),
			)
		result = release_export._validate_runtime_smoke_result(
			"EXPECTED_BUILD\nEXPECTED_OK\n",
			"EXPECTED_OK",
			required_exact_markers=("EXPECTED_BUILD",),
		)
		self.assertTrue(result["ok"])

	def test_macos_adhoc_signature_normalization_uses_host_codesign_and_verifies(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			app = root / "Deckland.app"
			logs = root / "logs"
			app.mkdir()
			calls: list[tuple[list[str], Path, Path | None, int | None]] = []

			def capture_run(
				command: list[str],
				*,
				cwd: Path,
				log_path: Path | None = None,
				timeout: int | None = None,
			) -> subprocess.CompletedProcess[str]:
				calls.append((list(command), cwd, log_path, timeout))
				return subprocess.CompletedProcess(command, 0, "")

			with (
				mock.patch.object(release_export, "_host_platform", return_value="macos"),
				mock.patch.object(release_export.shutil, "which", return_value="/usr/bin/codesign"),
				mock.patch.object(release_export, "_run", side_effect=capture_run),
			):
				result = release_export._normalize_macos_adhoc_signature(app, logs)

			self.assertTrue(result["ok"])
			self.assertEqual("adhoc", result["identity"])
			self.assertEqual(2, len(calls))
			self.assertEqual(
				[
					"/usr/bin/codesign",
					"--force",
					"--deep",
					"--sign",
					"-",
					"--preserve-metadata=identifier,entitlements,requirements,flags,runtime",
					str(app),
				],
				calls[0][0],
			)
			self.assertEqual(
				[
					"/usr/bin/codesign",
					"--verify",
					"--deep",
					"--strict",
					"--verbose=2",
					str(app),
				],
				calls[1][0],
			)
			self.assertEqual(root, calls[0][1])
			self.assertEqual(logs / "macos-codesign-normalize.log", calls[0][2])
			self.assertEqual(logs / "macos-codesign-verify.log", calls[1][2])
			self.assertEqual(180, calls[0][3])
			self.assertEqual(str(logs / "macos-codesign-verify.log"), result["verify_log"])
			self.assertTrue(result["verify_log_audit"]["ok"])
			self.assertEqual(64, len(result["signed_bundle_sha256"]))

	def test_macos_adhoc_signature_normalization_rejects_non_macos_host(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			app = Path(temp) / "Deckland.app"
			app.mkdir()
			with (
				mock.patch.object(release_export, "_host_platform", return_value="linux"),
				self.assertRaisesRegex(release_export.ReleaseError, "requires a macOS host"),
			):
				release_export._normalize_macos_adhoc_signature(app, Path(temp) / "logs")

	def test_macos_signature_is_normalized_after_legal_files_are_installed(self) -> None:
		source = (ROOT / "tools/release_export.py").read_text(encoding="utf-8")
		install = source.index("staged_legal_dir, staged_legal = _install_legal_distribution(", source.index("def run_release"))
		normalize = source.index("_normalize_macos_adhoc_signature(export_target, logs_dir)", install)
		runtime = source.index("runtime = _runtime_smoke(", normalize)
		self.assertLess(install, normalize)
		self.assertLess(normalize, runtime)

	def test_missing_release_assembly_is_detected(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			self.assertEqual([], release_export._find_pokegame_assembly(root))
			(root / "data").mkdir()
			assembly = root / "data/pokegame.dll"
			assembly.write_bytes(b"assembly")
			self.assertEqual([assembly], release_export._find_pokegame_assembly(root))

	def test_size_baseline_warns_and_blocks_at_configured_thresholds(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			baseline_path = Path(temp) / "baseline.json"
			baseline_path.write_text(
				json.dumps(
					{
						"windows": {
							"release_directory_size_bytes": 100 * 1024 * 1024,
							"pck_size_bytes": 100 * 1024 * 1024,
						}
					}
				),
				encoding="utf-8",
			)
			original_path = release_export.BASELINE_PATH
			try:
				release_export.BASELINE_PATH = baseline_path
				warning = release_export._size_baseline(
					"windows", {"size_bytes": 106 * 1024 * 1024}, 106 * 1024 * 1024
				)
				blocked = release_export._size_baseline(
					"windows", {"size_bytes": 116 * 1024 * 1024}, 116 * 1024 * 1024
				)
			finally:
				release_export.BASELINE_PATH = original_path
			self.assertEqual("warning", warning["status"])
			self.assertEqual("blocked", blocked["status"])


@unittest.skipUnless(GODOT.is_file(), "Godot 4.6 .NET executable is unavailable")
class ReleasePackageAuditIntegrationTests(unittest.TestCase):
	def _write_fixture(
		self,
		root: Path,
		*,
		include_data: bool = True,
		csharp_content: str = "",
		include_csharp_source: bool = False,
		extra_files: dict[str, str] | None = None,
		autoload_mcp: bool = False,
	) -> None:
		project = '''[application]
config/name="Release Audit Fixture"
run/main_scene="res://main.tscn"

[internationalization]
locale/translations=PackedStringArray("res://localization/ui.en.tres")

[rendering]
renderer/rendering_method="gl_compatibility"
'''
		if autoload_mcp:
			project += '\n[autoload]\nMCPScreenshot="*res://runtime_mcp.gd"\n'
		(root / "project.godot").write_text(project, encoding="utf-8")
		(root / "export_presets.cfg").write_text(
			f'''[preset.0]
name="Fixture"
platform="Windows Desktop"
export_filter="all_resources"
include_filter="data.pandora,localization/ui.csv,tmp/**,addons/godot_mcp/**,scripts/*.cs"
exclude_filter=""
[preset.0.options]
dotnet/include_scripts_content={str(include_csharp_source).lower()}
dotnet/include_debug_symbols=false
''',
			encoding="utf-8",
		)
		(root / "main.tscn").write_text(
			'[gd_scene format=3]\n\n[node name="Main" type="Node"]\n', encoding="utf-8"
		)
		for relative in (
			"addons/pandora/api.gd",
			"addons/pandora/backend/entity_backend.gd",
			"addons/pandora/model/entity.gd",
			"addons/pandora/model/type.gd",
		):
			path = root / relative
			path.parent.mkdir(parents=True, exist_ok=True)
			path.write_text("extends Node\n", encoding="utf-8")
		(root / "localization").mkdir()
		(root / "localization/ui.en.tres").write_text(
			'[gd_resource type="Translation" format=3]\n\n[resource]\nlocale = "en"\n', encoding="utf-8"
		)
		(root / "localization/ui.csv").write_text("keys,en\nHELLO,Hello\n", encoding="utf-8")
		if include_data:
			generator = root / "make_fixture_data.gd"
			generator.write_text(
				'''extends SceneTree
func _initialize() -> void:
\tvar file := FileAccess.open_compressed("res://data.pandora", FileAccess.WRITE)
\tfile.store_string("{\\\"fixture\\\":true}")
\tfile.close()
\tquit(0)
''',
				encoding="utf-8",
			)
			result = subprocess.run(
				[str(GODOT), "--headless", "--path", str(root), "--script", str(generator)],
				text=True,
				stdout=subprocess.PIPE,
				stderr=subprocess.STDOUT,
				check=False,
			)
			self.assertEqual(0, result.returncode, result.stdout)
			generator.unlink()
		(root / "scripts").mkdir()
		(root / "scripts/Runtime.cs").write_text(csharp_content, encoding="utf-8")
		if autoload_mcp:
			(root / "runtime_mcp.gd").write_text("extends Node\n", encoding="utf-8")
		for relative, content in (extra_files or {}).items():
			path = root / relative
			path.parent.mkdir(parents=True, exist_ok=True)
			path.write_text(content, encoding="utf-8")

	def _export_and_audit(self, fixture: Path) -> tuple[subprocess.CompletedProcess[str], dict[str, object]]:
		pck = fixture.parent / f"{fixture.name}.pck"
		export = subprocess.run(
			[str(GODOT), "--headless", "--path", str(fixture), "--export-pack", "Fixture", str(pck)],
			text=True,
			stdout=subprocess.PIPE,
			stderr=subprocess.STDOUT,
			check=False,
		)
		self.assertEqual(0, export.returncode, export.stdout)
		self.assertTrue(pck.is_file(), export.stdout)
		harness = fixture.parent / f"{fixture.name}-audit"
		harness.mkdir()
		(harness / "project.godot").write_text(
			'[application]\nconfig/name="Audit Harness"\n', encoding="utf-8"
		)
		report_path = fixture.parent / f"{fixture.name}-report.json"
		command = [
			str(GODOT),
			"--headless",
			"--path",
			str(harness),
			"--script",
			str(ROOT / "tools/export_package_content_audit.gd"),
			"--",
			"--artifact",
			str(pck),
			"--platform",
			"windows",
			"--report",
			str(report_path),
			"--main-scene",
			"res://main.tscn",
			"--translation",
			"res://localization/ui.en.tres",
		]
		result = subprocess.run(
			command,
			cwd=harness,
			text=True,
			stdout=subprocess.PIPE,
			stderr=subprocess.STDOUT,
			check=False,
		)
		self.assertTrue(report_path.is_file(), result.stdout)
		return result, json.loads(report_path.read_text(encoding="utf-8"))

	def test_clean_package_accepts_empty_csharp_placeholder(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			fixture = Path(temp) / "clean"
			fixture.mkdir()
			self._write_fixture(fixture)
			result, report = self._export_and_audit(fixture)
			self.assertEqual(0, result.returncode, result.stdout + "\n" + json.dumps(report, indent=2))
			self.assertTrue(report["ok"])
			self.assertEqual(0, report["csharp"]["source_content_count"])

	def test_package_blocks_forbidden_paths_and_mcp_autoload(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			fixture = Path(temp) / "forbidden"
			fixture.mkdir()
			self._write_fixture(
				fixture,
				extra_files={
					"tmp/leak.txt": "leak\n",
					"addons/godot_mcp/plugin.gd": "extends Node\n",
					"scripts/GameBoardScene.Debug.Network.cs": "debug network\n",
					"scripts/GameBoardScene.DebugHooks.cs": "debug hooks\n",
					"scripts/GameBoardScene.DebugRecording.cs": "debug recording\n",
					"art/fx/processed/effect.pipeline-meta.json": "{}\n",
				},
				autoload_mcp=True,
			)
			result, report = self._export_and_audit(fixture)
			self.assertNotEqual(0, result.returncode, result.stdout)
			self.assertFalse(report["ok"])
			self.assertTrue(any(path.startswith("tmp/") for path in report["forbidden_matches"]))
			self.assertIn("scripts/GameBoardScene.Debug.Network.cs", report["forbidden_matches"])
			self.assertIn("art/fx/processed/effect.pipeline-meta.json", report["forbidden_matches"])
			self.assertNotIn("scripts/GameBoardScene.DebugHooks.cs", report["forbidden_matches"])
			self.assertNotIn("scripts/GameBoardScene.DebugRecording.cs", report["forbidden_matches"])
			self.assertIn("scripts/GameBoardScene.DebugHooks.cs", report["file_entries"])
			self.assertIn("scripts/GameBoardScene.DebugRecording.cs", report["file_entries"])
			self.assertTrue(any("MCP autoload" in issue for issue in report["issues"]))

	def test_package_blocks_recoverable_csharp_source(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			fixture = Path(temp) / "csharp-source"
			fixture.mkdir()
			self._write_fixture(
				fixture,
				csharp_content="public sealed class LeakedSource {}\n",
				include_csharp_source=True,
			)
			result, report = self._export_and_audit(fixture)
			self.assertNotEqual(0, result.returncode, result.stdout)
			self.assertGreater(report["csharp"]["source_content_count"], 0)

	def test_package_blocks_missing_pandora_data(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			fixture = Path(temp) / "missing-data"
			fixture.mkdir()
			self._write_fixture(fixture, include_data=False)
			result, report = self._export_and_audit(fixture)
			self.assertNotEqual(0, result.returncode, result.stdout)
			self.assertTrue(any("data.pandora" in issue for issue in report["issues"]))

	def test_auditor_without_actual_pck_fails_instead_of_scanning_workspace(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			harness = Path(temp)
			(harness / "project.godot").write_text(
				'[application]\nconfig/name="No Pack"\n', encoding="utf-8"
			)
			result = subprocess.run(
				[
					str(GODOT),
					"--headless",
					"--path",
					str(harness),
					"--script",
					str(ROOT / "tools/export_package_content_audit.gd"),
				],
				cwd=harness,
				text=True,
				stdout=subprocess.PIPE,
				stderr=subprocess.STDOUT,
				check=False,
			)
			self.assertNotEqual(0, result.returncode, result.stdout)
			self.assertIn("Missing required --artifact PCK path", result.stdout)


if __name__ == "__main__":
	unittest.main(verbosity=2)
