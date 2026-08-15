#!/usr/bin/env python3

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any


REQUIRED_STEAM_CASES = {
	"active_owner_transfer",
	"source_crash",
	"automatic_owner_second_transfer",
	"owner_timeout",
	"candidate_exit",
}
REQUIRED_LAN_PLATFORMS = {"windows", "linux", "macos"}
REQUIRED_NETWORK_CASES = {
	"latency",
	"packet_loss",
	"duplicate_packet",
	"short_disconnect",
	"host_candidate_simultaneous_loss",
}
EXPECTED_NETWORK_PROTOCOL = 12
RELEASE_EXECUTABLE_SMOKE_MARKER = "RELEASE_EXECUTABLE_SMOKE_OK"
GODOT_PLATFORM_NAMES = {"windows": "Windows", "linux": "Linux", "macos": "macOS"}
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
COMMON_ASSERTIONS = {
	"single_authority",
	"authority_epoch_monotonic",
	"no_stale_restore",
	"no_permanent_input_lock",
	"no_sensitive_log_data",
	"finite_outcome",
}
STEAM_CASE_ASSERTIONS = {
	"active_owner_transfer": {"owner_transfer_observed", "checkpoint_restored", "survivors_same_winner"},
	"source_crash": {"source_force_killed", "automatic_owner_converged", "checkpoint_restored", "survivors_same_winner"},
	"automatic_owner_second_transfer": {
		"source_force_killed",
		"non_candidate_owner_observed",
		"second_transfer_to_signed_candidate",
		"survivors_same_winner",
	},
	"owner_timeout": {"steam_owner_timeout_observed", "safe_end_without_new_host"},
	"candidate_exit": {"candidate_force_killed", "old_plan_revoked", "replacement_or_safe_end"},
}
LOG_BLOCKERS = ("SCRIPT ERROR", "Unhandled exception", "kind=HARNESS_FAIL")
RELEASE_LOG_BLOCKERS = (
	"ERROR:",
	"Unicode parsing error",
	"SCRIPT ERROR",
	"Failed loading resource",
	"Failed to load resource",
	"Unable to export Pandora data",
)
SENSITIVE_PATTERNS = (
	re.compile(r"(?i)(room_?password|seat_?credential|private_?key|session_?key)\s*[:=]"),
	re.compile(r"(?i)\bnonce\s*="),
)


@dataclass
class ValidationReport:
	errors: list[str] = field(default_factory=list)
	warnings: list[str] = field(default_factory=list)

	@property
	def ok(self) -> bool:
		return not self.errors

	def require(self, condition: bool, message: str) -> None:
		if not condition:
			self.errors.append(message)


def _load_json(path: Path, report: ValidationReport, label: str) -> dict[str, Any] | None:
	try:
		value = json.loads(path.read_text(encoding="utf-8"))
	except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
		report.errors.append(f"{label} is not readable JSON: {path}: {error}")
		return None
	if not isinstance(value, dict):
		report.errors.append(f"{label} must contain a JSON object: {path}")
		return None
	return value


def _sha256_file(path: Path, report: ValidationReport, label: str) -> str:
	digest = hashlib.sha256()
	try:
		with path.open("rb") as handle:
			for chunk in iter(lambda: handle.read(1024 * 1024), b""):
				digest.update(chunk)
	except OSError as error:
		report.errors.append(f"{label} is not readable: {path}: {error}")
		return ""
	return digest.hexdigest()


def _resolve_path(manifest_path: Path, root_value: Any, raw_path: Any) -> Path:
	root = manifest_path.parent
	if isinstance(root_value, str) and root_value.strip():
		configured_root = Path(root_value)
		root = configured_root if configured_root.is_absolute() else manifest_path.parent / configured_root
	path = Path(str(raw_path))
	return path if path.is_absolute() else root / path


