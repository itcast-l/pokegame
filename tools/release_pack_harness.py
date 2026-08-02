#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import os
import platform as platform_module
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence

import verify_release_target_runtime as release_runtime


class HarnessError(RuntimeError):
	pass


@dataclass(frozen=True)
class RuntimeLayout:
	runtime_directory: str
	native_mappings: tuple[tuple[str, str], ...]


@dataclass(frozen=True)
class ReleasePack:
	report_path: Path
	report_sha256: str
	report: dict[str, Any]
	platform: str
	build_id: str
	network_protocol: int
	executable: Path
	pck: Path
	artifacts: dict[str, dict[str, Any]]


@dataclass(frozen=True)
class HarnessWorkspace:
	root: Path
	home: Path
	mapping_modes: dict[str, str]


RUNTIME_LAYOUTS = {
	("windows", "x86_64"): RuntimeLayout(
		"data_pokegame_windows_x86_64",
		(
			(
				"libgodotsteam.windows.template_release.x86_64.dll",
				"addons/godotsteam/win64/libgodotsteam.windows.template_debug.x86_64.dll",
			),
			("steam_api64.dll", "addons/godotsteam/win64/steam_api64.dll"),
		),
	),
	("linux", "x86_64"): RuntimeLayout(
		"data_pokegame_linuxbsd_x86_64",
		(
			(
				"libgodotsteam.linux.template_release.x86_64.so",
				"addons/godotsteam/linux64/libgodotsteam.linux.template_debug.x86_64.so",
			),
			("libsteam_api.so", "addons/godotsteam/linux64/libsteam_api.so"),
		),
	),
	("linux", "arm64"): RuntimeLayout(
		"data_pokegame_linuxbsd_arm64",
		(
			(
				"libgodotsteam.linux.template_release.arm64.so",
				"addons/godotsteam/linuxarm64/libgodotsteam.linux.template_debug.arm64.so",
			),
			("libsteam_api.so", "addons/godotsteam/linuxarm64/libsteam_api.so"),
		),
	),
	("macos", "x86_64"): RuntimeLayout(
		"data_pokegame_macos_x86_64",
		(
			(
				"../Frameworks/libgodotsteam.macos.template_release.dylib",
				"addons/godotsteam/osx/libgodotsteam.macos.template_debug.dylib",
			),
			("../Frameworks/libsteam_api.dylib", "addons/godotsteam/osx/libsteam_api.dylib"),
		),
	),
	("macos", "arm64"): RuntimeLayout(
		"data_pokegame_macos_arm64",
		(
			(
				"../Frameworks/libgodotsteam.macos.template_release.dylib",
				"addons/godotsteam/osx/libgodotsteam.macos.template_debug.dylib",
			),
			("../Frameworks/libsteam_api.dylib", "addons/godotsteam/osx/libsteam_api.dylib"),
		),
	),
}


def _normalize_machine(machine: str) -> str:
	value = machine.strip().lower().replace("-", "_")
	if value in {"amd64", "x64", "x86_64"}:
		return "x86_64"
	if value in {"aarch64", "arm64"}:
		return "arm64"
	return value


def load_release_pack(report_path: Path, *, host_platform: str | None = None) -> ReleasePack:
	report_path = report_path.resolve()
	host_platform = host_platform or release_runtime._host_platform()
	try:
		report = release_runtime._load_json(report_path, "release audit")
		build_id, network_protocol, artifact = release_runtime._require_release_audit(
			report,
			host_platform,
		)
		executable, executable_relative = release_runtime._resolve_artifact(
			report_path,
			artifact,
			"executable",
		)
		pck, pck_relative = release_runtime._resolve_artifact(report_path, artifact, "pck")
		artifacts = {
			"executable": release_runtime._artifact_evidence(
				executable,
				executable_relative,
				artifact.get("executable_sha256"),
				"release executable",
			),
			"pck": release_runtime._artifact_evidence(
				pck,
				pck_relative,
				artifact.get("pck_sha256"),
				"release PCK",
			),
		}
	except (OSError, ValueError, release_runtime.VerificationError) as error:
		raise HarnessError(str(error)) from error
	return ReleasePack(
		report_path=report_path,
		report_sha256=release_runtime._sha256_file(report_path),
		report=report,
		platform=str(report["platform"]),
		build_id=build_id,
		network_protocol=network_protocol,
		executable=executable,
		pck=pck,
		artifacts=artifacts,
	)


