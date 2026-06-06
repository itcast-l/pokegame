#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import shutil
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import numpy as np
from PIL import Image, ImageFilter


ACTION_SLUGS = ("idle", "attack", "hit", "score", "defeat", "victory")
TARGET_IDLE_FRAMES = 12
PROJECT_ROOT = Path(__file__).resolve().parents[1]
AVATAR_RESOURCE_DIR = PROJECT_ROOT / "resources" / "character_avatars"


@dataclass(frozen=True)
class AvatarDefinition:
    resource_path: Path
    avatar_id: str
    root_res: str
    root_path: Path
    idle_frame_count: int
    action_frame_count: int
    stage: int


def parse_tres_value(text: str, name: str, default: str = "") -> str:
    match = re.search(rf"^{re.escape(name)}\s*=\s*(.+)$", text, re.MULTILINE)
    if not match:
        return default
    raw = match.group(1).strip()
    if raw.startswith('"') and raw.endswith('"'):
        return raw[1:-1]
    return raw


def res_to_path(res_path: str) -> Path:
    if not res_path.startswith("res://"):
        raise ValueError(f"Unsupported resource path: {res_path}")
    return PROJECT_ROOT / res_path[len("res://") :]


def path_to_res(path: Path) -> str:
    return "res://" + str(path.resolve().relative_to(PROJECT_ROOT)).replace("\\", "/")


def load_avatar_definitions() -> list[AvatarDefinition]:
    definitions: list[AvatarDefinition] = []
    for resource_path in sorted(AVATAR_RESOURCE_DIR.glob("*.tres")):
        text = resource_path.read_text(encoding="utf-8")
        root_res = parse_tres_value(text, "RootPath")
        if not root_res:
            continue
        definitions.append(
            AvatarDefinition(
                resource_path=resource_path,
                avatar_id=parse_tres_value(text, "Id", resource_path.stem),
                root_res=root_res,
                root_path=res_to_path(root_res),
                idle_frame_count=int(parse_tres_value(text, "IdleFrameCount", "1")),
                action_frame_count=int(parse_tres_value(text, "ActionFrameCount", "4")),
                stage=int(parse_tres_value(text, "AdventureStageNumber", "0")),
            )
        )
    definitions.sort(key=lambda item: (item.stage, item.avatar_id))
    return definitions


def enemy_definitions() -> list[AvatarDefinition]:
    return [definition for definition in load_avatar_definitions() if definition.stage > 0]


def frame_path(definition: AvatarDefinition, action: str, index: int) -> Path:
    return definition.root_path / action / "processed" / f"{action}-{index}.png"


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def alpha_mask(image: Image.Image) -> np.ndarray:
    return np.asarray(image, dtype=np.uint8)[:, :, 3] > 0


def alpha_center(mask: np.ndarray) -> tuple[float, float]:
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return (0.0, 0.0)
    return (float(xs.mean()), float(ys.mean()))


def alpha_bbox(mask: np.ndarray) -> tuple[int, int, int, int]:
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return (0, 0, 0, 0)
    return (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)


def shift_mask(mask: np.ndarray, dx: int, dy: int) -> np.ndarray:
    h, w = mask.shape
    shifted = np.zeros_like(mask)
    src_x0 = max(0, -dx)
    src_y0 = max(0, -dy)
    src_x1 = min(w, w - dx)
    src_y1 = min(h, h - dy)
    dst_x0 = max(0, dx)
    dst_y0 = max(0, dy)
    dst_x1 = min(w, w + dx)
    dst_y1 = min(h, h + dy)
    if src_x0 < src_x1 and src_y0 < src_y1:
        shifted[dst_y0:dst_y1, dst_x0:dst_x1] = mask[src_y0:src_y1, src_x0:src_x1]
    return shifted


def shift_plane(plane: np.ndarray, dx: int, dy: int) -> np.ndarray:
    h, w = plane.shape
    shifted = np.zeros_like(plane)
    src_x0 = max(0, -dx)
    src_y0 = max(0, -dy)
    src_x1 = min(w, w - dx)
    src_y1 = min(h, h - dy)
    dst_x0 = max(0, dx)
    dst_y0 = max(0, dy)
    dst_x1 = min(w, w + dx)
    dst_y1 = min(h, h + dy)
    if src_x0 < src_x1 and src_y0 < src_y1:
        shifted[dst_y0:dst_y1, dst_x0:dst_x1] = plane[src_y0:src_y1, src_x0:src_x1]
    return shifted


