#!/usr/bin/env python3

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import platform as platform_module
import re
import subprocess
import sys
import time
from pathlib import Path
from typing import Any, Callable, Sequence


SUPPORTED_PLATFORMS = {"windows", "linux", "macos"}
GODOT_PLATFORM_NAMES = {"windows": "Windows", "linux": "Linux", "macos": "macOS"}
RELEASE_EXECUTABLE_SMOKE_MARKER = "RELEASE_EXECUTABLE_SMOKE_OK"
LOG_BLOCKERS = (
	"ERROR:",
	"Unicode parsing error",
	"SCRIPT ERROR",
	"Failed loading resource",
	"Failed to load resource",
	"Unable to export Pandora data",
)
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")


class VerificationError(RuntimeError):
	pass


def _utc_now() -> str:
	return dt.datetime.now(dt.timezone.utc).isoformat()


def _sha256_file(path: Path) -> str:
	digest = hashlib.sha256()
	with path.open("rb") as handle:
		for chunk in iter(lambda: handle.read(1024 * 1024), b""):
			digest.update(chunk)
	return digest.hexdigest()


def _load_json(path: Path, label: str) -> dict[str, Any]:
	try:
		value = json.loads(path.read_text(encoding="utf-8"))
	except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
		raise VerificationError(f"{label} is not readable JSON: {path}: {error}") from error
	if not isinstance(value, dict):
		raise VerificationError(f"{label} must contain a JSON object: {path}")
	return value


def _write_json(path: Path, value: dict[str, Any]) -> None:
	path.parent.mkdir(parents=True, exist_ok=True)
	path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _host_platform() -> str:
	if os.name == "nt":
		return "windows"
	if sys.platform == "darwin":
		return "macos"
	if sys.platform.startswith("linux"):
		return "linux"
	return "unknown"


def _resolve_within_report(report_path: Path, relative_path: str, label: str) -> Path:
	root = report_path.parent.resolve()
	candidate = (root / relative_path).resolve()
	try:
		candidate.relative_to(root)
	except ValueError as error:
		raise VerificationError(f"{label} escapes the release report directory: {relative_path}") from error
	return candidate


def _resolve_artifact(report_path: Path, artifact: dict[str, Any], kind: str) -> tuple[Path, str]:
	relative_key = f"{kind}_relative_path"
	raw_relative = artifact.get(relative_key)
	if isinstance(raw_relative, str) and raw_relative.strip():
		path = _resolve_within_report(report_path, raw_relative, f"artifact.{relative_key}")
		return path, raw_relative

	release_directory = artifact.get("release_directory")
	original_path = artifact.get(f"{kind}_path")
	if not isinstance(release_directory, str) or not isinstance(original_path, str):
		raise VerificationError(f"artifact.{relative_key} is missing")
	try:
		inside_package = Path(original_path).relative_to(Path(release_directory))
	except ValueError as error:
		raise VerificationError(f"artifact.{kind}_path is outside artifact.release_directory") from error
	release_relative = artifact.get("release_directory_relative_path", "package")
	if not isinstance(release_relative, str) or not release_relative.strip():
		raise VerificationError("artifact.release_directory_relative_path is invalid")
	portable_relative = (Path(release_relative) / inside_package).as_posix()
	path = _resolve_within_report(report_path, portable_relative, f"artifact.{relative_key}")
	return path, portable_relative


