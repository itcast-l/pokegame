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
LOG_FILE="$TMP_DIR/adventure-upgrade-clubs-miss-climb-badge.log"
SCREENSHOT_DIR="${CODEX_MISS_CLIMB_BADGE_SCREENSHOT_DIR:-$ROOT_DIR/outputs/adventure_upgrade_clubs_miss_climb_badge_smoke}"
mkdir -p "$SCREENSHOT_DIR"
export CODEX_MISS_CLIMB_BADGE_SCREENSHOT_DIR="$SCREENSHOT_DIR"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

GODOT_ARGS=(--path "$ROOT_DIR" --script res://tests/AdventureUpgradeClubsMissClimbBadgeSmoke.gd)
if [[ "${CODEX_MISS_CLIMB_BADGE_HEADLESS:-0}" != "0" ]]; then
	GODOT_ARGS=(--headless "${GODOT_ARGS[@]}")
	export CODEX_MISS_CLIMB_BADGE_CAPTURE_SCREENSHOTS=0
fi

set +e
"$GODOT_CMD" "${GODOT_ARGS[@]}" >"$LOG_FILE" 2>&1
GODOT_STATUS=$?
set -e

if [[ "$GODOT_STATUS" -ne 0 ]]; then
	cat "$LOG_FILE" >&2
	exit "$GODOT_STATUS"
fi

if grep -Fq "SCRIPT ERROR" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "ADVENTURE_UPGRADE_CLUBS_MISS_CLIMB_BADGE_SMOKE_OK" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }

cat "$LOG_FILE"
