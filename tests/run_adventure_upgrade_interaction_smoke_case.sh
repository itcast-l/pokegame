#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 3 ]]; then
	echo "Usage: $0 <res://script.gd> <success-marker> <log-slug>" >&2
	exit 2
fi

SCRIPT_PATH="$1"
SUCCESS_MARKER="$2"
LOG_SLUG="$3"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"

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
LOG_FILE="$TMP_DIR/$LOG_SLUG.log"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

set +e
"$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" \
	--quit-after 7200 --script "$SCRIPT_PATH" >"$LOG_FILE" 2>&1
GODOT_STATUS=$?
set -e

if [[ "$GODOT_STATUS" -ne 0 ]]; then
	cat "$LOG_FILE" >&2
	exit "$GODOT_STATUS"
fi

if grep -Eiq \
	"SCRIPT ERROR|FATAL:|handle_crash|Program crashed with signal|Abort trap|uncaught exception|mutex lock failed|RID allocations .* leaked|ObjectDB instances leaked|resources still in use|orphan StringName" \
	"$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "$SUCCESS_MARKER" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
cat "$LOG_FILE"