def _require_release_audit(report: dict[str, Any], host_platform: str) -> tuple[str, int, dict[str, Any]]:
	if report.get("schema_version") != 1:
		raise VerificationError("release audit schema_version must be 1")
	report_platform = str(report.get("platform", ""))
	if report_platform not in SUPPORTED_PLATFORMS:
		raise VerificationError(f"release audit platform is unsupported: {report_platform}")
	if report_platform != host_platform:
		raise VerificationError(
			f"release candidate targets {report_platform}, but this host is {host_platform}"
		)
	if report.get("status") not in {"passed", "target_runtime_pending"}:
		raise VerificationError("release audit status is not eligible for target runtime verification")
	build_id = str(report.get("build_id", "")).strip()
	if not build_id:
		raise VerificationError("release audit build_id is missing")
	network_protocol = report.get("network_protocol")
	if not isinstance(network_protocol, int) or network_protocol <= 0:
		raise VerificationError("release audit network_protocol must be a positive integer")
	if report.get("source_integrity", {}).get("unchanged") is not True:
		raise VerificationError("release audit source integrity did not pass")
	if report.get("package_audit", {}).get("content", {}).get("ok") is not True:
		raise VerificationError("release package content audit did not pass")
	if report.get("runtime_smoke", {}).get("pack_harness", {}).get("ok") is not True:
		raise VerificationError("release pack harness did not pass")
	log_counts = report.get("log_error_counts")
	if not isinstance(log_counts, dict) or any(int(log_counts.get(marker, -1)) != 0 for marker in LOG_BLOCKERS):
		raise VerificationError("release audit contains blocking log counts")
	artifact = report.get("artifact")
	if not isinstance(artifact, dict):
		raise VerificationError("release audit artifact must be an object")
	return build_id, network_protocol, artifact


def _artifact_evidence(path: Path, relative_path: str, expected_sha256: Any, label: str) -> dict[str, Any]:
	expected = str(expected_sha256).lower()
	if SHA256_PATTERN.fullmatch(expected) is None:
		raise VerificationError(f"{label} expected SHA-256 is invalid")
	if not path.is_file():
		raise VerificationError(f"{label} is missing: {path}")
	actual = _sha256_file(path)
	if actual != expected:
		raise VerificationError(f"{label} SHA-256 mismatch: expected {expected}, got {actual}")
	return {
		"relative_path": relative_path,
		"size_bytes": path.stat().st_size,
		"expected_sha256": expected,
		"actual_sha256": actual,
	}


def _scan_log(text: str) -> dict[str, int]:
	return {marker: text.count(marker) for marker in LOG_BLOCKERS}


