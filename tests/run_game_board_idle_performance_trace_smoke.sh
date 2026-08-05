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
LOG_FILE="$TMP_DIR/game_board_idle_performance_trace.log"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

set +e
DECKLAND_TEST_MUTE_AUDIO=1 "$GODOT_CMD" \
	--headless \
	--audio-driver Dummy \
	--path "$ROOT_DIR" \
	--script res://tests/GameBoardIdlePerformanceTraceSmoke.gd >"$LOG_FILE" 2>&1
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

if grep -Eiq "ObjectDB instances leaked|resources still in use|orphan StringName|unobserved task|TaskScheduler.UnobservedTaskException" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "IDLE_WINDOW_BEGIN" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "IDLE_WINDOW_END" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "GAME_BOARD_IDLE_PERFORMANCE_TRACE_SMOKE_OK" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }

IDLE_WINDOW_LOG="$(awk '
	/IDLE_WINDOW_BEGIN/ { in_window = 1; next }
	/IDLE_WINDOW_END/ { in_window = 0; next }
	in_window { print }
' "$LOG_FILE")"

if printf '%s\n' "$IDLE_WINDOW_LOG" | grep -Fq "[Perf][Render] RenderPlayerHands"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

if printf '%s\n' "$IDLE_WINDOW_LOG" | grep -Fq "[Perf][Render] RenderTableCards"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