def _read_artifact(
	manifest_path: Path,
	root_value: Any,
	raw_path: Any,
	report: ValidationReport,
	label: str,
) -> tuple[Path | None, str]:
	if not isinstance(raw_path, str) or not raw_path.strip():
		report.errors.append(f"{label} path is missing")
		return None, ""
	path = _resolve_path(manifest_path, root_value, raw_path)
	try:
		return path, path.read_text(encoding="utf-8")
	except (OSError, UnicodeDecodeError) as error:
		report.errors.append(f"{label} is not readable UTF-8: {path}: {error}")
		return path, ""


def _validate_timestamp(value: Any, report: ValidationReport, label: str) -> datetime | None:
	if not isinstance(value, str) or not value.strip():
		report.errors.append(f"{label} is missing")
		return None
	try:
		parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
	except ValueError:
		report.errors.append(f"{label} is not ISO-8601: {value}")
		return None
	if parsed.tzinfo is None:
		report.errors.append(f"{label} must include a timezone: {value}")
		return None
	return parsed


def _validate_duration(entry: dict[str, Any], report: ValidationReport, label: str) -> None:
	started = _validate_timestamp(entry.get("started_at_utc"), report, f"{label}.started_at_utc")
	ended = _validate_timestamp(entry.get("ended_at_utc"), report, f"{label}.ended_at_utc")
	if started is not None and ended is not None:
		report.require(ended >= started, f"{label} ended before it started")
		report.require((ended - started).total_seconds() <= 1800, f"{label} exceeded the 30-minute finite-outcome limit")


def _validate_log_text(text: str, report: ValidationReport, label: str) -> None:
	for blocker in LOG_BLOCKERS:
		if blocker in text:
			report.errors.append(f"{label} contains blocker: {blocker}")
	for pattern in SENSITIVE_PATTERNS:
		if pattern.search(text):
			report.errors.append(f"{label} contains sensitive material matching {pattern.pattern}")


def _validate_assertions(
	entry: dict[str, Any],
	required: set[str],
	report: ValidationReport,
	label: str,
) -> None:
	assertions = entry.get("assertions")
	if not isinstance(assertions, dict):
		report.errors.append(f"{label}.assertions must be an object")
		return
	for key in sorted(required):
		report.require(assertions.get(key) is True, f"{label}.assertions.{key} must be true")


def _validate_timeline(entry: dict[str, Any], report: ValidationReport, label: str) -> list[dict[str, Any]]:
	timeline = entry.get("timeline")
	if not isinstance(timeline, list) or len(timeline) < 3:
		report.errors.append(f"{label}.timeline must contain at least three events")
		return []
	parsed: list[tuple[datetime, dict[str, Any]]] = []
	for index, raw_event in enumerate(timeline):
		if not isinstance(raw_event, dict):
			report.errors.append(f"{label}.timeline[{index}] must be an object")
			continue
		when = _validate_timestamp(raw_event.get("at_utc"), report, f"{label}.timeline[{index}].at_utc")
		report.require(bool(str(raw_event.get("event", "")).strip()), f"{label}.timeline[{index}].event is missing")
		if when is not None:
			parsed.append((when, raw_event))
	for index in range(1, len(parsed)):
		report.require(parsed[index][0] >= parsed[index - 1][0], f"{label}.timeline is not chronological")
	return [event for _, event in parsed]


def _collect_cases(raw: Any, required_ids: set[str], report: ValidationReport, label: str) -> dict[str, dict[str, Any]]:
	if not isinstance(raw, list):
		report.errors.append(f"{label} must be an array")
		return {}
	collected: dict[str, dict[str, Any]] = {}
	for index, entry in enumerate(raw):
		if not isinstance(entry, dict):
			report.errors.append(f"{label}[{index}] must be an object")
			continue
		case_id = str(entry.get("id", ""))
		if case_id in collected:
			report.errors.append(f"{label} contains duplicate id: {case_id}")
			continue
		collected[case_id] = entry
	for missing in sorted(required_ids - collected.keys()):
		report.errors.append(f"{label} is missing required case: {missing}")
	return collected