def _resolve_layout(release_pack: ReleasePack, machine: str | None = None) -> RuntimeLayout:
	architecture = _normalize_machine(machine or platform_module.machine())
	key = (release_pack.platform, architecture)
	if key not in RUNTIME_LAYOUTS:
		raise HarnessError(
			f"No Release PCK harness layout for platform={release_pack.platform} machine={architecture}"
		)
	return RUNTIME_LAYOUTS[key]


def _link_or_copy_file(source: Path, target: Path) -> str:
	target.parent.mkdir(parents=True, exist_ok=True)
	try:
		target.symlink_to(source.resolve())
		return "symlink"
	except (NotImplementedError, OSError):
		shutil.copy2(source, target)
		return "copy"


def _link_or_copy_tree(source: Path, target: Path) -> str:
	target.parent.mkdir(parents=True, exist_ok=True)
	try:
		target.symlink_to(source.resolve(), target_is_directory=True)
		return "symlink"
	except (NotImplementedError, OSError):
		shutil.copytree(source, target)
		return "copy"


def prepare_release_pack_harness(
	release_pack: ReleasePack,
	root: Path,
	*,
	machine: str | None = None,
) -> HarnessWorkspace:
	root = root.resolve()
	if root.exists() and any(root.iterdir()):
		raise HarnessError(f"Harness workspace must be empty: {root}")
	root.mkdir(parents=True, exist_ok=True)
	(root / "project.godot").write_text(
		'[application]\nconfig/name="Release Pack Harness"\n',
		encoding="utf-8",
	)
	layout = _resolve_layout(release_pack, machine)
	package_resource_root = release_pack.pck.parent
	runtime_source = (package_resource_root / layout.runtime_directory).resolve()
	if not (runtime_source / "pokegame.dll").is_file():
		raise HarnessError(f"Release C# runtime is missing pokegame.dll: {runtime_source}")
	assembly_paths = release_pack.report.get("artifact", {}).get("assembly_paths", [])
	if not isinstance(assembly_paths, list) or not any(
		Path(str(path)).parent.name == layout.runtime_directory for path in assembly_paths
	):
		raise HarnessError(
			f"Release audit does not declare the selected C# runtime: {layout.runtime_directory}"
		)
	mapping_modes = {
		"csharp_runtime": _link_or_copy_tree(
			runtime_source,
			root / ".godot/mono/temp/bin/Debug",
		)
	}
	for source_relative, target_relative in layout.native_mappings:
		source = (package_resource_root / source_relative).resolve()
		if not source.is_file():
			raise HarnessError(f"Release native runtime is missing: {source}")
		mapping_modes[target_relative] = _link_or_copy_file(source, root / target_relative)
	home = root / "home"
	for path in (
		home,
		home / "Library/Application Support",
		home / "Library/Logs",
		home / ".config",
		home / ".cache",
		home / ".local/share",
		home / "AppData/Roaming",
		home / "AppData/Local",
	):
		path.mkdir(parents=True, exist_ok=True)
	return HarnessWorkspace(root=root, home=home, mapping_modes=mapping_modes)


def resolve_godot_binary(explicit: Path | None = None) -> Path:
	candidates: list[Path] = []
	if explicit is not None:
		candidates.append(explicit)
	if os.environ.get("GODOT_BIN"):
		candidates.append(Path(os.environ["GODOT_BIN"]))
	for name in ("Godot", "godot-mono", "godot"):
		resolved = shutil.which(name)
		if resolved:
			candidates.append(Path(resolved))
	candidates.append(Path("/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"))
	for candidate in candidates:
		path = candidate.expanduser().resolve()
		if path.is_file():
			return path
	raise HarnessError("Missing Godot 4.6 .NET editor binary. Set GODOT_BIN or pass --godot.")


def build_harness_command(
	godot: Path,
	release_pack: ReleasePack,
	workspace: HarnessWorkspace,
	script: Path,
	harness_args: Sequence[str] = (),
) -> list[str]:
	script = script.resolve()
	if not script.is_file():
		raise HarnessError(f"External GDScript harness is missing: {script}")
	return [
		str(godot.resolve()),
		"--headless",
		"--audio-driver",
		"Dummy",
		"--path",
		str(workspace.root),
		"--main-pack",
		str(release_pack.pck),
		"--script",
		str(script),
		"--",
		*list(harness_args),
	]


