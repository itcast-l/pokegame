#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
GODOT_CMD="${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
codex_setup_godot_test_home "$TMP_DIR"
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/DrawCardItemAvailabilitySmoke.gd 2>&1 | tee "$TMP_DIR/smoke.log"
grep -Fq "DRAW_CARD_ITEM_AVAILABILITY_SMOKE_OK" "$TMP_DIR/smoke.log"
