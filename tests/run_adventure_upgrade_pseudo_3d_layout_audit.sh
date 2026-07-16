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
LOG_FILE="$TMP_DIR/adventure-upgrade-pseudo-3d-layout.log"

if ! PSEUDO_3D_SCREENSHOT_DIR="$TMP_DIR" \
	"$GODOT_CMD" --path "$ROOT_DIR" --script res://tests/AdventureUpgradePseudo3DLayoutAudit.gd >"$LOG_FILE" 2>&1; then
	cat "$LOG_FILE" >&2
	exit 1
fi

if grep -Fq "SCRIPT ERROR" "$LOG_FILE" \
		|| grep -Fq "ERROR: Failed to load" "$LOG_FILE" \
		|| grep -Fq "ERROR: Cannot open" "$LOG_FILE" \
		|| ! grep -Fq "ADVENTURE_UPGRADE_PSEUDO_3D_LAYOUT_AUDIT_OK" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

for size in 1280x720 960x900 1680x720; do
	PNG_PATH="$TMP_DIR/adventure-upgrade-pseudo-3d-$size.png"
	if [[ ! -s "$PNG_PATH" ]]; then
		cat "$LOG_FILE" >&2
		echo "Missing pseudo-3D screenshot: $PNG_PATH" >&2
		exit 1
	fi
	file "$PNG_PATH"
done

cat "$LOG_FILE"
