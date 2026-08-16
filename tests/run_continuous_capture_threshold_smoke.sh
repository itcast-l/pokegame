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

run_godot() {
	DECKLAND_TEST_MUTE_AUDIO=1 "$GODOT_CMD" "$@" \
		--audio-driver Dummy \
		--path "$ROOT_DIR" \
		--script res://tests/ContinuousCaptureThresholdSmoke.gd
}

if [[ -n "${DECKLAND_CAPTURE_THRESHOLD_SCREENSHOT_DIR:-}" ]]; then
	run_godot 2>&1 | tee "$TMP_DIR/continuous_capture_threshold.log"
else
	run_godot --headless 2>&1 | tee "$TMP_DIR/continuous_capture_threshold.log"
fi
grep -Fq "CONTINUOUS_CAPTURE_THRESHOLD_SMOKE_OK" "$TMP_DIR/continuous_capture_threshold.log"
