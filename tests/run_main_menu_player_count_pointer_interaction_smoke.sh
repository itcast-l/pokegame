#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
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

LOG_FILE="$(mktemp -t main_menu_player_count_pointer_interaction_smoke)"

cleanup() {
	rm -f "$LOG_FILE"
}

trap cleanup EXIT

set +e
"$GODOT_CMD" --path "$ROOT_DIR" --script res://tests/MainMenuPlayerCountPointerInteractionSmoke.gd >"$LOG_FILE" 2>&1
GODOT_STATUS=$?
set -e

if [[ "$GODOT_STATUS" -ne 0 ]]; then
	cat "$LOG_FILE" >&2
	exit "$GODOT_STATUS"
fi

if grep -Eq "SCRIPT ERROR|Failed to load resource|No loader found|MAIN_MENU_PLAYER_COUNT_POINTER_INTERACTION_SMOKE_FAIL" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "MAIN_MENU_PLAYER_COUNT_POINTER_INTERACTION_SMOKE_OK" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
cat "$LOG_FILE"