def _validate_accounts(manifest: dict[str, Any], report: ValidationReport) -> dict[str, dict[str, Any]]:
	raw_accounts = manifest.get("accounts")
	if not isinstance(raw_accounts, list):
		report.errors.append("accounts must be an array")
		return {}
	accounts: dict[str, dict[str, Any]] = {}
	steam_ids: set[str] = set()
	for index, raw_account in enumerate(raw_accounts):
		if not isinstance(raw_account, dict):
			report.errors.append(f"accounts[{index}] must be an object")
			continue
		label = str(raw_account.get("label", "")).strip()
		steam_id = str(raw_account.get("steam_id", "")).strip()
		platform = str(raw_account.get("platform", "")).lower()
		device_id = str(raw_account.get("device_id", "")).strip()
		report.require(bool(label), f"accounts[{index}].label is missing")
		report.require(re.fullmatch(r"[0-9]{10,20}", steam_id) is not None, f"accounts[{index}].steam_id is invalid")
		report.require(platform in REQUIRED_LAN_PLATFORMS, f"accounts[{index}].platform is invalid")
		report.require(bool(device_id), f"accounts[{index}].device_id is missing")
		if label in accounts:
			report.errors.append(f"Duplicate account label: {label}")
		if steam_id in steam_ids:
			report.errors.append(f"Duplicate Steam ID: {steam_id}")
		accounts[label] = raw_account
		steam_ids.add(steam_id)
	return accounts


