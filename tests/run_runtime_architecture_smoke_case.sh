#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"
SCRIPT_PATH="${1:?Missing Godot script path}"
SUCCESS_MARKER="${2:?Missing success marker}"

if [[ -n "${GODOT_BIN:-}" ]]; then
	GODOT_CMD="$GODOT_BIN"
elif command -v Godot >/dev/null 2>&1; then
	GODOT_CMD="$(command -v Godot)"
elif [[ -x "$DEFAULT_GODOT_BIN" ]]; then
	GODOT_CMD="$DEFAULT_GODOT_BIN"
else
	echo "Missing Godot binary. Set GODOT_BIN." >&2
	exit 1
fi

TMP_DIR="$(mktemp -d)"
codex_setup_godot_test_home "$TMP_DIR"
LOG_FILE="$TMP_DIR/runtime_architecture_smoke.log"
trap 'rm -rf "$TMP_DIR"' EXIT

status=0
DECKLAND_TEST_MUTE_AUDIO=1 "$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" --script "$SCRIPT_PATH" >"$LOG_FILE" 2>&1 || status=$?
if [[ "$status" -ne 0 ]] || grep -Eiq "^ERROR:|SCRIPT ERROR|Unhandled exception|ObjectDB instances leaked|orphan StringName" "$LOG_FILE" || ! grep -Fq "$SUCCESS_MARKER" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi
cat "$LOG_FILE"
