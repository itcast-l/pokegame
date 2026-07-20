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
LOG_FILE="$TMP_DIR/game-board-async-lifetime-shutdown.log"

if ! "$GODOT_CMD" --headless --verbose --path "$ROOT_DIR" --script res://tests/GameBoardAsyncLifetimeShutdownSmoke.gd >"$LOG_FILE" 2>&1; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "GAME_BOARD_ASYNC_LIFETIME_SHUTDOWN_SMOKE_OK" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
if grep -Eiq "SCRIPT ERROR|ObjectDB instances leaked|orphan StringName|unobserved task|TaskScheduler.UnobservedTaskException|Board async task failed|Network visual playback failed" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