def _validate_steam_case(
	case_id: str,
	entry: dict[str, Any],
	accounts: dict[str, dict[str, Any]],
	manifest_path: Path,
	root_value: Any,
	build_id: str,
	report: ValidationReport,
	allow_pending: bool,
) -> None:
	label = f"steam_cases[{case_id}]"
	status = entry.get("status")
	if status != "passed":
		if allow_pending and status == "pending":
			report.warnings.append(f"{label} is pending")
			return
		report.errors.append(f"{label}.status must be passed")
		return
	_validate_duration(entry, report, label)
	timeline = _validate_timeline(entry, report, label)
	_validate_assertions(entry, COMMON_ASSERTIONS | STEAM_CASE_ASSERTIONS[case_id], report, label)

	account_labels = entry.get("account_labels")
	if not isinstance(account_labels, list):
		report.errors.append(f"{label}.account_labels must be an array")
		return
	selected = [str(value) for value in account_labels]
	report.require(len(selected) == 3 and len(set(selected)) == 3, f"{label} must use exactly three distinct accounts")
	for account_label in selected:
		report.require(account_label in accounts, f"{label} references unknown account: {account_label}")
	report.require(
		len({str(accounts[name].get("steam_id")) for name in selected if name in accounts}) == 3,
		f"{label} does not contain three distinct Steam IDs",
	)

	artifacts = entry.get("artifacts")
	if not isinstance(artifacts, list):
		report.errors.append(f"{label}.artifacts must be an array")
		return
	report.require(len(artifacts) == 3, f"{label} must contain one artifact record per account")
	artifact_labels: set[str] = set()
	aggregate_log = ""
	results: list[dict[str, Any]] = []
	for index, raw_artifact in enumerate(artifacts):
		artifact_label = f"{label}.artifacts[{index}]"
		if not isinstance(raw_artifact, dict):
			report.errors.append(f"{artifact_label} must be an object")
			continue
		peer_label = str(raw_artifact.get("account_label", ""))
		artifact_labels.add(peer_label)
		_, log_text = _read_artifact(manifest_path, root_value, raw_artifact.get("log"), report, f"{artifact_label}.log")
		_validate_log_text(log_text, report, f"{artifact_label}.log")
		aggregate_log += "\n" + log_text
		result_path = raw_artifact.get("result")
		force_terminated = raw_artifact.get("force_terminated") is True
		if result_path in (None, ""):
			report.require(force_terminated, f"{artifact_label} may omit result only when force_terminated=true")
			continue
		resolved = _resolve_path(manifest_path, root_value, result_path)
		result = _load_json(resolved, report, f"{artifact_label}.result")
		if result is None:
			continue
		report.require(result.get("schema_version") == 1, f"{artifact_label}.result schema mismatch")
		report.require(result.get("build_id") == build_id, f"{artifact_label}.result build mismatch")
		report.require(result.get("scenario") == case_id, f"{artifact_label}.result scenario mismatch")
		report.require(result.get("peer_label") == peer_label, f"{artifact_label}.result peer mismatch")
		report.require(result.get("outcome") != "harness_failure", f"{artifact_label}.result reports harness failure")
		if peer_label in accounts:
			report.require(str(result.get("steam_id")) == str(accounts[peer_label].get("steam_id")), f"{artifact_label}.result Steam ID mismatch")
		results.append(result)
	report.require(artifact_labels == set(selected), f"{label}.artifacts do not match account_labels")

	markers = {
		"active_owner_transfer": ("MIGRATION_ACTIVE_BEGIN", "MIGRATION_HOST_BOUND", "MIGRATION_CHECKPOINT_RESTORED", "kind=WINNER"),
		"source_crash": ("kind=SOURCE_KILL_READY", "MIGRATION_HOST_BOUND", "MIGRATION_CHECKPOINT_RESTORED", "kind=WINNER"),
		"automatic_owner_second_transfer": ("kind=SECONDARY_OWNER_REQUEST", "MIGRATION_HOST_BOUND", "kind=WINNER"),
		"owner_timeout": ("MIGRATION_ACTIVE_FAILED reason=SteamOwnerTimeout", "kind=SAFE_END"),
		"candidate_exit": ("kind=SOURCE_KILL_READY",),
	}[case_id]
	for marker in markers:
		report.require(marker in aggregate_log, f"{label} logs are missing marker: {marker}")
	if case_id == "candidate_exit":
		candidate_seats = set(re.findall(r"MIGRATION_PLAN_READY phase=InGame candidate_seat=([0-9]+)", aggregate_log))
		report.require(len(candidate_seats) >= 2, f"{label} must prove a replacement candidate plan")
	if case_id in {"active_owner_transfer", "source_crash", "automatic_owner_second_transfer"}:
		winners = {int(result.get("winner", -1)) for result in results if result.get("outcome") == "continue"}
		report.require(len(winners) == 1 and next(iter(winners), -1) >= 0, f"{label} survivors do not report one winner")
	if case_id == "owner_timeout":
		report.require(all(result.get("outcome") in {"safe_end", "source_exit"} for result in results), f"{label} contains a continuing result")

	timeline_events = [str(event.get("event", "")) for event in timeline]
	if case_id in {"source_crash", "automatic_owner_second_transfer"}:
		report.require("source_force_killed" in timeline_events, f"{label}.timeline is missing source_force_killed")
	if case_id == "candidate_exit":
		report.require("candidate_force_killed" in timeline_events, f"{label}.timeline is missing candidate_force_killed")
	if case_id == "automatic_owner_second_transfer":
		owner_events = [event for event in timeline if event.get("event") == "lobby_owner_changed"]
		report.require(len(owner_events) >= 3, f"{label}.timeline must preserve source, non-candidate, and candidate owners")


def _validate_steam(
	manifest: dict[str, Any],
	manifest_path: Path,
	root_value: Any,
	build_id: str,
	report: ValidationReport,
	allow_pending: bool,
) -> None:
	accounts = _validate_accounts(manifest, report)
	cases = _collect_cases(manifest.get("steam_cases"), REQUIRED_STEAM_CASES, report, "steam_cases")
	for case_id in sorted(REQUIRED_STEAM_CASES & cases.keys()):
		_validate_steam_case(case_id, cases[case_id], accounts, manifest_path, root_value, build_id, report, allow_pending)


