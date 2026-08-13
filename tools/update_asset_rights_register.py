#!/usr/bin/env python3
"""Generate or verify Deckland's tracked media asset rights register."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import subprocess
import sys
from pathlib import Path
from typing import Sequence


ROOT = Path(__file__).resolve().parent.parent
REGISTER_RELATIVE_PATH = Path("legal/ASSET_RIGHTS_REGISTER.csv")
ASSET_ROOTS = ("art/", "asset/", "assets/", "resources/")
MEDIA_TYPES = {
	".bmp": "image",
	".flac": "audio",
	".gif": "image",
	".ico": "image",
	".jpeg": "image",
	".jpg": "image",
	".mov": "video",
	".mp3": "audio",
	".mp4": "video",
	".ogg": "audio",
	".ogv": "video",
	".otf": "font",
	".png": "image",
	".svg": "image",
	".tga": "image",
	".ttf": "font",
	".wav": "audio",
	".webm": "video",
	".webp": "image",
	".woff": "font",
	".woff2": "font",
}
FIELDNAMES = (
	"asset_path",
	"asset_type",
	"file_sha256",
	"ownership",
	"provenance",
	"rights_holder",
	"license_or_terms",
	"commercial_use",
	"redistribution",
	"rights_basis",
	"notes",
)


class RegisterError(RuntimeError):
	pass


def _sha256(path: Path) -> str:
	digest = hashlib.sha256()
	with path.open("rb") as handle:
		for chunk in iter(lambda: handle.read(1024 * 1024), b""):
			digest.update(chunk)
	return digest.hexdigest()


def tracked_media_paths(root: Path) -> list[str]:
	result = subprocess.run(
		["git", "ls-files", "--", *[value.rstrip("/") for value in ASSET_ROOTS]],
		cwd=root,
		text=True,
		stdout=subprocess.PIPE,
		stderr=subprocess.PIPE,
		check=False,
	)
	if result.returncode != 0:
		raise RegisterError(f"git ls-files failed: {result.stderr.strip()}")
	paths = []
	for raw_path in result.stdout.splitlines():
		path = raw_path.strip()
		if path.startswith(ASSET_ROOTS) and Path(path).suffix.lower() in MEDIA_TYPES:
			paths.append(path)
	return sorted(set(paths))


def untracked_unignored_media_paths(root: Path) -> list[str]:
	result = subprocess.run(
		[
			"git",
			"ls-files",
			"--others",
			"--exclude-standard",
			"--",
			*[value.rstrip("/") for value in ASSET_ROOTS],
		],
		cwd=root,
		text=True,
		stdout=subprocess.PIPE,
		stderr=subprocess.PIPE,
		check=False,
	)
	if result.returncode != 0:
		raise RegisterError(f"git untracked-file scan failed: {result.stderr.strip()}")
	return sorted(
		path
		for path in (raw_path.strip() for raw_path in result.stdout.splitlines())
		if path.startswith(ASSET_ROOTS) and Path(path).suffix.lower() in MEDIA_TYPES
	)


def build_rows(root: Path) -> list[dict[str, str]]:
	rows: list[dict[str, str]] = []
	for relative_path in tracked_media_paths(root):
		path = root / relative_path
		if not path.is_file():
			raise RegisterError(f"Tracked media file is missing from the worktree: {relative_path}")
		asset_type = MEDIA_TYPES[path.suffix.lower()]
		if relative_path.startswith("assets/fonts/noto/") and asset_type == "font":
			row = {
				"asset_path": relative_path,
				"asset_type": asset_type,
				"file_sha256": _sha256(path),
				"ownership": "third_party",
				"provenance": "Noto font distribution",
				"rights_holder": "Noto Project Authors",
				"license_or_terms": "SIL Open Font License 1.1",
				"commercial_use": "yes",
				"redistribution": "yes_with_license",
				"rights_basis": "assets/fonts/noto/OFL.txt",
				"notes": "Public license copy is included in legal/distribution/licenses.",
			}
		else:
			row = {
				"asset_path": relative_path,
				"asset_type": asset_type,
				"file_sha256": _sha256(path),
				"ownership": "project_owned",
				"provenance": "original_or_ai_assisted",
				"rights_holder": "Deckland project owner/publisher of record",
				"license_or_terms": "Proprietary project asset",
				"commercial_use": "yes",
				"redistribution": "yes",
				"rights_basis": "OWNER_DECLARATION_2026-08-13",
				"notes": "Owner-confirmed; AI involvement is not inferred per individual file.",
			}
		rows.append(row)
	return rows


def render_register(root: Path) -> str:
	buffer = io.StringIO(newline="")
	writer = csv.DictWriter(buffer, fieldnames=FIELDNAMES, lineterminator="\n")
	writer.writeheader()
	writer.writerows(build_rows(root))
	return buffer.getvalue()


def check_register(root: Path) -> tuple[bool, str]:
	untracked = untracked_unignored_media_paths(root)
	if untracked:
		preview = ", ".join(untracked[:5])
		if len(untracked) > 5:
			preview += f", ... ({len(untracked)} total)"
		return False, (
			"Untracked, unignored media can enter release staging without rights records: "
			+ preview
		)
	path = root / REGISTER_RELATIVE_PATH
	if not path.is_file():
		return False, f"Asset rights register is missing: {path}"
	expected = render_register(root)
	actual = path.read_text(encoding="utf-8")
	if actual != expected:
		return False, (
			"Asset rights register is stale. Run "
			"python3 tools/update_asset_rights_register.py --write"
		)
	return True, f"ASSET_RIGHTS_REGISTER_OK rows={len(build_rows(root))}"


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description=__doc__)
	mode = parser.add_mutually_exclusive_group(required=True)
	mode.add_argument("--write", action="store_true", help="Regenerate the committed CSV register")
	mode.add_argument("--check", action="store_true", help="Verify the committed register without changing files")
	parser.add_argument("--root", type=Path, default=ROOT, help=argparse.SUPPRESS)
	return parser


def main(argv: Sequence[str] | None = None) -> int:
	args = build_parser().parse_args(argv)
	root = args.root.resolve()
	try:
		if args.write:
			output = root / REGISTER_RELATIVE_PATH
			output.parent.mkdir(parents=True, exist_ok=True)
			rows = build_rows(root)
			output.write_text(render_register(root), encoding="utf-8", newline="")
			print(f"ASSET_RIGHTS_REGISTER_WRITTEN rows={len(rows)} path={output}")
			return 0
		ok, message = check_register(root)
		print(message, file=sys.stdout if ok else sys.stderr)
		return 0 if ok else 1
	except (OSError, RegisterError) as error:
		print(f"Asset rights register error: {error}", file=sys.stderr)
		return 1


if __name__ == "__main__":
	raise SystemExit(main())
