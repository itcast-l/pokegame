#!/usr/bin/env python3

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))

import verify_release_target_runtime as target_runtime  # noqa: E402


class ReleaseTargetRuntimeTests(unittest.TestCase):
	def _sha256(self, path: Path) -> str:
		return hashlib.sha256(path.read_bytes()).hexdigest()

	def _fixture(self, root: Path, platform: str = "linux") -> tuple[Path, Path, Path]:
		package = root / "package"
		package.mkdir()
		executable = package / ("Deckland.exe" if platform == "windows" else "Deckland.x86_64")
		pck = package / "Deckland.pck"
		executable.write_bytes(b"release-executable")
		executable.chmod(0o755)
		pck.write_bytes(b"release-pck")
		report = {
			"schema_version": 1,
			"status": "target_runtime_pending",
			"build_id": "release-fixture",
			"network_protocol": 11,
			"preset": "Linux" if platform == "linux" else "Windows Desktop",
			"platform": platform,
			"source_integrity": {"unchanged": True},
			"package_audit": {"content": {"ok": True}},
			"runtime_smoke": {"pack_harness": {"ok": True}},
			"log_error_counts": {marker: 0 for marker in target_runtime.LOG_BLOCKERS},
			"artifact": {
				"release_directory_relative_path": "package",
				"executable_relative_path": executable.relative_to(root).as_posix(),
				"executable_sha256": self._sha256(executable),
				"pck_relative_path": pck.relative_to(root).as_posix(),
				"pck_sha256": self._sha256(pck),
			},
		}
		report_path = root / "release-audit.json"
		report_path.write_text(json.dumps(report), encoding="utf-8")
		return report_path, executable, pck

	def test_verifies_hashes_and_exact_target_marker(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path, executable, _ = self._fixture(root)
			output = root / "evidence/linux-runtime.json"
			log = root / "evidence/linux-runtime.log"

			def runner(command: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
				self.assertEqual(executable.resolve(), Path(command[0]))
				self.assertIn("--audio-driver", command)
				self.assertEqual("1", kwargs["env"]["DECKLAND_TEST_MUTE_AUDIO"])  # type: ignore[index]
				return subprocess.CompletedProcess(
					command,
					0,
					"RELEASE_BUILD_INFO build_id=release-fixture protocol=11 platform=Linux\n"
					"RELEASE_EXECUTABLE_SMOKE_OK platform=Linux\n",
				)

			evidence = target_runtime.verify_target_runtime(
				report_path,
				output,
				log,
				"linux-host",
				host_platform="linux",
				runner=runner,
			)
			self.assertEqual("passed", evidence["status"])
			self.assertEqual("release-fixture", evidence["build_id"])
			self.assertEqual(11, evidence["network_protocol"])
			self.assertEqual(evidence["artifacts"]["pck"]["expected_sha256"], evidence["artifacts"]["pck"]["actual_sha256"])
			self.assertEqual(self._sha256(log), evidence["runtime_smoke"]["log_sha256"])

	def test_hash_mismatch_fails_before_execution(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path, _, pck = self._fixture(root)
			pck.write_bytes(b"tampered")

			def runner(*args: object, **kwargs: object) -> subprocess.CompletedProcess[str]:
				raise AssertionError("runtime must not execute after a hash mismatch")

			evidence = target_runtime.verify_target_runtime(
				report_path,
				root / "evidence.json",
				root / "evidence.log",
				"linux-host",
				host_platform="linux",
				runner=runner,
			)
			self.assertEqual("failed", evidence["status"])
			self.assertIn("release PCK SHA-256 mismatch", evidence["error"])

	def test_cross_platform_execution_is_rejected(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path, _, _ = self._fixture(root, "windows")
			evidence = target_runtime.verify_target_runtime(
				report_path,
				root / "evidence.json",
				root / "evidence.log",
				"linux-host",
				host_platform="linux",
			)
			self.assertEqual("failed", evidence["status"])
			self.assertIn("targets windows, but this host is linux", evidence["error"])

	def test_generic_marker_without_platform_is_rejected(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path, _, _ = self._fixture(root)

			def runner(command: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
				return subprocess.CompletedProcess(
					command,
					0,
					"RELEASE_BUILD_INFO build_id=release-fixture protocol=11 platform=Linux\n"
					"RELEASE_EXECUTABLE_SMOKE_OK\n",
				)

			evidence = target_runtime.verify_target_runtime(
				report_path,
				root / "evidence.json",
				root / "evidence.log",
				"linux-host",
				host_platform="linux",
				runner=runner,
			)
			self.assertEqual("failed", evidence["status"])
			self.assertIn("did not emit exact marker", evidence["error"])

	def test_build_marker_must_match_audited_candidate(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			report_path, _, _ = self._fixture(root)

			def runner(command: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
				return subprocess.CompletedProcess(
					command,
					0,
					"RELEASE_BUILD_INFO build_id=other-build protocol=11 platform=Linux\n"
					"RELEASE_EXECUTABLE_SMOKE_OK platform=Linux\n",
				)

			evidence = target_runtime.verify_target_runtime(
				report_path,
				root / "evidence.json",
				root / "evidence.log",
				"linux-host",
				host_platform="linux",
				runner=runner,
			)
			self.assertEqual("failed", evidence["status"])
			self.assertIn("did not emit exact build marker", evidence["error"])


if __name__ == "__main__":
	unittest.main()