def _validate_target_runtime_evidence(
	entry: dict[str, Any],
	platform: str,
	device_ids: set[str],
	manifest_path: Path,
	root_value: Any,
	build_id: str,
	report: ValidationReport,
	label: str,
) -> None:
	evidence_raw = entry.get("target_runtime_evidence")
	audit_raw = entry.get("release_audit")
	if not isinstance(evidence_raw, str) or not evidence_raw.strip():
		report.errors.append(f"{label}.target_runtime_evidence path is missing")
		return
	if not isinstance(audit_raw, str) or not audit_raw.strip():
		report.errors.append(f"{label}.release_audit path is missing")
		return
	evidence_path = _resolve_path(manifest_path, root_value, evidence_raw)
	audit_path = _resolve_path(manifest_path, root_value, audit_raw)
	evidence = _load_json(evidence_path, report, f"{label}.target_runtime_evidence")
	audit = _load_json(audit_path, report, f"{label}.release_audit")
	if evidence is None or audit is None:
		return

	report.require(evidence.get("schema_version") == 1, f"{label}.target_runtime_evidence schema mismatch")
	report.require(evidence.get("status") == "passed", f"{label}.target_runtime_evidence status must be passed")
	report.require(evidence.get("build_id") == build_id, f"{label}.target_runtime_evidence build mismatch")
	report.require(evidence.get("network_protocol") == EXPECTED_NETWORK_PROTOCOL, f"{label}.target_runtime_evidence protocol mismatch")
	report.require(evidence.get("platform") == platform, f"{label}.target_runtime_evidence platform mismatch")
	device_id = str(evidence.get("device_id", ""))
	report.require(device_id in device_ids, f"{label}.target_runtime_evidence device must match a LAN device")
	_validate_duration(
		{
			"started_at_utc": evidence.get("started_at_utc"),
			"ended_at_utc": evidence.get("completed_at_utc"),
		},
		report,
		f"{label}.target_runtime_evidence",
	)
	host = evidence.get("host")
	if not isinstance(host, dict):
		report.errors.append(f"{label}.target_runtime_evidence.host must be an object")
	else:
		report.require(host.get("platform") == platform, f"{label}.target_runtime_evidence.host.platform mismatch")
		for key in ("system", "release", "machine", "hostname"):
			report.require(bool(str(host.get(key, "")).strip()), f"{label}.target_runtime_evidence.host.{key} is missing")

	report.require(audit.get("schema_version") == 1, f"{label}.release_audit schema mismatch")
	report.require(audit.get("status") in {"passed", "target_runtime_pending"}, f"{label}.release_audit status is invalid")
	report.require(audit.get("build_id") == build_id, f"{label}.release_audit build mismatch")
	report.require(audit.get("network_protocol") == EXPECTED_NETWORK_PROTOCOL, f"{label}.release_audit protocol mismatch")
	report.require(audit.get("platform") == platform, f"{label}.release_audit platform mismatch")
	report.require(audit.get("source_integrity", {}).get("unchanged") is True, f"{label}.release_audit source integrity failed")
	report.require(audit.get("package_audit", {}).get("content", {}).get("ok") is True, f"{label}.release_audit package audit failed")
	report.require(audit.get("runtime_smoke", {}).get("pack_harness", {}).get("ok") is True, f"{label}.release_audit pack harness failed")
	log_error_counts = audit.get("log_error_counts")
	report.require(
		isinstance(log_error_counts, dict)
		and all(log_error_counts.get(marker) == 0 for marker in RELEASE_LOG_BLOCKERS),
		f"{label}.release_audit contains blocking log counts",
	)

	audit_reference = evidence.get("release_audit")
	if not isinstance(audit_reference, dict):
		report.errors.append(f"{label}.target_runtime_evidence.release_audit must be an object")
	else:
		expected_audit_hash = str(audit_reference.get("sha256", "")).lower()
		report.require(SHA256_PATTERN.fullmatch(expected_audit_hash) is not None, f"{label}.target_runtime_evidence release audit hash is invalid")
		actual_audit_hash = _sha256_file(audit_path, report, f"{label}.release_audit")
		report.require(expected_audit_hash == actual_audit_hash, f"{label}.target_runtime_evidence release audit hash mismatch")

	audit_artifact = audit.get("artifact")
	evidence_artifacts = evidence.get("artifacts")
	if not isinstance(audit_artifact, dict) or not isinstance(evidence_artifacts, dict):
		report.errors.append(f"{label} artifact hashes are missing")
	else:
		for kind in ("executable", "pck"):
			artifact_label = f"{label}.target_runtime_evidence.artifacts.{kind}"
			evidence_artifact = evidence_artifacts.get(kind)
			if not isinstance(evidence_artifact, dict):
				report.errors.append(f"{artifact_label} must be an object")
				continue
			audit_hash = str(audit_artifact.get(f"{kind}_sha256", "")).lower()
			expected_hash = str(evidence_artifact.get("expected_sha256", "")).lower()
			actual_hash = str(evidence_artifact.get("actual_sha256", "")).lower()
			report.require(SHA256_PATTERN.fullmatch(audit_hash) is not None, f"{label}.release_audit {kind} hash is invalid")
			report.require(expected_hash == audit_hash, f"{artifact_label} expected hash does not match release audit")
			report.require(actual_hash == audit_hash, f"{artifact_label} actual hash does not match release audit")
			size_bytes = evidence_artifact.get("size_bytes")
			report.require(isinstance(size_bytes, int) and size_bytes > 0, f"{artifact_label}.size_bytes must be positive")

	runtime = evidence.get("runtime_smoke")
	if not isinstance(runtime, dict):
		report.errors.append(f"{label}.target_runtime_evidence.runtime_smoke must be an object")
		return
	_validate_duration(runtime, report, f"{label}.target_runtime_evidence.runtime_smoke")
	duration = runtime.get("duration_seconds")
	report.require(isinstance(duration, (int, float)) and 0 <= duration <= 300, f"{label}.target_runtime_evidence runtime duration is invalid")
	report.require(runtime.get("exit_code") == 0, f"{label}.target_runtime_evidence runtime exit code must be zero")
	expected_build_marker = (
		f"RELEASE_BUILD_INFO build_id={build_id} protocol={EXPECTED_NETWORK_PROTOCOL} "
		f"platform={GODOT_PLATFORM_NAMES[platform]}"
	)
	expected_marker = f"{RELEASE_EXECUTABLE_SMOKE_MARKER} platform={GODOT_PLATFORM_NAMES[platform]}"
	report.require(runtime.get("build_marker") == expected_build_marker, f"{label}.target_runtime_evidence build marker mismatch")
	report.require(runtime.get("success_marker") == expected_marker, f"{label}.target_runtime_evidence success marker mismatch")
	log_path, log_text = _read_artifact(
		evidence_path,
		".",
		runtime.get("log_path"),
		report,
		f"{label}.target_runtime_evidence.runtime_smoke.log",
	)
	_validate_log_text(log_text, report, f"{label}.target_runtime_evidence.runtime_smoke.log")
	report.require(expected_build_marker in log_text.splitlines(), f"{label}.target_runtime_evidence runtime log is missing exact build marker")
	report.require(expected_marker in log_text.splitlines(), f"{label}.target_runtime_evidence runtime log is missing exact marker")
	if log_path is not None:
		expected_log_hash = str(runtime.get("log_sha256", "")).lower()
		report.require(SHA256_PATTERN.fullmatch(expected_log_hash) is not None, f"{label}.target_runtime_evidence runtime log hash is invalid")
		report.require(
			expected_log_hash == _sha256_file(log_path, report, f"{label}.target_runtime_evidence.runtime_smoke.log"),
			f"{label}.target_runtime_evidence runtime log hash mismatch",
		)
	blocker_counts = runtime.get("blocker_counts")
	report.require(
		isinstance(blocker_counts, dict)
		and all(blocker_counts.get(marker) == 0 for marker in RELEASE_LOG_BLOCKERS),
		f"{label}.target_runtime_evidence runtime blocker counts must be zero",
	)