def verify_target_runtime(
	report_path: Path,
	output_path: Path,
	log_path: Path,
	device_id: str,
	*,
	timeout_seconds: int = 180,
	host_platform: str | None = None,
	runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
) -> dict[str, Any]:
	report_path = report_path.resolve()
	output_path = output_path.resolve()
	log_path = log_path.resolve()
	host_platform = host_platform or _host_platform()
	evidence: dict[str, Any] = {
		"schema_version": 1,
		"status": "failed",
		"started_at_utc": _utc_now(),
		"device_id": device_id.strip(),
		"host": {
			"platform": host_platform,
			"system": platform_module.system(),
			"release": platform_module.release(),
			"machine": platform_module.machine(),
			"hostname": platform_module.node(),
		},
	}
	try:
		if not device_id.strip():
			raise VerificationError("device_id is missing")
		if timeout_seconds <= 0:
			raise VerificationError("timeout_seconds must be positive")
		report = _load_json(report_path, "release audit")
		build_id, network_protocol, artifact = _require_release_audit(report, host_platform)
		evidence["build_id"] = build_id
		evidence["network_protocol"] = network_protocol
		evidence["platform"] = report["platform"]
		evidence["preset"] = report.get("preset")
		evidence["release_audit"] = {
			"file_name": report_path.name,
			"sha256": _sha256_file(report_path),
			"status": report.get("status"),
		}

		executable, executable_relative = _resolve_artifact(report_path, artifact, "executable")
		pck, pck_relative = _resolve_artifact(report_path, artifact, "pck")
		evidence["artifacts"] = {
			"executable": _artifact_evidence(
				executable, executable_relative, artifact.get("executable_sha256"), "release executable"
			),
			"pck": _artifact_evidence(pck, pck_relative, artifact.get("pck_sha256"), "release PCK"),
		}
		if host_platform != "windows" and not os.access(executable, os.X_OK):
			raise VerificationError(f"release executable is not executable: {executable}")

		command = [
			str(executable),
			"--headless",
			"--audio-driver",
			"Dummy",
			"--",
			"--release-package-smoke",
		]
		environment = os.environ.copy()
		environment["DECKLAND_TEST_MUTE_AUDIO"] = "1"
		runtime_started = _utc_now()
		start = time.monotonic()
		try:
			result = runner(
				command,
				cwd=executable.parent,
				env=environment,
				text=True,
				stdout=subprocess.PIPE,
				stderr=subprocess.STDOUT,
				timeout=timeout_seconds,
				check=False,
			)
		except subprocess.TimeoutExpired as error:
			partial = error.stdout or ""
			if isinstance(partial, bytes):
				partial = partial.decode("utf-8", errors="replace")
			log_path.parent.mkdir(parents=True, exist_ok=True)
			log_path.write_text(partial, encoding="utf-8")
			raise VerificationError(f"target executable timed out after {timeout_seconds} seconds") from error
		duration_seconds = time.monotonic() - start
		log_path.parent.mkdir(parents=True, exist_ok=True)
		log_path.write_text(result.stdout, encoding="utf-8")
		blocker_counts = _scan_log(result.stdout)
		expected_build_marker = (
			f"RELEASE_BUILD_INFO build_id={build_id} protocol={network_protocol} "
			f"platform={GODOT_PLATFORM_NAMES[host_platform]}"
		)
		expected_marker = (
			f"{RELEASE_EXECUTABLE_SMOKE_MARKER} platform={GODOT_PLATFORM_NAMES[host_platform]}"
		)
		evidence["runtime_smoke"] = {
			"started_at_utc": runtime_started,
			"ended_at_utc": _utc_now(),
			"duration_seconds": round(duration_seconds, 3),
			"exit_code": result.returncode,
			"arguments": command[1:],
			"build_marker": expected_build_marker,
			"success_marker": expected_marker,
			"log_path": os.path.relpath(log_path, output_path.parent),
			"log_sha256": _sha256_file(log_path),
			"blocker_counts": blocker_counts,
		}
		if result.returncode != 0:
			raise VerificationError(f"target executable exited with code {result.returncode}")
		if any(blocker_counts.values()):
			raise VerificationError(f"target executable log contains blockers: {blocker_counts}")
		if expected_build_marker not in result.stdout.splitlines():
			raise VerificationError(f"target executable did not emit exact build marker: {expected_build_marker}")
		if expected_marker not in result.stdout.splitlines():
			raise VerificationError(f"target executable did not emit exact marker: {expected_marker}")
		evidence["status"] = "passed"
	except (OSError, VerificationError, ValueError) as error:
		evidence["error"] = str(error)
	evidence["completed_at_utc"] = _utc_now()
	return evidence


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(
		description="Verify a Deckland release executable on its target operating system."
	)
	parser.add_argument("release_audit", type=Path)
	parser.add_argument("--device-id", required=True, help="Stable physical-device label used by the release evidence manifest")
	parser.add_argument("--output", type=Path, required=True, help="Structured target runtime evidence JSON")
	parser.add_argument("--log", type=Path, help="Raw executable log; defaults beside --output")
	parser.add_argument("--timeout", type=int, default=180)
	return parser


def main(argv: Sequence[str] | None = None) -> int:
	args = build_parser().parse_args(argv)
	report_path = args.release_audit.resolve()
	output_path = args.output.resolve()
	log_path = args.log.resolve() if args.log else output_path.with_suffix(".log")
	if len({report_path, output_path, log_path}) != 3:
		print("RELEASE_TARGET_RUNTIME_FAILED report, output, and log paths must be distinct", file=sys.stderr)
		return 1
	evidence = verify_target_runtime(
		report_path,
		output_path,
		log_path,
		args.device_id,
		timeout_seconds=args.timeout,
	)
	_write_json(output_path, evidence)
	if evidence["status"] == "passed":
		print(
			f"RELEASE_TARGET_RUNTIME_OK platform={evidence['platform']} "
			f"build_id={evidence['build_id']} evidence={output_path}"
		)
		return 0
	print(f"RELEASE_TARGET_RUNTIME_FAILED {evidence.get('error', 'unknown error')}", file=sys.stderr)
	return 1


if __name__ == "__main__":
	raise SystemExit(main())
