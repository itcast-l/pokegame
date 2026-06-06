#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import shutil
import tarfile
import time
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BACKUP_TAR = Path(
    "/private/tmp/pokegame_avatar_rebuild_artifacts/backups/"
    "project_tmp_avatar_rebuild_backups/pre_rebuild_from_git/"
    "original_enemy_avatar_idle.tar"
)
DEFAULT_SAFETY_DIR = Path("/private/tmp/pokegame_avatar_rebuild_artifacts/rollback_safety")


def parse_tres_value(text: str, name: str, default: str = "") -> str:
    match = re.search(rf"^{re.escape(name)}\s*=\s*(.+)$", text, re.MULTILINE)
    if not match:
        return default
    raw = match.group(1).strip()
    if raw.startswith('"') and raw.endswith('"'):
        return raw[1:-1]
    return raw


def normalize_archive_path(name: str) -> str:
    return name[2:] if name.startswith("./") else name


def archive_text(tar: tarfile.TarFile, name: str) -> str:
    member = tar.getmember(name)
    file_obj = tar.extractfile(member)
    if file_obj is None:
        raise ValueError(f"Cannot read archive member: {name}")
    return file_obj.read().decode("utf-8")


def enemy_restore_set(backup_tar: Path) -> tuple[set[str], set[Path]]:
    with tarfile.open(backup_tar, "r") as tar:
        names = {normalize_archive_path(member.name) for member in tar.getmembers() if member.isfile()}
        member_by_name = {normalize_archive_path(member.name): member.name for member in tar.getmembers()}
        resource_names = sorted(
            name for name in names if name.startswith("resources/character_avatars/") and name.endswith(".tres")
        )

        restore_names: set[str] = set()
        idle_dirs: set[Path] = set()
        for resource_name in resource_names:
            text = archive_text(tar, member_by_name[resource_name])
            stage = int(parse_tres_value(text, "AdventureStageNumber", "0"))
            if stage <= 0:
                continue
            root_res = parse_tres_value(text, "RootPath")
            if not root_res.startswith("res://"):
                raise ValueError(f"Unsupported RootPath in {resource_name}: {root_res}")
            root = root_res.removeprefix("res://")
            idle_dir = Path(root) / "idle" / "processed"
            idle_dirs.add(idle_dir)
            restore_names.add(resource_name)
            restore_names.update(
                name
                for name in names
                if name.startswith(str(idle_dir).replace("\\", "/") + "/")
            )
        return restore_names, idle_dirs


def current_generated_files(restore_names: set[str], idle_dirs: set[Path]) -> set[str]:
    generated: set[str] = set()
    pattern = re.compile(r"^idle-\d+\.png(?:\.import)?$")
    for idle_dir in idle_dirs:
        current_dir = PROJECT_ROOT / idle_dir
        if not current_dir.exists():
            continue
        for path in current_dir.iterdir():
            if path.is_file() and pattern.match(path.name):
                rel = str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")
                if rel not in restore_names:
                    generated.add(rel)
    return generated


def backup_current(paths: set[str], safety_dir: Path) -> None:
    safety_dir.mkdir(parents=True, exist_ok=True)
    for rel in sorted(paths):
        src = PROJECT_ROOT / rel
        if not src.exists():
            continue
        dst = safety_dir / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)


def restore_archive_members(backup_tar: Path, restore_names: set[str]) -> None:
    with tarfile.open(backup_tar, "r") as tar:
        member_by_name = {normalize_archive_path(member.name): member for member in tar.getmembers()}
        for rel in sorted(restore_names):
            member = member_by_name.get(rel)
            if member is None:
                raise ValueError(f"Missing archive member: {rel}")
            src = tar.extractfile(member)
            if src is None:
                continue
            dst = PROJECT_ROOT / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            with dst.open("wb") as out:
                shutil.copyfileobj(src, out)


def main() -> int:
    parser = argparse.ArgumentParser(description="Restore enemy idle avatars from the pre-rebuild backup tar.")
    parser.add_argument("--backup-tar", type=Path, default=DEFAULT_BACKUP_TAR)
    parser.add_argument("--apply", action="store_true", help="Apply the restore. Without this, only prints a dry-run.")
    parser.add_argument("--safety-dir", type=Path, default=DEFAULT_SAFETY_DIR)
    args = parser.parse_args()

    if not args.backup_tar.exists():
        raise FileNotFoundError(args.backup_tar)

    restore_names, idle_dirs = enemy_restore_set(args.backup_tar)
    generated = current_generated_files(restore_names, idle_dirs)
    touched = restore_names | generated

    print(f"backup_tar: {args.backup_tar}")
    print(f"restore_files: {len(restore_names)}")
    print(f"delete_generated_files: {len(generated)}")
    print(f"mode: {'apply' if args.apply else 'dry-run'}")

    if not args.apply:
        for rel in sorted(list(generated))[:40]:
            print(f"would delete: {rel}")
        if len(generated) > 40:
            print(f"would delete: ... {len(generated) - 40} more")
        return 0

    safety_dir = args.safety_dir / time.strftime("%Y%m%d_%H%M%S")
    backup_current(touched, safety_dir)
    for rel in sorted(generated):
        path = PROJECT_ROOT / rel
        if path.exists():
            path.unlink()
    restore_archive_members(args.backup_tar, restore_names)
    print(f"safety_backup: {safety_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