def _validate_lan(
	manifest: dict[str, Any],
	manifest_path: Path,
	root_value: Any,
	build_id: str,
	report: ValidationReport,
	allow_pending: bool,
) -> None:
	runs = _collect_cases(manifest.get("real_lan_runs"), REQUIRED_LAN_PLATFORMS, report, "real_lan_runs")
	for platform in sorted(REQUIRED_LAN_PLATFORMS & runs.keys()):
		entry = runs[platform]
		label = f"real_lan_runs[{platform}]"
		if entry.get("status") != "passed":
			if allow_pending and entry.get("status") == "pending":
				report.warnings.append(f"{label} is pending")
				continue
			report.errors.append(f"{label}.status must be passed")
			continue
		_validate_duration(entry, report, label)
		_validate_assertions(entry, COMMON_ASSERTIONS | {"two_physical_devices", "same_build", "private_state_not_leaked"}, report, label)
		report.require(entry.get("build_id") == build_id, f"{label}.build_id mismatch")
		host_device = str(entry.get("host_device_id", ""))
		client_device = str(entry.get("client_device_id", ""))
		report.require(bool(host_device) and bool(client_device) and host_device != client_device, f"{label} must name two physical devices")
		_validate_target_runtime_evidence(
			entry,
			platform,
			{host_device, client_device},
			manifest_path,
			root_value,
			build_id,
			report,
			label,
		)
		_, host_log = _read_artifact(manifest_path, root_value, entry.get("host_log"), report, f"{label}.host_log")
		_, client_log = _read_artifact(manifest_path, root_value, entry.get("client_log"), report, f"{label}.client_log")
		result_path, result_text = _read_artifact(manifest_path, root_value, entry.get("result"), report, f"{label}.result")
		_validate_log_text(host_log + client_log, report, label)
		build_marker = (
			f"RELEASE_BUILD_INFO build_id={build_id} protocol={EXPECTED_NETWORK_PROTOCOL} "
			f"platform={GODOT_PLATFORM_NAMES[platform]}"
		)
		report.require(build_marker in host_log.splitlines(), f"{label}.host_log is missing release build marker")
		report.require(build_marker in client_log.splitlines(), f"{label}.client_log is missing release build marker")
		for marker in ("MIGRATION_PLAN_READY", "REAL_LAN_HOST_KILL_9"):
			report.require(marker in host_log, f"{label}.host_log is missing {marker}")
		for marker in ("MIGRATION_HOST_BOUND", "MIGRATION_CHECKPOINT_RESTORED", "MIGRATION_RECOVERY_READY", "FULL_MATCH_CLIENT_WINNER", "FULL_MATCH_CLIENT_DONE"):
			report.require(marker in client_log, f"{label}.client_log is missing {marker}")
		report.require("private_leak=false" in result_text, f"{label}.result does not prove private_leak=false")
		report.require(result_path is not None, f"{label}.result is missing")


