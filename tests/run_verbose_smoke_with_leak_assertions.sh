#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"

if [[ $# -lt 2 || $# -gt 3 ]]; then
	echo "Usage: $0 <godot-script-path> <success-marker> [expected-adventure-upgrade-acquisitions]" >&2
	exit 1
fi

SCRIPT_PATH="$1"
SUCCESS_MARKER="$2"
EXPECTED_ADVENTURE_UPGRADE_ACQUISITIONS="${3:-}"

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
if ! "$GODOT_CMD" --headless --verbose --path "$ROOT_DIR" --script "$SCRIPT_PATH" >"$LOG_FILE" 2>&1; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "$SUCCESS_MARKER" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }

if [[ -n "$EXPECTED_ADVENTURE_UPGRADE_ACQUISITIONS" ]]; then
	ACQUIRED_COUNT="$(grep -Fc "Adventure upgrade acquired:" "$LOG_FILE" || true)"
	if [[ "$ACQUIRED_COUNT" -ne "$EXPECTED_ADVENTURE_UPGRADE_ACQUISITIONS" ]]; then
		echo "Expected $EXPECTED_ADVENTURE_UPGRADE_ACQUISITIONS adventure upgrade acquisition log lines, found $ACQUIRED_COUNT." >&2
		cat "$LOG_FILE" >&2
		exit 1
	fi
fi

if grep -Eq \
	"ObjectDB instances leaked|Leaked instance:|resources still in use|Resource still in use:" \
	"$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
