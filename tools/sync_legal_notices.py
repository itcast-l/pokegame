#!/usr/bin/env python3
"""Synchronize or verify pinned third-party legal notices for release packages."""

from __future__ import annotations

import argparse
import hashlib
import sys
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


ROOT = Path(__file__).resolve().parent.parent


@dataclass(frozen=True)
class NoticeSource:
	destination: str
	sha256: str
	url: str | None = None
	local_source: str | None = None


NOTICE_SOURCES = (
	NoticeSource(
		"GODOT_ENGINE_LICENSE.txt",
		"b0435e3b3e4e55238f05f4b306f30524a1b2e20147810d436eaa554fa6855c80",
		url="https://raw.githubusercontent.com/godotengine/godot/4.6.1-stable/LICENSE.txt",
	),
	NoticeSource(
		"GODOT_ENGINE_COPYRIGHT.txt",
		"dc043a7dbf69632560219e72fbc61ef9fa7338c7a773660acd81b51590b69104",
		url="https://raw.githubusercontent.com/godotengine/godot/4.6.1-stable/COPYRIGHT.txt",
	),
	NoticeSource(
		"DOTNET_RUNTIME_LICENSE.txt",
		"cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310",
		url="https://raw.githubusercontent.com/dotnet/runtime/v8.0.25/LICENSE.TXT",
	),
	NoticeSource(
		"DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt",
		"97c1a7b3da6a4c6ad516448719f45114b41a4d4c5aa300a944476e2e4f5da438",
		url="https://raw.githubusercontent.com/dotnet/runtime/v8.0.25/THIRD-PARTY-NOTICES.TXT",
	),
	NoticeSource(
		"PANDORA.txt",
		"d35a32b92e94a1aa43cdb6e0628a2bfcd6bc88ca271b8962243b848b871fd1d5",
		url="https://raw.githubusercontent.com/bitbrain/pandora/v1.0-alpha9/LICENSE",
	),
	NoticeSource(
		"NANOID_GODOT.txt",
		"a00dc3cd7717befd8ce9ddfec2e7088ff0cbadd467d05da86cae4555498cbdf3",
		url="https://raw.githubusercontent.com/eth0net/nanoid-godot/v0.2.0/LICENSE",
	),
	NoticeSource(
		"PHANTOM_CAMERA.txt",
		"9edff15559a55c27d4b0612d73315d0597980d13f7e5b3d4ff80192ea57c76d2",
		url="https://raw.githubusercontent.com/ramokz/phantom-camera/v0.11.0.2/LICENSE",
	),
	NoticeSource(
		"STEAM_LOCALE_MAPPER.txt",
		"015912e59e8669beb7df332f690b788b77b37c343143ea632e0031c3436063b4",
		url=(
			"https://raw.githubusercontent.com/AstralCrowGames/GodotSteamLocaleMapper/"
			"ffabd53c9adbb405b329018d09ddec60d3fd0de5/LICENSE"
		),
	),
	NoticeSource(
		"NUNITO_OFL-1.1.txt",
		"580df76c95a1ec5ab878ceb25bb3d85c6a076804e9c970c8c6972aea775fdf65",
		url=(
			"https://raw.githubusercontent.com/googlefonts/nunito/"
			"8c6a9bb9732545b9ed53f29ec5e1ab0ff53c4e6f/OFL.txt"
		),
	),
	NoticeSource(
		"GODOTSTEAM.txt",
		"279f04511cceed18b63c2d4c72b5274719092f3ba58af618c6ce90f528039d5d",
		local_source="addons/godotsteam/license.md",
	),
	NoticeSource(
		"RESONATE.txt",
		"c99219a6ce1b16542063efe4714bfe8752c3b0f38e4daff398238138c9ce55fa",
		local_source="addons/resonate/LICENSE",
	),
	NoticeSource(
		"SAVESTATE_LITE.txt",
		"c8ce9745049988bb894a7ba4d90fa284c143c04ec134dcbf4b53321d2bbd7f25",
		local_source="docs/vendor/savestate-lite/LICENSE",
	),
	NoticeSource(
		"NOTO_FONTS_OFL-1.1.txt",
		"cee9892f9f0cc8fe882c9e9537ee6a89621d86ee7ceaf70b02e2b2b1c25c061a",
		local_source="assets/fonts/noto/OFL.txt",
	),
)


THIRD_PARTY_NOTICES = """Deckland Third-Party Notices
================================

Deckland includes third-party software and fonts. The corresponding license
texts, copyright information, and notices are provided in the licenses
directory next to this file.

Included components
-------------------

- Godot Engine 4.6.1: GODOT_ENGINE_LICENSE.txt and
  GODOT_ENGINE_COPYRIGHT.txt
- .NET Runtime 8.0.25: DOTNET_RUNTIME_LICENSE.txt and
  DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt
- GodotSteam 4.18.1: GODOTSTEAM.txt
- Steamworks redistributable binaries: STEAMWORKS_SDK_NOTICE.txt
- Pandora 1.0-alpha9: PANDORA.txt
- nanoid-godot 0.2.0 (Pandora generator inspiration): NANOID_GODOT.txt
- Phantom Camera 0.11.0.2: PHANTOM_CAMERA.txt
- Resonate 2.4.0: RESONATE.txt
- SaveState Lite 1.2.0: SAVESTATE_LITE.txt
- Steam Locale Mapper 1.0: STEAM_LOCALE_MAPPER.txt
- Noto fonts: NOTO_FONTS_OFL-1.1.txt
- Nunito fonts supplied with Phantom Camera: NUNITO_OFL-1.1.txt

The names and trademarks of third parties are the property of their
respective owners. Their inclusion does not imply endorsement.
"""