def _validate_network(
	manifest: dict[str, Any],
	manifest_path: Path,
	root_value: Any,
	build_id: str,
	report: ValidationReport,
	allow_pending: bool,
) -> None:
	cases = _collect_cases(manifest.get("network_impairment_cases"), REQUIRED_NETWORK_CASES, report, "network_impairment_cases")
	for case_id in sorted(REQUIRED_NETWORK_CASES & cases.keys()):
		entry = cases[case_id]
		label = f"network_impairment_cases[{case_id}]"
		if entry.get("status") != "passed":
			if allow_pending and entry.get("status") == "pending":
				report.warnings.append(f"{label} is pending")
				continue
			report.errors.append(f"{label}.status must be passed")
			continue
		_validate_duration(entry, report, label)
		scenario_assertions = (
			{"external_process_termination"}
			if case_id == "host_candidate_simultaneous_loss"
			else {"real_udp_impairment"}
		)
		_validate_assertions(entry, COMMON_ASSERTIONS | {"final_state_consistent"} | scenario_assertions, report, label)
		report.require(entry.get("build_id") == build_id, f"{label}.build_id mismatch")
		report.require(bool(str(entry.get("driver", "")).strip()), f"{label}.driver is missing")
		logs = entry.get("logs")
		if not isinstance(logs, list) or not logs:
			report.errors.append(f"{label}.logs must be a non-empty array")
			continue
		aggregate = ""
		for index, raw_path in enumerate(logs):
			_, text = _read_artifact(manifest_path, root_value, raw_path, report, f"{label}.logs[{index}]")
			_validate_log_text(text, report, f"{label}.logs[{index}]")
			aggregate += "\n" + text
		report.require(f"LAN_NETWORK_IMPAIRMENT_CASE_OK {case_id}" in aggregate, f"{label} is missing its success marker")
		if case_id == "short_disconnect":
			report.require("FULL_MATCH_CLIENT_STATE 8" in aggregate, f"{label} did not enter Reconnecting")
			report.require("NETWORK_IMPAIRMENT_CLEARED" in aggregate, f"{label} did not record link restoration")
		elif case_id == "host_candidate_simultaneous_loss":
			report.require("MIGRATION_FAILED code=ConnectTimeout" in aggregate, f"{label} did not reach ConnectTimeout")
			report.require("artifacts=false" in aggregate, f"{label} did not clear migration artifacts")
		else:
			report.require("NETWORK_IMPAIRMENT_APPLIED" in aggregate, f"{label} did not record impairment application")
			report.require("FULL_MATCH_CLIENT_WINNER" in aggregate, f"{label} did not finish the migrated match")