def build_harness_environment(
	workspace: HarnessWorkspace,
	*,
	base: Mapping[str, str] | None = None,
	extra: Mapping[str, str] | None = None,
) -> dict[str, str]:
	environment = dict(base or os.environ)
	environment.update(
		{
			"DECKLAND_TEST_MUTE_AUDIO": "1",
			"HOME": str(workspace.home),
			"XDG_CONFIG_HOME": str(workspace.home / ".config"),
			"XDG_CACHE_HOME": str(workspace.home / ".cache"),
			"XDG_DATA_HOME": str(workspace.home / ".local/share"),
			"APPDATA": str(workspace.home / "AppData/Roaming"),
			"LOCALAPPDATA": str(workspace.home / "AppData/Local"),
		}
	)
	if extra:
		environment.update(extra)
	return environment


def _parse_environment(values: Sequence[str]) -> dict[str, str]:
	result: dict[str, str] = {}
	for value in values:
		if "=" not in value or not value.split("=", 1)[0]:
			raise HarnessError(f"Invalid --env value: {value}")
		name, raw_value = value.split("=", 1)
		result[name] = raw_value
	return result


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description="Run an external GDScript against an audited Release PCK.")
	parser.add_argument("release_audit", type=Path)
	parser.add_argument("script", type=Path)
	parser.add_argument("--godot", type=Path)
	parser.add_argument("--log", type=Path, required=True)
	parser.add_argument("--evidence", type=Path)
	parser.add_argument("--runtime-dir", type=Path)
	parser.add_argument("--timeout", type=int, default=180)
	parser.add_argument("--env", action="append", default=[])
	parser.add_argument("--expect-marker", action="append", default=[])
	parser.add_argument("harness_args", nargs=argparse.REMAINDER)
	return parser


def main(argv: Sequence[str] | None = None) -> int:
	args = build_parser().parse_args(argv)
	log_path = args.log.resolve()
	evidence_path = args.evidence.resolve() if args.evidence else None
	evidence: dict[str, Any] = {"schema_version": 1, "status": "failed"}
	try:
		if args.timeout <= 0:
			raise HarnessError("--timeout must be positive")
		release_pack = load_release_pack(args.release_audit)
		godot = resolve_godot_binary(args.godot)
		harness_args = list(args.harness_args)
		if harness_args and harness_args[0] == "--":
			harness_args.pop(0)
		with tempfile.TemporaryDirectory(prefix="deckland-release-pack-") if args.runtime_dir is None else _NullContext(str(args.runtime_dir)) as runtime:
			workspace = prepare_release_pack_harness(release_pack, Path(runtime))
			command = build_harness_command(godot, release_pack, workspace, args.script, harness_args)
			environment = build_harness_environment(
				workspace,
				extra=_parse_environment(args.env),
			)
			result = subprocess.run(
				command,
				cwd=workspace.root,
				env=environment,
				text=True,
				stdout=subprocess.PIPE,
				stderr=subprocess.STDOUT,
				timeout=args.timeout,
				check=False,
			)
		log_path.parent.mkdir(parents=True, exist_ok=True)
		log_path.write_text(result.stdout, encoding="utf-8")
		sys.stdout.write(result.stdout)
		if result.returncode != 0:
			raise HarnessError(f"Godot harness exited with code {result.returncode}")
		lines = result.stdout.splitlines()
		for marker in args.expect_marker:
			if marker not in lines:
				raise HarnessError(f"Godot harness did not emit exact marker: {marker}")
		evidence.update(
			{
				"status": "passed",
				"build_id": release_pack.build_id,
				"network_protocol": release_pack.network_protocol,
				"platform": release_pack.platform,
				"release_audit_sha256": release_pack.report_sha256,
				"artifacts": release_pack.artifacts,
				"log": {"path": str(log_path), "sha256": release_runtime._sha256_file(log_path)},
			}
		)
	except (HarnessError, OSError, subprocess.TimeoutExpired) as error:
		evidence["error"] = str(error)
		print(f"RELEASE_PACK_HARNESS_FAILED {error}", file=sys.stderr)
		status = 1
	else:
		print(
			f"RELEASE_PACK_HARNESS_OK build_id={evidence['build_id']} "
			f"protocol={evidence['network_protocol']} platform={evidence['platform']}"
		)
		status = 0
	if evidence_path is not None:
		evidence_path.parent.mkdir(parents=True, exist_ok=True)
		evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
	return status


class _NullContext:
	def __init__(self, value: str) -> None:
		self.value = value

	def __enter__(self) -> str:
		return self.value

	def __exit__(self, *_: object) -> None:
		return None


if __name__ == "__main__":
	raise SystemExit(main())
