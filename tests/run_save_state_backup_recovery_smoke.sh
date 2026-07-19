#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"
GODOT_CMD="${GODOT_BIN:-$DEFAULT_GODOT_BIN}"

TMP_DIR="$(mktemp -d)"
codex_setup_godot_test_home "$TMP_DIR"
LOG_FILE="$TMP_DIR/save-state-backup-recovery.log"
trap 'rm -rf "$TMP_DIR"' EXIT

if ! "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/SaveStateBackupRecoverySmoke.gd >"$LOG_FILE" 2>&1; then
	cat "$LOG_FILE" >&2
	exit 1
fi
if grep -Fq "SCRIPT ERROR" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi
grep -Fq "SAVE_STATE_BACKUP_RECOVERY_SMOKE_OK" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
cat "$LOG_FILE"