def validate_manifest(path: Path, *, scope: str = "all", allow_pending: bool = False) -> ValidationReport:
	report = ValidationReport()
	manifest = _load_json(path, report, "manifest")
	if manifest is None:
		return report
	report.require(manifest.get("schema_version") == 1, "schema_version must be 1")
	report.require(bool(str(manifest.get("release_id", "")).strip()), "release_id is missing")
	build = manifest.get("build")
	if not isinstance(build, dict):
		report.errors.append("build must be an object")
		build = {}
	build_id = str(build.get("build_id", "")).strip()
	report.require(bool(build_id), "build.build_id is missing")
	report.require(build.get("configuration") == "Release", "build.configuration must be Release")
	report.require(build.get("network_protocol") == EXPECTED_NETWORK_PROTOCOL, f"build.network_protocol must be {EXPECTED_NETWORK_PROTOCOL}")
	root_value = manifest.get("evidence_root", ".")
	if scope in {"all", "steam"}:
		_validate_steam(manifest, path, root_value, build_id, report, allow_pending)
	if scope in {"all", "lan"}:
		_validate_lan(manifest, path, root_value, build_id, report, allow_pending)
	if scope in {"all", "network"}:
		_validate_network(manifest, path, root_value, build_id, report, allow_pending)
	return report


def main() -> int:
	parser = argparse.ArgumentParser(description="Validate release-grade host migration evidence.")
	parser.add_argument("manifest", type=Path)
	parser.add_argument("--scope", choices=("all", "steam", "lan", "network"), default="all")
	parser.add_argument("--allow-pending", action="store_true")
	args = parser.parse_args()
	report = validate_manifest(args.manifest.resolve(), scope=args.scope, allow_pending=args.allow_pending)
	for warning in report.warnings:
		print(f"WARN {warning}")
	for error in report.errors:
		print(f"ERROR {error}", file=sys.stderr)
	if report.ok:
		print(f"HOST_MIGRATION_RELEASE_EVIDENCE_OK scope={args.scope}")
		return 0
	print(f"HOST_MIGRATION_RELEASE_EVIDENCE_FAILED errors={len(report.errors)}", file=sys.stderr)
	return 1


if __name__ == "__main__":
	raise SystemExit(main())
