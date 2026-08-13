#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
GODOT_CMD="${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
codex_setup_godot_test_home "$TMP_DIR"
LOG_FILE="$TMP_DIR/adventure-stage-unlock.log"
if ! "$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" \
		--script res://tests/AdventureStageUnlockSmoke.gd >"$LOG_FILE" 2>&1; then
	cat "$LOG_FILE" >&2
	exit 1
fi

if grep -Eq "SCRIPT ERROR|^ERROR:|FATAL:|ObjectDB instances leaked|resources still in use|RID allocations .* leaked" "$LOG_FILE" \
		|| ! grep -Fq "ADVENTURE_STAGE_UNLOCK_SMOKE_OK" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