def best_alpha_shift(reference: np.ndarray, candidate: np.ndarray, max_shift: int = 6) -> tuple[int, int, float]:
    best: tuple[int, int, float] | None = None
    ref = reference.astype(np.int16)
    for dy in range(-max_shift, max_shift + 1):
        for dx in range(-max_shift, max_shift + 1):
            shifted = shift_mask(candidate, dx, dy).astype(np.int16)
            score = float(np.mean(np.abs(ref - shifted)))
            if best is None or score < best[2]:
                best = (dx, dy, score)
    assert best is not None
    return best


def registration_plane(image: Image.Image) -> np.ndarray:
    arr = np.asarray(image, dtype=np.uint8).astype(np.float32)
    alpha = arr[:, :, 3] / 255.0
    luma = arr[:, :, 0] * 0.299 + arr[:, :, 1] * 0.587 + arr[:, :, 2] * 0.114
    return luma * alpha


def best_image_shift(reference: np.ndarray, candidate: np.ndarray, max_shift: int = 6) -> tuple[int, int, float]:
    best: tuple[int, int, float] | None = None
    for dy in range(-max_shift, max_shift + 1):
        for dx in range(-max_shift, max_shift + 1):
            shifted = shift_plane(candidate, dx, dy)
            score = float(np.mean((reference - shifted) ** 2))
            if best is None or score < best[2]:
                best = (dx, dy, score)
    assert best is not None
    return best


def resized_blur_mask(mask: np.ndarray, radius: float) -> np.ndarray:
    image = Image.fromarray(np.clip(mask * 255.0, 0, 255).astype(np.uint8), mode="L")
    blurred = image.filter(ImageFilter.GaussianBlur(radius=radius))
    return np.asarray(blurred, dtype=np.float32) / 255.0


def erode_mask(mask: np.ndarray, iterations: int = 2) -> np.ndarray:
    result = mask.copy()
    for _ in range(iterations):
        inner = result.copy()
        inner[1:, :] &= result[:-1, :]
        inner[:-1, :] &= result[1:, :]
        inner[:, 1:] &= result[:, :-1]
        inner[:, :-1] &= result[:, 1:]
        result = inner
    return result


def image_feature_masks(base: Image.Image) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    arr = np.asarray(base, dtype=np.uint8)
    rgb = arr[:, :, :3].astype(np.float32)
    alpha = arr[:, :, 3] > 0
    if not np.any(alpha):
        zero = np.zeros(alpha.shape, dtype=np.float32)
        return zero, zero, zero

    luma = rgb[:, :, 0] * 0.299 + rgb[:, :, 1] * 0.587 + rgb[:, :, 2] * 0.114
    max_rgb = rgb.max(axis=2)
    min_rgb = rgb.min(axis=2)
    saturation = np.divide(max_rgb - min_rgb, np.maximum(max_rgb, 1.0))

    ys, xs = np.indices(alpha.shape)
    x0, y0, x1, y1 = alpha_bbox(alpha)
    width = max(1, x1 - x0)
    height = max(1, y1 - y0)
    x_norm = np.clip((xs - x0) / width, 0.0, 1.0)
    y_norm = np.clip((ys - y0) / height, 0.0, 1.0)

    luma_values = luma[alpha]
    sat_values = saturation[alpha]
    bright_threshold = float(np.percentile(luma_values, 72))
    accent_threshold = float(np.percentile(sat_values, 58))

    interior = erode_mask(alpha, 2)
    highlight = interior & ((luma >= bright_threshold) | (saturation >= accent_threshold))
    highlight_weight = resized_blur_mask(highlight.astype(np.float32), 2.0)
    top_weight = resized_blur_mask((interior.astype(np.float32) * (1.0 - y_norm)), 8.0)
    diagonal_wave = interior.astype(np.float32) * (0.65 + 0.35 * x_norm) * (0.35 + 0.65 * (1.0 - y_norm))
    return highlight_weight, top_weight, diagonal_wave.astype(np.float32)


