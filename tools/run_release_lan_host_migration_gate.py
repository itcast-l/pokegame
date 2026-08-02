#!/usr/bin/env python3

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import shutil
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Sequence, TextIO

import release_pack_harness
import verify_release_target_runtime as release_runtime


ROOT = Path(__file__).resolve().parent.parent
PLATFORM_NAMES = {"windows": "Windows", "linux": "Linux", "macos": "macOS"}
LOG_BLOCKERS = (
	"ERROR:",
	"Unicode parsing error",
	"SCRIPT ERROR",
	"Failed loading resource",
	"Failed to load resource",
	"Unable to export Pandora data",
)


class GateError(RuntimeError):
	pass


@dataclass
class PeerProcess:
	role: str
	seat: int
	process: subprocess.Popen[str]
	log_path: Path
	log_handle: TextIO
	workspace: release_pack_harness.HarnessWorkspace


def _utc_now() -> str:
	return dt.datetime.now(dt.timezone.utc).isoformat()


def _load_json(path: Path, label: str) -> dict[str, Any]:
	try:
		value = json.loads(path.read_text(encoding="utf-8"))
	except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
		raise GateError(f"{label} is not readable JSON: {path}: {error}") from error
	if not isinstance(value, dict):
		raise GateError(f"{label} must contain a JSON object: {path}")
	return value


def validate_target_runtime_evidence(
	path: Path,
	release_pack: release_pack_harness.ReleasePack,
) -> dict[str, Any]:
	path = path.resolve()
	evidence = _load_json(path, "target runtime evidence")
	for key, expected in (
		("schema_version", 1),
		("status", "passed"),
		("build_id", release_pack.build_id),
		("network_protocol", release_pack.network_protocol),
		("platform", release_pack.platform),
	):
		if evidence.get(key) != expected:
			raise GateError(f"target runtime evidence {key} does not match the release candidate")
	release_audit = evidence.get("release_audit")
	if not isinstance(release_audit, dict) or release_audit.get("sha256") != release_pack.report_sha256:
		raise GateError("target runtime evidence release audit hash does not match")
	artifacts = evidence.get("artifacts")
	if not isinstance(artifacts, dict):
		raise GateError("target runtime evidence artifacts are missing")
	for kind in ("executable", "pck"):
		artifact = artifacts.get(kind)
		expected_hash = release_pack.artifacts[kind]["actual_sha256"]
		if not isinstance(artifact, dict) or artifact.get("actual_sha256") != expected_hash:
			raise GateError(f"target runtime evidence {kind} hash does not match")
	runtime_smoke = evidence.get("runtime_smoke")
	if not isinstance(runtime_smoke, dict):
		raise GateError("target runtime evidence runtime_smoke is missing")
	if runtime_smoke.get("exit_code") != 0:
		raise GateError("target runtime evidence executable exit code is not zero")
	reported_blockers = runtime_smoke.get("blocker_counts")
	if not isinstance(reported_blockers, dict) or any(
		int(reported_blockers.get(marker, -1)) != 0 for marker in LOG_BLOCKERS
	):
		raise GateError("target runtime evidence reports blocking log entries")
	log_relative = runtime_smoke.get("log_path")
	if not isinstance(log_relative, str) or not log_relative:
		raise GateError("target runtime evidence raw log path is missing")
	log_path = (path.parent / log_relative).resolve()
	try:
		log_path.relative_to(path.parent.resolve())
	except ValueError as error:
		raise GateError("target runtime evidence raw log escapes its evidence directory") from error
	if not log_path.is_file() or release_runtime._sha256_file(log_path) != runtime_smoke.get("log_sha256"):
		raise GateError("target runtime evidence raw log hash does not match")
	log_lines = log_path.read_text(encoding="utf-8").splitlines()
	log_text = "\n".join(log_lines)
	if any(marker in log_text for marker in LOG_BLOCKERS):
		raise GateError("target runtime evidence raw log contains blocking entries")
	for marker_key in ("build_marker", "success_marker"):
		marker = runtime_smoke.get(marker_key)
		if not isinstance(marker, str) or marker not in log_lines:
			raise GateError(f"target runtime evidence raw log is missing {marker_key}")
	device_id = str(evidence.get("device_id", "")).strip()
	if not device_id:
		raise GateError("target runtime evidence device_id is missing")
	return {
		"path": str(path),
		"sha256": release_runtime._sha256_file(path),
		"device_id": device_id,
		"runtime_log_path": str(log_path),
		"runtime_log_sha256": runtime_smoke["log_sha256"],
	}


