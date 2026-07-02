#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"

if [[ $# -ne 2 ]]; then
	echo "Usage: $0 <godot-script-path> <success-marker>" >&2
	exit 1
fi

SCRIPT_PATH="$1"
SUCCESS_MARKER="$2"

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
trap 'rm -rf "$TMP_DIR"' EXIT

LOG_FILE="$TMP_DIR/verbose-smoke.log"
"$GODOT_CMD" --headless --verbose --path "$ROOT_DIR" --script "$SCRIPT_PATH" >"$LOG_FILE" 2>&1

grep -Fq "$SUCCESS_MARKER" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }

if grep -Eq \
	"ObjectDB instances leaked|Leaked instance:|resources still in use|Resource still in use:" \
	"$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
