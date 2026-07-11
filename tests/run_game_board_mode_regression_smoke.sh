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
LOG_FILE="$TMP_DIR/mode.log"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

for mode in solo_low solo_high solo_high_four_player adventure; do
	MODE_TMP_DIR="$TMP_DIR/$mode"
	codex_setup_godot_test_home "$MODE_TMP_DIR"
	if ! "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/GameBoardModeRegressionSmoke.gd -- --mode="$mode" >>"$LOG_FILE" 2>&1; then
		cat "$LOG_FILE" >&2
		exit 1
	fi
done

if grep -Fq "SCRIPT ERROR" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "SOLO_LOW_PLAYED_CARD" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "SOLO_LOW_SAW_OPPONENT_TURN" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "MODE_OK solo_low" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "SOLO_HIGH_PLAYED_CARD" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "SOLO_HIGH_SAW_OPPONENT_TURN" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "MODE_OK solo_high" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "SOLO_HIGH_FOUR_PLAYER_PLAYED_CARD" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "SOLO_HIGH_FOUR_PLAYER_SAW_OPPONENT_TURN" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "MODE_OK solo_high_four_player" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "ADVENTURE_PLAYED_CARD" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "ADVENTURE_SAW_OPPONENT_TURN" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "MODE_OK adventure" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
grep -Fq "GAME_BOARD_MODE_REGRESSION_SMOKE_OK" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }

cat "$LOG_FILE"
