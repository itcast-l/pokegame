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

import validate_host_migration_release_evidence as evidence  # noqa: E402


class HostMigrationReleaseEvidenceTests(unittest.TestCase):
	def _assertions(self, *extra: str) -> dict[str, bool]:
		return {key: True for key in evidence.COMMON_ASSERTIONS | set(extra)}

	def _write(self, root: Path, relative: str, content: str) -> str:
		path = root / relative
		path.parent.mkdir(parents=True, exist_ok=True)
		path.write_text(content, encoding="utf-8")
		return relative

	def _sha256(self, path: Path) -> str:
		return hashlib.sha256(path.read_bytes()).hexdigest()

	def _runtime_artifacts(self, root: Path, platform: str, build_id: str, device_id: str) -> tuple[str, str]:
		executable_hash = hashlib.sha256(f"{platform}-executable".encode()).hexdigest()
		pck_hash = hashlib.sha256(f"{platform}-pck".encode()).hexdigest()
		audit = {
			"schema_version": 1,
			"status": "target_runtime_pending",
			"build_id": build_id,
			"network_protocol": evidence.EXPECTED_NETWORK_PROTOCOL,
			"platform": platform,
			"source_integrity": {"unchanged": True},
			"package_audit": {"content": {"ok": True}},
			"runtime_smoke": {"pack_harness": {"ok": True}},
			"log_error_counts": {marker: 0 for marker in evidence.RELEASE_LOG_BLOCKERS},
			"artifact": {
				"executable_sha256": executable_hash,
				"pck_sha256": pck_hash,
			},
		}
		audit_relative = self._write(root, f"runtime/{platform}/release-audit.json", json.dumps(audit))
		audit_path = root / audit_relative
		build_marker = (
			f"RELEASE_BUILD_INFO build_id={build_id} "
			f"protocol={evidence.EXPECTED_NETWORK_PROTOCOL} "
			f"platform={evidence.GODOT_PLATFORM_NAMES[platform]}"
		)
		marker = f"RELEASE_EXECUTABLE_SMOKE_OK platform={evidence.GODOT_PLATFORM_NAMES[platform]}"
		log_relative = self._write(root, f"runtime/{platform}/target-runtime.log", f"{build_marker}\n{marker}\n")
		log_path = root / log_relative
		runtime_evidence = {
			"schema_version": 1,
			"status": "passed",
			"started_at_utc": "2026-08-01T10:00:00Z",
			"completed_at_utc": "2026-08-01T10:00:10Z",
			"build_id": build_id,
			"network_protocol": evidence.EXPECTED_NETWORK_PROTOCOL,
			"platform": platform,
			"device_id": device_id,
			"host": {
				"platform": platform,
				"system": evidence.GODOT_PLATFORM_NAMES[platform],
				"release": "fixture",
				"machine": "x86_64",
				"hostname": device_id,
			},
			"release_audit": {"sha256": self._sha256(audit_path)},
			"artifacts": {
				"executable": {"size_bytes": 10, "expected_sha256": executable_hash, "actual_sha256": executable_hash},
				"pck": {"size_bytes": 20, "expected_sha256": pck_hash, "actual_sha256": pck_hash},
			},
			"runtime_smoke": {
				"started_at_utc": "2026-08-01T10:00:01Z",
				"ended_at_utc": "2026-08-01T10:00:09Z",
				"duration_seconds": 8.0,
				"exit_code": 0,
				"build_marker": build_marker,
				"success_marker": marker,
				"log_path": "target-runtime.log",
				"log_sha256": self._sha256(log_path),
				"blocker_counts": {marker: 0 for marker in evidence.RELEASE_LOG_BLOCKERS},
			},
		}
		evidence_relative = self._write(
			root,
			f"runtime/{platform}/target-runtime.json",
			json.dumps(runtime_evidence),
		)
		return audit_relative, evidence_relative

	def _result(self, build_id: str, scenario: str, label: str, steam_id: str, outcome: str, winner: int) -> dict[str, object]:
		return {
			"schema_version": 1,
			"build_id": build_id,
			"scenario": scenario,
			"peer_label": label,
			"steam_id": steam_id,
			"outcome": outcome,
			"winner": winner,
		}

	def test_complete_release_matrix_is_accepted(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			root = Path(temp)
			build_id = "release-fixture"
			accounts = [
				{"label": "account-a", "steam_id": "76561198000000001", "platform": "windows", "device_id": "device-a"},
				{"label": "account-b", "steam_id": "76561198000000002", "platform": "linux", "device_id": "device-b"},
				{"label": "account-c", "steam_id": "76561198000000003", "platform": "macos", "device_id": "device-c"},
			]
			steam_cases = []
			case_markers = {
				"active_owner_transfer": "MIGRATION_ACTIVE_BEGIN\nMIGRATION_HOST_BOUND\nMIGRATION_CHECKPOINT_RESTORED\nkind=WINNER\n",
				"source_crash": "kind=SOURCE_KILL_READY\nMIGRATION_HOST_BOUND\nMIGRATION_CHECKPOINT_RESTORED\nkind=WINNER\n",
				"automatic_owner_second_transfer": "kind=SECONDARY_OWNER_REQUEST\nMIGRATION_HOST_BOUND\nkind=WINNER\n",
				"owner_timeout": "MIGRATION_ACTIVE_FAILED reason=SteamOwnerTimeout\nkind=SAFE_END\n",
				"candidate_exit": "kind=SOURCE_KILL_READY\nMIGRATION_PLAN_READY phase=InGame candidate_seat=1\nMIGRATION_PLAN_READY phase=InGame candidate_seat=2\n",
			}
			for case_id in sorted(evidence.REQUIRED_STEAM_CASES):
				artifacts = []
				for index, account in enumerate(accounts):
					label = str(account["label"])
					artifact = {
						"account_label": label,
						"log": self._write(root, f"steam/{case_id}/{label}.log", case_markers[case_id] if index == 0 else "kind=DONE outcome=continue\n"),
					}
					force_terminated = (case_id in {"source_crash", "automatic_owner_second_transfer"} and index == 0) or (case_id == "candidate_exit" and index < 2)
					if force_terminated:
						artifact["force_terminated"] = True
					else:
						outcome = "safe_end" if case_id == "owner_timeout" else ("source_exit" if case_id == "active_owner_transfer" and index == 0 else "continue")
						winner = 1 if outcome == "continue" else -1
						artifact["result"] = self._write(
							root,
							f"steam/{case_id}/{label}.json",
							json.dumps(self._result(build_id, case_id, label, str(account["steam_id"]), outcome, winner)),
						)
					artifacts.append(artifact)
				timeline = [
					{"at_utc": "2026-08-01T10:00:00Z", "event": "case_started"},
					{"at_utc": "2026-08-01T10:01:00Z", "event": "migration_started"},
					{"at_utc": "2026-08-01T10:02:00Z", "event": "case_finished"},
				]
				if case_id in {"source_crash", "automatic_owner_second_transfer"}:
					timeline[1]["event"] = "source_force_killed"
				if case_id == "candidate_exit":
					timeline[1]["event"] = "candidate_force_killed"
				if case_id == "automatic_owner_second_transfer":
					timeline = [
						{"at_utc": "2026-08-01T10:00:00Z", "event": "lobby_owner_changed", "steam_id": accounts[0]["steam_id"]},
						{"at_utc": "2026-08-01T10:00:30Z", "event": "lobby_owner_changed", "steam_id": accounts[2]["steam_id"]},
						{"at_utc": "2026-08-01T10:01:00Z", "event": "source_force_killed"},
						{"at_utc": "2026-08-01T10:01:30Z", "event": "lobby_owner_changed", "steam_id": accounts[1]["steam_id"]},
					]
				steam_cases.append(
					{
						"id": case_id,
						"status": "passed",
						"started_at_utc": "2026-08-01T10:00:00Z",
						"ended_at_utc": "2026-08-01T10:03:00Z",
						"account_labels": [account["label"] for account in accounts],
						"artifacts": artifacts,
						"timeline": timeline,
						"assertions": self._assertions(*evidence.STEAM_CASE_ASSERTIONS[case_id]),
					}
				)

			real_lan_runs = []
			for platform in sorted(evidence.REQUIRED_LAN_PLATFORMS):
				build_marker = (
					f"RELEASE_BUILD_INFO build_id={build_id} "
					f"protocol={evidence.EXPECTED_NETWORK_PROTOCOL} "
					f"platform={evidence.GODOT_PLATFORM_NAMES[platform]}"
				)
				audit_path, runtime_evidence_path = self._runtime_artifacts(
					root,
					platform,
					build_id,
					f"{platform}-host",
				)
				real_lan_runs.append(
					{
						"id": platform,
						"status": "passed",
						"build_id": build_id,
						"started_at_utc": "2026-08-01T11:00:00Z",
						"ended_at_utc": "2026-08-01T11:02:00Z",
						"host_device_id": f"{platform}-host",
						"client_device_id": f"{platform}-client",
						"release_audit": audit_path,
						"target_runtime_evidence": runtime_evidence_path,
						"host_log": self._write(root, f"lan/{platform}/host.log", f"{build_marker}\nMIGRATION_PLAN_READY\nREAL_LAN_HOST_KILL_9\n"),
						"client_log": self._write(root, f"lan/{platform}/client.log", f"{build_marker}\nMIGRATION_HOST_BOUND\nMIGRATION_CHECKPOINT_RESTORED\nMIGRATION_RECOVERY_READY\nFULL_MATCH_CLIENT_WINNER\nFULL_MATCH_CLIENT_DONE\n"),
						"result": self._write(root, f"lan/{platform}/client.result", "private_leak=false\n"),
						"assertions": self._assertions("two_physical_devices", "same_build", "private_state_not_leaked"),
					}
				)

			network_cases = []
			for case_id in sorted(evidence.REQUIRED_NETWORK_CASES):
				if case_id == "host_candidate_simultaneous_loss":
					markers = f"LAN_NETWORK_IMPAIRMENT_CASE_OK {case_id}\nMIGRATION_FAILED code=ConnectTimeout\nartifacts=false\n"
				elif case_id == "short_disconnect":
					markers = f"LAN_NETWORK_IMPAIRMENT_CASE_OK {case_id}\nFULL_MATCH_CLIENT_STATE 8\nNETWORK_IMPAIRMENT_CLEARED\n"
				else:
					markers = f"LAN_NETWORK_IMPAIRMENT_CASE_OK {case_id}\nNETWORK_IMPAIRMENT_APPLIED\nFULL_MATCH_CLIENT_WINNER\n"
				network_cases.append(
					{
						"id": case_id,
						"status": "passed",
						"build_id": build_id,
						"started_at_utc": "2026-08-01T12:00:00Z",
						"ended_at_utc": "2026-08-01T12:02:00Z",
						"driver": "tc netem",
						"logs": [self._write(root, f"network/{case_id}.log", markers)],
						"assertions": self._assertions(
							"external_process_termination" if case_id == "host_candidate_simultaneous_loss" else "real_udp_impairment",
							"final_state_consistent",
						),
					}
				)

			manifest = {
				"schema_version": 1,
				"release_id": "release-fixture",
				"evidence_root": ".",
				"build": {
					"build_id": build_id,
					"configuration": "Release",
					"network_protocol": evidence.EXPECTED_NETWORK_PROTOCOL,
				},
				"accounts": accounts,
				"steam_cases": steam_cases,
				"real_lan_runs": real_lan_runs,
				"network_impairment_cases": network_cases,
			}
			manifest_path = root / "manifest.json"
			manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
			report = evidence.validate_manifest(manifest_path)
			self.assertTrue(report.ok, "\n".join(report.errors))

	def test_pending_template_is_structurally_valid_only_when_allowed(self) -> None:
		template = ROOT / "tests/HOST_MIGRATION_RELEASE_EVIDENCE_TEMPLATE.json"
		allowed = evidence.validate_manifest(template, allow_pending=True)
		self.assertTrue(allowed.ok, "\n".join(allowed.errors))
		self.assertEqual(13, len(allowed.warnings))
		blocked = evidence.validate_manifest(template)
		self.assertFalse(blocked.ok)
		self.assertTrue(any("status must be passed" in error for error in blocked.errors))

	def test_duplicate_steam_accounts_are_rejected(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			manifest_path = Path(temp) / "evidence.json"
			manifest = json.loads((ROOT / "tests/HOST_MIGRATION_RELEASE_EVIDENCE_TEMPLATE.json").read_text(encoding="utf-8"))
			manifest["accounts"] = [
				{"label": "a", "steam_id": "76561198000000001", "platform": "windows", "device_id": "device-a"},
				{"label": "b", "steam_id": "76561198000000001", "platform": "linux", "device_id": "device-b"},
			]
			manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
			report = evidence.validate_manifest(manifest_path, scope="steam", allow_pending=True)
			self.assertFalse(report.ok)
			self.assertIn("Duplicate Steam ID: 76561198000000001", report.errors)

	def test_log_blockers_and_sensitive_values_are_rejected(self) -> None:
		report = evidence.ValidationReport()
		evidence._validate_log_text(
			"SCRIPT ERROR\nseat_credential=secret\nnonce=0011\n",
			report,
			"fixture.log",
		)
		self.assertEqual(3, len(report.errors))

	def test_duration_must_be_finite_and_chronological(self) -> None:
		report = evidence.ValidationReport()
		evidence._validate_duration(
			{
				"started_at_utc": "2026-08-01T10:00:00Z",
				"ended_at_utc": "2026-08-01T09:59:00Z",
			},
			report,
			"case",
		)
		self.assertIn("case ended before it started", report.errors)

	def test_lan_runtime_boolean_cannot_replace_artifacts(self) -> None:
		with tempfile.TemporaryDirectory() as temp:
			report = evidence.ValidationReport()
			evidence._validate_target_runtime_evidence(
				{"target_runtime_evidence": True, "release_audit": True},
				"linux",
				{"linux-host", "linux-client"},
				Path(temp) / "manifest.json",
				".",
				"release-fixture",
				report,
				"real_lan_runs[linux]",
			)
			self.assertIn("real_lan_runs[linux].target_runtime_evidence path is missing", report.errors)


if __name__ == "__main__":
	unittest.main()