def _read_log(path: Path) -> str:
	try:
		return path.read_text(encoding="utf-8", errors="replace")
	except OSError:
		return ""


def _wait_for_marker(path: Path, marker: str, timeout_seconds: float) -> None:
	deadline = time.monotonic() + timeout_seconds
	while time.monotonic() < deadline:
		if marker in _read_log(path):
			return
		time.sleep(0.25)
	raise GateError(f"Timed out waiting for marker {marker!r} in {path.name}")


def _wait_for_any_marker(paths: Sequence[Path], marker: str, timeout_seconds: float) -> None:
	deadline = time.monotonic() + timeout_seconds
	while time.monotonic() < deadline:
		if any(marker in _read_log(path) for path in paths):
			return
		time.sleep(0.25)
	raise GateError(f"Timed out waiting for marker {marker!r} in client logs")


def _parse_result(path: Path, player_count: int) -> dict[str, str]:
	if not path.is_file() or path.stat().st_size == 0:
		raise GateError(f"LAN result is missing: {path}")
	result: dict[str, str] = {}
	for line in path.read_text(encoding="utf-8").splitlines():
		if "=" not in line:
			raise GateError(f"Malformed LAN result line in {path}: {line}")
		key, value = line.split("=", 1)
		if key in result:
			raise GateError(f"Duplicate LAN result field in {path}: {key}")
		result[key] = value
	winner = result.get("winner", "")
	if not winner.isdigit() or not 0 <= int(winner) < player_count:
		raise GateError(f"LAN result winner is invalid: {path}")
	if result.get("private_leak") != "false":
		raise GateError(f"LAN result detected private state leakage: {path}")
	if re.fullmatch(r"[0-9a-f]{64}", result.get("fingerprint", "")) is None:
		raise GateError(f"LAN result fingerprint is invalid: {path}")
	for field in ("table_count", "snapshot_id"):
		if not result.get(field, "").isdigit():
			raise GateError(f"LAN result {field} is invalid: {path}")
	for field in ("score", "deck", "hand", "item_hand"):
		for index in range(player_count):
			if not result.get(f"{field}_{index}", "").isdigit():
				raise GateError(f"LAN result {field}_{index} is invalid: {path}")
	return result


def assert_migrated_results(result_dir: Path, player_count: int = 3) -> list[str]:
	assertions: list[str] = []
	public_fields = ["winner", "fingerprint", "snapshot_id", "table_count"]
	public_fields.extend(
		f"{field}_{index}"
		for field in ("score", "deck", "hand", "item_hand")
		for index in range(player_count)
	)
	for seat in range(player_count):
		_parse_result(result_dir / f"expected_{seat}.result", player_count)
		assertions.append(f"expected_{seat}_valid")
	for seat in range(1, player_count):
		expected = _parse_result(result_dir / f"expected_{seat}.result", player_count)
		actual = _parse_result(result_dir / f"client_{seat}.result", player_count)
		for field in public_fields:
			if expected[field] != actual[field]:
				raise GateError(f"Migrated public state mismatch for seat={seat} field={field}")
		assertions.append(f"client_{seat}_matches_authority")
	return assertions


def _start_peer(
	release_pack: release_pack_harness.ReleasePack,
	godot: Path,
	script: Path,
	workspace_root: Path,
	log_path: Path,
	result_dir: Path,
	role: str,
	seat: int,
) -> PeerProcess:
	workspace = release_pack_harness.prepare_release_pack_harness(
		release_pack,
		workspace_root,
	)
	arguments = [
		f"--role={role}",
		f"--seat={seat}",
		"--players=3",
		"--item-mode=1",
		f"--result-dir={result_dir}",
	]
	if role == "client":
		arguments.extend(("--allow-client-result-without-host", "--require-all-migration-peers"))
	command = release_pack_harness.build_harness_command(
		godot,
		release_pack,
		workspace,
		script,
		arguments,
	)
	extra_environment = {
		"CODEX_LAN_MATCH_SEED": "20260714",
		"CODEX_LAN_MIGRATION_ADDRESS": "127.0.0.1",
		"CODEX_LAN_SKIP_MIGRATION_PORT_PROBE": "1",
		"CODEX_TEST_STABLE_USER_ID": f"release-pack-migration-seat-{seat}",
	}
	if role == "host":
		extra_environment["CODEX_LAN_MIGRATION_FIXTURE"] = "1"
	environment = release_pack_harness.build_harness_environment(
		workspace,
		extra=extra_environment,
	)
	log_handle = log_path.open("w", encoding="utf-8")
	process = subprocess.Popen(
		command,
		cwd=workspace.root,
		env=environment,
		text=True,
		stdout=log_handle,
		stderr=subprocess.STDOUT,
	)
	return PeerProcess(role, seat, process, log_path, log_handle, workspace)


