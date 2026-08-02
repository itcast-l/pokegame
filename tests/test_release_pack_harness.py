#!/usr/bin/env python3

from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))

import release_pack_harness  # noqa: E402
import run_release_lan_host_migration_gate as release_gate  # noqa: E402


class ReleasePackHarnessTests(unittest.TestCase):
	def _sha256(self, path: Path) -> str:
		return hashlib.sha256(path.read_bytes()).hexdigest()

	def _fixture(self, root: Path, platform: str = "linux", machine: str = "x86_64") -> Path:
		package = root / "package"
		if platform == "macos":
			resources = package / "Deckland.app/Contents/Resources"
			frameworks = package / "Deckland.app/Contents/Frameworks"
			executable = package / "Deckland.app/Contents/MacOS/Deckland"
			pck = resources / "Deckland.pck"
			runtime_name = f"data_pokegame_macos_{machine}"
			native_files = (
				frameworks / "libgodotsteam.macos.template_release.dylib",
				frameworks / "libsteam_api.dylib",
			)
		else:
			resources = package
			executable = package / ("Deckland.exe" if platform == "windows" else "Deckland.x86_64")
			pck = package / "Deckland.pck"
			if platform == "windows":
				runtime_name = "data_pokegame_windows_x86_64"
				native_files = (
					package / "libgodotsteam.windows.template_release.x86_64.dll",
					package / "steam_api64.dll",
				)
			else:
				runtime_name = f"data_pokegame_linuxbsd_{machine}"
				native_files = (
					package / f"libgodotsteam.linux.template_release.{machine}.so",
					package / "libsteam_api.so",
				)
		resources.mkdir(parents=True, exist_ok=True)
		executable.parent.mkdir(parents=True, exist_ok=True)
		executable.write_bytes(b"release-executable")
		executable.chmod(0o755)
		pck.write_bytes(b"release-pck")
		runtime = resources / runtime_name
		runtime.mkdir()
		(runtime / "pokegame.dll").write_bytes(b"assembly")
		for native_file in native_files:
			native_file.parent.mkdir(parents=True, exist_ok=True)
			native_file.write_bytes(b"native")
		report = {
			"schema_version": 1,
			"status": "passed",
			"build_id": "release-pack-fixture",
			"network_protocol": 11,
			"preset": platform,
			"platform": platform,
			"source_integrity": {"unchanged": True},
			"package_audit": {"content": {"ok": True}},
			"runtime_smoke": {"pack_harness": {"ok": True}},
			"log_error_counts": {marker: 0 for marker in release_pack_harness.release_runtime.LOG_BLOCKERS},
			"artifact": {
				"release_directory_relative_path": "package",
				"executable_relative_path": executable.relative_to(root).as_posix(),
				"executable_sha256": self._sha256(executable),
				"pck_relative_path": pck.relative_to(root).as_posix(),
				"pck_sha256": self._sha256(pck),
				"assembly_paths": [f"{runtime_name}/pokegame.dll"],
			},
		}
		report_path = root / "release-audit.json"
		report_path.write_text(json.dumps(report), encoding="utf-8")
		return report_path

	def test_linux_workspace_maps_release_runtime_to_debug_paths(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path = self._fixture(root)
			release_pack = release_pack_harness.load_release_pack(report_path, host_platform="linux")
			workspace = release_pack_harness.prepare_release_pack_harness(
				release_pack,
				root / "workspace",
				machine="x86_64",
			)
			self.assertTrue((workspace.root / ".godot/mono/temp/bin/Debug/pokegame.dll").is_file())
			self.assertTrue(
				(workspace.root / "addons/godotsteam/linux64/libgodotsteam.linux.template_debug.x86_64.so").is_file()
			)
			self.assertTrue((workspace.root / "addons/godotsteam/linux64/libsteam_api.so").is_file())

	def test_macos_arm64_workspace_selects_matching_assembly(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path = self._fixture(root, "macos", "arm64")
			release_pack = release_pack_harness.load_release_pack(report_path, host_platform="macos")
			workspace = release_pack_harness.prepare_release_pack_harness(
				release_pack,
				root / "workspace",
				machine="arm64",
			)
			self.assertTrue((workspace.root / ".godot/mono/temp/bin/Debug/pokegame.dll").is_file())
			self.assertTrue(
				(workspace.root / "addons/godotsteam/osx/libgodotsteam.macos.template_debug.dylib").is_file()
			)

	def test_windows_workspace_maps_release_runtime_to_debug_paths(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path = self._fixture(root, "windows")
			release_pack = release_pack_harness.load_release_pack(report_path, host_platform="windows")
			workspace = release_pack_harness.prepare_release_pack_harness(
				release_pack,
				root / "workspace",
				machine="AMD64",
			)
			self.assertTrue((workspace.root / ".godot/mono/temp/bin/Debug/pokegame.dll").is_file())
			self.assertTrue(
				(workspace.root / "addons/godotsteam/win64/libgodotsteam.windows.template_debug.x86_64.dll").is_file()
			)
			self.assertTrue((workspace.root / "addons/godotsteam/win64/steam_api64.dll").is_file())

	def test_hash_mismatch_is_rejected_before_workspace_setup(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path = self._fixture(root)
			(root / "package/Deckland.pck").write_bytes(b"tampered")
			with self.assertRaisesRegex(release_pack_harness.HarnessError, "PCK SHA-256 mismatch"):
				release_pack_harness.load_release_pack(report_path, host_platform="linux")

	def test_cross_platform_release_pack_is_rejected(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path = self._fixture(root, "windows")
			with self.assertRaisesRegex(release_pack_harness.HarnessError, "targets windows, but this host is linux"):
				release_pack_harness.load_release_pack(report_path, host_platform="linux")

	def test_command_and_environment_force_isolated_muted_execution(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path = self._fixture(root)
			release_pack = release_pack_harness.load_release_pack(report_path, host_platform="linux")
			workspace = release_pack_harness.prepare_release_pack_harness(
				release_pack,
				root / "workspace",
				machine="x86_64",
			)
			godot = root / "Godot"
			godot.write_bytes(b"binary")
			script = root / "Harness.gd"
			script.write_text("extends SceneTree\n", encoding="utf-8")
			command = release_pack_harness.build_harness_command(
				godot,
				release_pack,
				workspace,
				script,
				("--fixture=1",),
			)
			self.assertIn("--main-pack", command)
			self.assertIn(str(release_pack.pck), command)
			self.assertEqual("--fixture=1", command[-1])
			environment = release_pack_harness.build_harness_environment(workspace, base={})
			self.assertEqual("1", environment["DECKLAND_TEST_MUTE_AUDIO"])
			self.assertEqual(str(workspace.home), environment["HOME"])

	def test_target_runtime_evidence_must_bind_report_artifacts_and_raw_log(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path = self._fixture(root)
			release_pack = release_pack_harness.load_release_pack(report_path, host_platform="linux")
			build_marker = "RELEASE_BUILD_INFO build_id=release-pack-fixture protocol=11 platform=Linux"
			success_marker = "RELEASE_EXECUTABLE_SMOKE_OK platform=Linux"
			runtime_log = root / "target-runtime.log"
			runtime_log.write_text(f"{build_marker}\n{success_marker}\n", encoding="utf-8")
			evidence = {
				"schema_version": 1,
				"status": "passed",
				"device_id": "linux-fixture",
				"build_id": release_pack.build_id,
				"network_protocol": release_pack.network_protocol,
				"platform": release_pack.platform,
				"release_audit": {"sha256": release_pack.report_sha256},
				"artifacts": {
					kind: {"actual_sha256": release_pack.artifacts[kind]["actual_sha256"]}
					for kind in ("executable", "pck")
				},
				"runtime_smoke": {
					"exit_code": 0,
					"blocker_counts": {marker: 0 for marker in release_gate.LOG_BLOCKERS},
					"log_path": runtime_log.name,
					"log_sha256": self._sha256(runtime_log),
					"build_marker": build_marker,
					"success_marker": success_marker,
				},
			}
			evidence_path = root / "target-runtime.json"
			evidence_path.write_text(json.dumps(evidence), encoding="utf-8")
			validated = release_gate.validate_target_runtime_evidence(evidence_path, release_pack)
			self.assertEqual("linux-fixture", validated["device_id"])
			evidence["artifacts"]["pck"]["actual_sha256"] = "0" * 64
			evidence_path.write_text(json.dumps(evidence), encoding="utf-8")
			with self.assertRaisesRegex(release_gate.GateError, "pck hash does not match"):
				release_gate.validate_target_runtime_evidence(evidence_path, release_pack)

	def test_migrated_result_assertions_compare_all_public_fields(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			result_dir = Path(temp)
			lines = [
				"role=host",
				"seat=1",
				"winner=1",
				f"fingerprint={'a' * 64}",
				"snapshot_id=9",
				"private_leak=false",
				"table_count=0",
			]
			for field in ("score", "deck", "hand", "item_hand"):
				for index in range(3):
					lines.append(f"{field}_{index}=0")
			payload = "\n".join(lines) + "\n"
			for seat in range(3):
				(result_dir / f"expected_{seat}.result").write_text(payload, encoding="utf-8")
			for seat in (1, 2):
				(result_dir / f"client_{seat}.result").write_text(payload, encoding="utf-8")
			assertions = release_gate.assert_migrated_results(result_dir)
			self.assertIn("client_2_matches_authority", assertions)
			(result_dir / "client_2.result").write_text(payload.replace("score_1=0", "score_1=1"), encoding="utf-8")
			with self.assertRaisesRegex(release_gate.GateError, "public state mismatch"):
				release_gate.assert_migrated_results(result_dir)


if __name__ == "__main__":
	unittest.main()
