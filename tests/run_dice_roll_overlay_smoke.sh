#!/usr/bin/env bash
set -euo pipefail

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
trap 'rm -rf "$TMP_DIR"' EXIT
LOG_FILE="$TMP_DIR/dice-roll-overlay.log"
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/DiceRollOverlaySmoke.gd >"$LOG_FILE" 2>&1

if ! grep -Fq "DICE_ROLL_OVERLAY_SMOKE_OK" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

echo "DICE_ROLL_OVERLAY_SMOKE_OK"