def _require_clean_log(peer: PeerProcess, expected_build_marker: str) -> list[str]:
	text = _read_log(peer.log_path)
	lines = text.splitlines()
	if expected_build_marker not in lines:
		raise GateError(f"{peer.role} seat={peer.seat} did not emit the audited Release build marker")
	for blocker in LOG_BLOCKERS:
		if blocker in text:
			raise GateError(f"{peer.role} seat={peer.seat} log contains blocker {blocker!r}")
	markers = [expected_build_marker]
	if peer.role == "client":
		for marker in (
			"FULL_MATCH_CLIENT_WINNER",
			"FULL_MATCH_CLIENT_STATE 10",
			"FULL_MATCH_CLIENT_STATE 7",
			"MIGRATION_UI_STATE visible=true",
		):
			if marker not in text:
				raise GateError(f"client seat={peer.seat} log is missing marker {marker!r}")
			markers.append(marker)
	return markers


def _resolved_peer_seat(peer: PeerProcess) -> int:
	if peer.role == "host":
		return 0
	match = re.search(r"(?m)^FULL_MATCH_CLIENT_LOCAL_SEAT ([0-9]+)$", _read_log(peer.log_path))
	return int(match.group(1)) if match else -1


def run_gate(
	release_audit_path: Path,
	target_runtime_evidence_path: Path,
	output_dir: Path,
	*,
	godot_path: Path | None = None,
	script_path: Path = ROOT / "tests/LanFullMatchPeer.gd",
	readiness_timeout_seconds: int = 90,
	completion_timeout_seconds: int = 180,
) -> dict[str, Any]:
	output_dir = output_dir.resolve()
	if output_dir.exists() and any(output_dir.iterdir()):
		raise GateError(f"Output directory must be empty: {output_dir}")
	output_dir.mkdir(parents=True, exist_ok=True)
	result_dir = output_dir / "results"
	result_dir.mkdir()
	evidence_path = output_dir / "release-pack-lan-gate.json"
	evidence: dict[str, Any] = {
		"schema_version": 1,
		"status": "failed",
		"scope": "single_device_loopback",
		"scenario": "lan_3p_items_source_host_hard_kill",
		"started_at_utc": _utc_now(),
	}
	peers: list[PeerProcess] = []
	temporary_runtime: tempfile.TemporaryDirectory[str] | None = None
	try:
		if readiness_timeout_seconds <= 0 or completion_timeout_seconds <= 0:
			raise GateError("Gate timeouts must be positive")
		release_pack = release_pack_harness.load_release_pack(release_audit_path)
		target_runtime = validate_target_runtime_evidence(
			target_runtime_evidence_path,
			release_pack,
		)
		godot = release_pack_harness.resolve_godot_binary(godot_path)
		script_path = script_path.resolve()
		if not script_path.is_file():
			raise GateError(f"LAN harness is missing: {script_path}")
		evidence.update(
			{
				"build_id": release_pack.build_id,
				"network_protocol": release_pack.network_protocol,
				"platform": release_pack.platform,
				"device_id": target_runtime["device_id"],
				"release_audit": {
					"path": str(release_pack.report_path),
					"sha256": release_pack.report_sha256,
				},
				"target_runtime_evidence": target_runtime,
				"artifacts": release_pack.artifacts,
				"harness": {
					"path": str(script_path),
					"sha256": release_runtime._sha256_file(script_path),
				},
			}
		)
		temporary_runtime = tempfile.TemporaryDirectory(prefix="deckland-release-lan-")
		runtime_root = Path(temporary_runtime.name)
		host = _start_peer(
			release_pack,
			godot,
			script_path,
			runtime_root / "host",
			output_dir / "host.log",
			result_dir,
			"host",
			0,
		)
		peers.append(host)
		_wait_for_marker(host.log_path, "FULL_MATCH_HOST_STATE 4", readiness_timeout_seconds)
		for seat in (1, 2):
			peers.append(
				_start_peer(
					release_pack,
					godot,
					script_path,
					runtime_root / f"client_{seat}",
					output_dir / f"client_{seat}.log",
					result_dir,
					"client",
					seat,
				)
			)
		client_logs = [peer.log_path for peer in peers if peer.role == "client"]
		_wait_for_any_marker(client_logs, "STANDBY_ACKED", readiness_timeout_seconds)
		for marker in (
			"MIGRATION_PLAN_READY phase=InGame",
			"FULL_MATCH_HOST_START_MATCH",
			"FULL_MATCH_HOST_STATE 7",
		):
			_wait_for_marker(host.log_path, marker, readiness_timeout_seconds)
		host.process.kill()
		host.process.wait(timeout=15)
		evidence["source_host_hard_killed"] = True
		for peer in peers:
			if peer.role == "host":
				continue
			try:
				peer.process.wait(timeout=completion_timeout_seconds)
			except subprocess.TimeoutExpired as error:
				raise GateError(f"client seat={peer.seat} did not finish after migration") from error
			if peer.process.returncode != 0:
				raise GateError(
					f"client seat={peer.seat} exited with code {peer.process.returncode}"
				)
		for peer in peers:
			peer.log_handle.flush()
		expected_build_marker = (
			f"RELEASE_BUILD_INFO build_id={release_pack.build_id} "
			f"protocol={release_pack.network_protocol} platform={PLATFORM_NAMES[release_pack.platform]}"
		)
		assertions: list[str] = []
		for peer in peers:
			assertions.extend(_require_clean_log(peer, expected_build_marker))
		assertions.extend(assert_migrated_results(result_dir))
		evidence["assertions"] = assertions
		evidence["status"] = "passed"
	except (
		GateError,
		OSError,
		release_pack_harness.HarnessError,
		subprocess.SubprocessError,
	) as error:
		evidence["error"] = str(error)
	finally:
		for peer in peers:
			if peer.process.poll() is None:
				peer.process.kill()
				try:
					peer.process.wait(timeout=10)
				except subprocess.TimeoutExpired:
					pass
			peer.log_handle.close()
		peer_evidence: list[dict[str, Any]] = []
		for peer in peers:
			item: dict[str, Any] = {
				"role": peer.role,
				"process_slot": peer.seat,
				"resolved_seat": _resolved_peer_seat(peer),
				"exit_code": peer.process.returncode,
				"log_path": peer.log_path.name,
				"mapping_modes": peer.workspace.mapping_modes,
			}
			if peer.log_path.is_file():
				item["log_sha256"] = release_runtime._sha256_file(peer.log_path)
			peer_evidence.append(item)
		evidence["peers"] = peer_evidence
		evidence["completed_at_utc"] = _utc_now()
		evidence_path.write_text(
			json.dumps(evidence, ensure_ascii=False, indent=2) + "\n",
			encoding="utf-8",
		)
		if temporary_runtime is not None:
			temporary_runtime.cleanup()
	return evidence


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(
		description="Run the three-player hard-kill LAN migration gate against an audited Release PCK."
	)
	parser.add_argument("release_audit", type=Path)
	parser.add_argument("target_runtime_evidence", type=Path)
	parser.add_argument("--output-dir", type=Path, required=True)
	parser.add_argument("--godot", type=Path)
	parser.add_argument("--script", type=Path, default=ROOT / "tests/LanFullMatchPeer.gd")
	parser.add_argument("--readiness-timeout", type=int, default=90)
	parser.add_argument("--completion-timeout", type=int, default=180)
	return parser


def main(argv: Sequence[str] | None = None) -> int:
	args = build_parser().parse_args(argv)
	try:
		evidence = run_gate(
			args.release_audit,
			args.target_runtime_evidence,
			args.output_dir,
			godot_path=args.godot,
			script_path=args.script,
			readiness_timeout_seconds=args.readiness_timeout,
			completion_timeout_seconds=args.completion_timeout,
		)
	except GateError as error:
		print(f"RELEASE_LAN_HOST_MIGRATION_GATE_FAILED {error}", file=sys.stderr)
		return 1
	if evidence["status"] != "passed":
		print(
			f"RELEASE_LAN_HOST_MIGRATION_GATE_FAILED {evidence.get('error', 'unknown error')}",
			file=sys.stderr,
		)
		return 1
	print(
		f"RELEASE_LAN_HOST_MIGRATION_GATE_OK build_id={evidence['build_id']} "
		f"platform={evidence['platform']} evidence={args.output_dir.resolve() / 'release-pack-lan-gate.json'}"
	)
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
