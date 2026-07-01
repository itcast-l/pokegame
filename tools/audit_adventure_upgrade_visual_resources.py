from __future__ import annotations

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
POPUP_SCENE = ROOT / "scenes" / "upgrade_choice_popup_asset_scene.tscn"
SCAN_ROOTS = (
    ROOT / "scenes",
    ROOT / "resources",
)
FILE_PATTERNS = ("*.tscn", "*.tres")
MAX_SCENE_BYTES = 1_000_000
MAX_PACKED_BYTE_ARRAYS_PER_FILE = 0
WHITELISTED_LARGE_FILES: dict[Path, str] = {}


def count_embedded_payloads(text: str) -> tuple[int, int, int]:
    return (
        text.count('[sub_resource type="Image"'),
        text.count('[sub_resource type="ImageTexture"'),
        text.count("PackedByteArray("),
    )


def iter_owned_resource_files() -> list[Path]:
    files: list[Path] = []
    for scan_root in SCAN_ROOTS:
        if not scan_root.exists():
            continue
        for pattern in FILE_PATTERNS:
            for path in scan_root.rglob(pattern):
                if "addons" in path.parts:
                    continue
                files.append(path)
    return sorted(set(files))


def audit_popup_scene(failures: list[str]) -> None:
    text = POPUP_SCENE.read_text(errors="ignore")
    embedded_images, embedded_image_textures, packed_byte_arrays = count_embedded_payloads(text)

    if embedded_images == 0 and embedded_image_textures == 0 and packed_byte_arrays == 0:
        print("ADVENTURE_UPGRADE_VISUAL_AUDIT_OK popup scene has no embedded image payloads")
        return

    failures.append(
        "popup scene contains embedded image payloads: "
        f"images={embedded_images} image_textures={embedded_image_textures} packed_arrays={packed_byte_arrays}"
    )


def audit_repo_owned_resources(failures: list[str]) -> None:
    for path in iter_owned_resource_files():
        text = path.read_text(errors="ignore")
        embedded_images, embedded_image_textures, packed_byte_arrays = count_embedded_payloads(text)
        relative_path = path.relative_to(ROOT)

        if embedded_images > 0:
            failures.append(f"{relative_path} embeds Image subresources ({embedded_images})")
        if embedded_image_textures > 0:
            failures.append(f"{relative_path} embeds ImageTexture subresources ({embedded_image_textures})")
        if packed_byte_arrays > MAX_PACKED_BYTE_ARRAYS_PER_FILE:
            failures.append(f"{relative_path} serializes PackedByteArray payloads ({packed_byte_arrays})")

        if path.suffix == ".tscn":
            size = path.stat().st_size
            if size > MAX_SCENE_BYTES and path not in WHITELISTED_LARGE_FILES:
                failures.append(
                    f"{relative_path} is oversized at {size} bytes "
                    f"(threshold {MAX_SCENE_BYTES} bytes)"
                )


def main() -> int:
    failures: list[str] = []
    audit_popup_scene(failures)
    audit_repo_owned_resources(failures)

    if failures:
        for failure in failures:
            print(f"ADVENTURE_UPGRADE_VISUAL_AUDIT_FAIL {failure}")
        return 1

    print("ADVENTURE_UPGRADE_VISUAL_AUDIT_OK repo-owned scenes/resources passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