STEAMWORKS_NOTICE = """Steamworks SDK Redistributables Notice
=====================================

Deckland's platform packages may include Steamworks SDK redistributable
binaries (for example steam_api64.dll, libsteam_api.so, or
libsteam_api.dylib) supplied through the GodotSteam integration.

These binaries are proprietary Valve software. They are not covered by the
MIT license for GodotSteam. Their use and redistribution are governed by the
Steamworks SDK Access Agreement, the Steam Distribution Agreement, and other
applicable Valve/Steamworks terms accepted by the publisher.

Official terms and documentation:
https://partner.steamgames.com/documentation/sdk_access_agreement
https://partner.steamgames.com/doc/sdk/uploading#5

This notice intentionally does not reproduce or modify Valve's proprietary
agreement text. The publisher must maintain an active Steamworks account and
comply with the current terms when distributing these binaries.
"""


GENERATED_FILES = {
	"legal/distribution/THIRD_PARTY_NOTICES.txt": THIRD_PARTY_NOTICES.encode("utf-8"),
	"legal/distribution/licenses/STEAMWORKS_SDK_NOTICE.txt": STEAMWORKS_NOTICE.encode("utf-8"),
}


def sha256_bytes(data: bytes) -> str:
	return hashlib.sha256(data).hexdigest()


def expected_notice_bytes(source: NoticeSource, root: Path, *, allow_network: bool) -> bytes:
	if source.local_source is not None:
		path = root / source.local_source
		if not path.is_file():
			raise RuntimeError(f"Local license source is missing: {path}")
		data = path.read_bytes()
	elif allow_network and source.url is not None:
		request = urllib.request.Request(source.url, headers={"User-Agent": "Deckland-license-sync/1"})
		with urllib.request.urlopen(request, timeout=30) as response:
			data = response.read()
	else:
		path = root / "legal/distribution/licenses" / source.destination
		if not path.is_file():
			raise RuntimeError(f"Pinned license file is missing: {path}")
		data = path.read_bytes()
	actual_sha256 = sha256_bytes(data)
	if actual_sha256 != source.sha256:
		raise RuntimeError(
			f"License SHA-256 mismatch for {source.destination}: "
			f"expected {source.sha256}, got {actual_sha256}"
		)
	return data


def synchronize(root: Path = ROOT) -> None:
	license_dir = root / "legal/distribution/licenses"
	license_dir.mkdir(parents=True, exist_ok=True)
	for source in NOTICE_SOURCES:
		data = expected_notice_bytes(source, root, allow_network=True)
		(license_dir / source.destination).write_bytes(data)
	for relative_path, data in GENERATED_FILES.items():
		path = root / relative_path
		path.parent.mkdir(parents=True, exist_ok=True)
		path.write_bytes(data)


def check(root: Path = ROOT) -> list[str]:
	issues: list[str] = []
	license_dir = root / "legal/distribution/licenses"
	expected_names = {source.destination for source in NOTICE_SOURCES} | {"STEAMWORKS_SDK_NOTICE.txt"}
	if license_dir.is_dir():
		actual_names = {path.name for path in license_dir.iterdir() if path.is_file()}
		unexpected = sorted(actual_names - expected_names)
		if unexpected:
			issues.append("Unexpected public license files: " + ", ".join(unexpected))
	for source in NOTICE_SOURCES:
		destination = license_dir / source.destination
		try:
			expected = expected_notice_bytes(source, root, allow_network=False)
		except (OSError, RuntimeError) as error:
			issues.append(str(error))
			continue
		if not destination.is_file():
			issues.append(f"Public license file is missing: {destination}")
		elif destination.read_bytes() != expected:
			issues.append(f"Public license file differs from its pinned source: {destination}")
	for relative_path, expected in GENERATED_FILES.items():
		path = root / relative_path
		if not path.is_file():
			issues.append(f"Generated legal notice is missing: {path}")
		elif path.read_bytes() != expected:
			issues.append(f"Generated legal notice is stale: {path}")
	return issues


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description=__doc__)
	mode = parser.add_mutually_exclusive_group(required=True)
	mode.add_argument("--sync", action="store_true", help="Download/copy the pinned license sources")
	mode.add_argument("--check", action="store_true", help="Verify committed notices completely offline")
	return parser


def main(argv: Sequence[str] | None = None) -> int:
	args = build_parser().parse_args(argv)
	try:
		if args.sync:
			synchronize()
		issues = check()
	except (OSError, RuntimeError, urllib.error.URLError) as error:
		print(f"Legal notice sync error: {error}", file=sys.stderr)
		return 1
	if issues:
		for issue in issues:
			print(f"ERROR: {issue}", file=sys.stderr)
		return 1
	print(f"LEGAL_NOTICES_OK files={len(NOTICE_SOURCES) + len(GENERATED_FILES)}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
