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
LOG_FILE="$TMP_DIR/localization-screenshot-audit.log"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
SCREENSHOT_DIR="${CODEX_LOCALIZATION_SCREENSHOT_DIR:-$ROOT_DIR/outputs/localization_screenshots/$TIMESTAMP}"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

GODOT_ARGS=(--path "$ROOT_DIR" --script res://tests/LocalizationScreenshotAudit.gd)
if [[ "${CODEX_LOCALIZATION_SCREENSHOT_HEADLESS:-0}" != "0" ]]; then
	GODOT_ARGS=(--headless "${GODOT_ARGS[@]}")
fi

set +e
CODEX_LOCALIZATION_SCREENSHOT_DIR="$SCREENSHOT_DIR" \
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

if ! grep -Fq "LOCALIZATION_SCREENSHOT_AUDIT_OK" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