def dominant_accent(base: Image.Image) -> np.ndarray:
    arr = np.asarray(base, dtype=np.uint8)
    rgb = arr[:, :, :3].astype(np.float32)
    alpha = arr[:, :, 3] > 0
    if not np.any(alpha):
        return np.array([255.0, 255.0, 255.0], dtype=np.float32)
    max_rgb = rgb.max(axis=2)
    min_rgb = rgb.min(axis=2)
    saturation = np.divide(max_rgb - min_rgb, np.maximum(max_rgb, 1.0))
    luma = rgb[:, :, 0] * 0.299 + rgb[:, :, 1] * 0.587 + rgb[:, :, 2] * 0.114
    candidates = alpha & (saturation > np.percentile(saturation[alpha], 60)) & (luma > np.percentile(luma[alpha], 35))
    if not np.any(candidates):
        candidates = alpha
    return np.median(rgb[candidates], axis=0).astype(np.float32)


def synthesize_idle_frame(base: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    arr = np.asarray(base, dtype=np.uint8).copy()
    rgb = arr[:, :, :3].astype(np.float32)
    alpha = arr[:, :, 3].copy()
    highlight, top_weight, diagonal_wave = image_feature_masks(base)
    accent = dominant_accent(base)

    phase = 2.0 * math.pi * (frame_index - 1) / frame_count
    breathing = 0.5 + 0.5 * math.sin(phase)
    shimmer = 0.5 + 0.5 * math.sin(phase * 1.5 + 0.7)
    reverse = 0.5 + 0.5 * math.sin(phase + math.pi)

    local_weight = (
        highlight * (0.020 + 0.030 * breathing)
        + top_weight * (0.012 + 0.018 * reverse)
        + diagonal_wave * (0.010 + 0.016 * shimmer)
    )
    local_weight = np.clip(local_weight, 0.0, 0.055)[:, :, None]
    rgb = rgb * (1.0 + local_weight) + accent[None, None, :] * local_weight * 0.16
    arr[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    arr[:, :, 3] = alpha
    return Image.fromarray(arr, mode="RGBA")


def old_idle_patch_frame(base: Image.Image, old: Image.Image, frame_index: int, frame_count: int) -> Image.Image:
    base_arr = np.asarray(base, dtype=np.uint8)
    old_arr = np.asarray(old.resize(base.size, Image.Resampling.LANCZOS), dtype=np.uint8)
    out = base_arr.copy()
    base_rgb = base_arr[:, :, :3].astype(np.float32)
    old_rgb = old_arr[:, :, :3].astype(np.float32)
    base_alpha = base_arr[:, :, 3] > 0

    interior = erode_mask(base_alpha, 3)
    diff = np.abs(old_rgb - base_rgb).mean(axis=2)
    if np.any(interior):
        threshold = max(9.0, float(np.percentile(diff[interior], 78)))
    else:
        threshold = 255.0
    diff_mask = interior & (diff >= threshold)
    patch_weight = resized_blur_mask(diff_mask.astype(np.float32), 3.0)

    phase = 2.0 * math.pi * (frame_index - 1) / frame_count
    patch_strength = 0.18 + 0.16 * (0.5 + 0.5 * math.sin(phase))
    patch_weight = np.clip(patch_weight * patch_strength, 0.0, 0.36)[:, :, None]
    mixed = base_rgb * (1.0 - patch_weight) + old_rgb * patch_weight

    out[:, :, :3] = np.clip(mixed, 0, 255).astype(np.uint8)
    out[:, :, 3] = base_arr[:, :, 3]
    synthetic = np.asarray(synthesize_idle_frame(Image.fromarray(out, mode="RGBA"), frame_index, frame_count), dtype=np.uint8).copy()
    synthetic[:, :, 3] = base_arr[:, :, 3]
    return Image.fromarray(synthetic, mode="RGBA")


def stable_idle_frames(definition: AvatarDefinition) -> list[Image.Image]:
    base_path = frame_path(definition, "idle", 1)
    base = load_rgba(base_path)
    old_frames = [frame_path(definition, "idle", index) for index in range(1, TARGET_IDLE_FRAMES + 1)]
    has_old_loop = definition.action_frame_count == TARGET_IDLE_FRAMES and all(path.exists() for path in old_frames)
    frames = [base]
    for index in range(2, TARGET_IDLE_FRAMES + 1):
        if has_old_loop:
            frames.append(old_idle_patch_frame(base, load_rgba(old_frames[index - 1]), index, TARGET_IDLE_FRAMES))
        else:
            frames.append(synthesize_idle_frame(base, index, TARGET_IDLE_FRAMES))
    return frames


def deterministic_uid(res_path: str) -> str:
    alphabet = "abcdefghijklmnopqrstuvwxyz0123456789"
    digest = hashlib.sha1(("avatar-idle:" + res_path).encode("utf-8")).digest()
    value = int.from_bytes(digest, "big")
    chars = []
    for _ in range(13):
        value, remainder = divmod(value, len(alphabet))
        chars.append(alphabet[remainder])
    return "uid://" + "".join(chars)


def import_hash(res_path: str) -> str:
    return hashlib.md5(res_path.encode("utf-8")).hexdigest()


def write_png_import(png_path: Path, template_path: Path) -> None:
    import_path = png_path.with_suffix(png_path.suffix + ".import")
    if import_path.exists():
        return
    res_path = path_to_res(png_path)
    template = template_path.read_text(encoding="utf-8")
    dest_hash = import_hash(res_path)
    dest_file = f'res://.godot/imported/{png_path.name}-{dest_hash}.ctex'

    template = re.sub(r'uid="uid://[^"]+"', f'uid="{deterministic_uid(res_path)}"', template)
    template = re.sub(r'path="res://\.godot/imported/[^"]+\.ctex"', f'path="{dest_file}"', template)
    template = re.sub(r'source_file="res://[^"]+"', f'source_file="{res_path}"', template)
    template = re.sub(r'dest_files=\["res://\.godot/imported/[^"]+\.ctex"\]', f'dest_files=["{dest_file}"]', template)
    import_path.write_text(template, encoding="utf-8")


def update_idle_frame_count(definition: AvatarDefinition, target_count: int = TARGET_IDLE_FRAMES) -> bool:
    text = definition.resource_path.read_text(encoding="utf-8")
    updated = re.sub(r"^IdleFrameCount\s*=\s*\d+$", f"IdleFrameCount = {target_count}", text, flags=re.MULTILINE)
    if updated == text:
        return False
    definition.resource_path.write_text(updated, encoding="utf-8")
    return True


def backup_paths(definitions: Iterable[AvatarDefinition], backup_dir: Path) -> None:
    backup_dir.mkdir(parents=True, exist_ok=True)
    manifest: dict[str, list[str] | str] = {
        "created_at": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
        "project_root": str(PROJECT_ROOT),
        "files": [],
    }
    files: set[Path] = set()
    for definition in definitions:
        files.add(definition.resource_path)
        idle_dir = definition.root_path / "idle" / "processed"
        if idle_dir.exists():
            files.update(path for path in idle_dir.iterdir() if path.is_file())
    for path in sorted(files):
        rel = path.relative_to(PROJECT_ROOT)
        target = backup_dir / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            shutil.copy2(path, target)
        manifest["files"].append(str(rel))
    (backup_dir / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")


def rebuild_idle(definitions: list[AvatarDefinition], backup_dir: Path | None) -> list[dict[str, object]]:
    if backup_dir is not None:
        backup_paths(definitions, backup_dir)

    results: list[dict[str, object]] = []
    for definition in definitions:
        frames = stable_idle_frames(definition)
        template_import = frame_path(definition, "idle", 1).with_suffix(".png.import")
        for index, image in enumerate(frames, start=1):
            output_path = frame_path(definition, "idle", index)
            output_path.parent.mkdir(parents=True, exist_ok=True)
            if index > 1 or not output_path.exists():
                image.save(output_path)
            write_png_import(output_path, template_import)
        changed_resource = update_idle_frame_count(definition)
        results.append(
            {
                "stage": definition.stage,
                "id": definition.avatar_id,
                "root": definition.root_res,
                "idle_frames": TARGET_IDLE_FRAMES,
                "resource_updated": changed_resource,
            }
        )
    return results


def existing_frame_indices(definition: AvatarDefinition, action: str) -> list[int]:
    processed_dir = definition.root_path / action / "processed"
    if not processed_dir.exists():
        return []
    pattern = re.compile(rf"^{re.escape(action)}-(\d+)\.png$")
    indices = []
    for path in processed_dir.iterdir():
        match = pattern.match(path.name)
        if match:
            indices.append(int(match.group(1)))
    return sorted(indices)


def qc_action(definition: AvatarDefinition, action: str) -> dict[str, object]:
    indices = existing_frame_indices(definition, action)
    expected = TARGET_IDLE_FRAMES if action == "idle" else definition.action_frame_count
    result: dict[str, object] = {
        "stage": definition.stage,
        "id": definition.avatar_id,
        "action": action,
        "expected": expected,
        "found": len(indices),
        "missing": [index for index in range(1, expected + 1) if index not in indices],
        "frames": [],
    }
    if not indices:
        return result

    ref_image = load_rgba(frame_path(definition, action, indices[0]))
    ref_mask = alpha_mask(ref_image)
    ref_plane = registration_plane(ref_image)
    for index in indices:
        path = frame_path(definition, action, index)
        image = load_rgba(path)
        mask = alpha_mask(image)
        plane = registration_plane(image)
        center = alpha_center(mask)
        bbox = alpha_bbox(mask)
        dx, dy, score = best_alpha_shift(ref_mask, mask)
        image_dx, image_dy, image_score = best_image_shift(ref_plane, plane)
        result["frames"].append(
            {
                "index": index,
                "size": list(image.size),
                "alpha_center": [round(center[0], 3), round(center[1], 3)],
                "bbox": list(bbox),
                "alpha_shift": [dx, dy],
                "alpha_shift_score": round(score, 6),
                "image_shift": [image_dx, image_dy],
                "image_shift_score": round(image_score, 6),
            }
        )
    return result


def qc(definitions: list[AvatarDefinition], actions: Iterable[str]) -> list[dict[str, object]]:
    return [qc_action(definition, action) for definition in definitions for action in actions]


def print_list(definitions: list[AvatarDefinition]) -> None:
    for definition in definitions:
        print(
            f"{definition.stage:02d} {definition.avatar_id} "
            f"idle={definition.idle_frame_count} action={definition.action_frame_count} {definition.root_res}"
        )


def main() -> int:
    parser = argparse.ArgumentParser(description="Rebuild enemy avatar idle loops with stable alpha boundaries.")
    parser.add_argument("--all-avatars", action="store_true", help="Include stage 0 avatars as well.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    subparsers.add_parser("list", help="List avatars in processing order.")

    backup_parser = subparsers.add_parser("backup", help="Back up resources that rebuild modifies.")
    backup_parser.add_argument("--backup-dir", type=Path, required=True)

    rebuild_parser = subparsers.add_parser("rebuild", help="Generate stable 12-frame idle loops.")
    rebuild_parser.add_argument("--backup-dir", type=Path)
    rebuild_parser.add_argument("--report", type=Path)

    qc_parser = subparsers.add_parser("qc", help="Write frame QC JSON.")
    qc_parser.add_argument("--actions", default="idle", help="Comma-separated actions to inspect.")
    qc_parser.add_argument("--output", type=Path, required=True)

    args = parser.parse_args()
    definitions = load_avatar_definitions() if args.all_avatars else enemy_definitions()

    if args.command == "list":
        print_list(definitions)
        return 0

    if args.command == "backup":
        backup_paths(definitions, args.backup_dir)
        print(args.backup_dir)
        return 0

    if args.command == "rebuild":
        report = rebuild_idle(definitions, args.backup_dir)
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
        print(json.dumps(report, indent=2, ensure_ascii=False))
        return 0

    if args.command == "qc":
        actions = [action.strip() for action in args.actions.split(",") if action.strip()]
        unknown = [action for action in actions if action not in ACTION_SLUGS]
        if unknown:
            raise ValueError(f"Unknown action(s): {', '.join(unknown)}")
        report = qc(definitions, actions)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
        print(args.output)
        return 0

    raise AssertionError(args.command)


if __name__ == "__main__":
    raise SystemExit(main())
