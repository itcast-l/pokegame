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
HOST_LOG="$TMP_DIR/host.log"
CLIENT_ONE_LOG="$TMP_DIR/client-one.log"
CLIENT_TWO_LOG="$TMP_DIR/client-two.log"
HOST_PID=""
CLIENT_ONE_PID=""
CLIENT_TWO_PID=""

cleanup() {
	for pid in "$CLIENT_TWO_PID" "$CLIENT_ONE_PID" "$HOST_PID"; do
		if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
			kill "$pid" 2>/dev/null || true
		fi
	done
	rm -rf "$TMP_DIR"
}

print_logs() {
	for file in "$HOST_LOG" "$CLIENT_ONE_LOG" "$CLIENT_TWO_LOG"; do
		echo "--- $file ---" >&2
		cat "$file" >&2 || true
	done
}

require_marker() {
	local file="$1"
	local marker="$2"
	if ! grep -Fq "$marker" "$file"; then
		echo "Missing marker: $marker" >&2
		print_logs
		exit 1
	fi
}

trap cleanup EXIT

CODEX_OPENING_DICE_ROLL_RESULT=2 DECKLAND_TEST_MUTE_AUDIO=1 \
CODEX_TEST_STABLE_USER_ID="starting-dice-seat-0" \
"$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" \
	--script res://tests/LanStartingPlayerDiceHost.gd >"$HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
CODEX_OPENING_DICE_ROLL_RESULT=2 DECKLAND_TEST_MUTE_AUDIO=1 \
CODEX_TEST_STABLE_USER_ID="starting-dice-seat-1" \
"$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" \
	--script res://tests/LanStartingPlayerDiceClient.gd -- --expected-seat=1 >"$CLIENT_ONE_LOG" 2>&1 &
CLIENT_ONE_PID=$!
sleep 0.5
CODEX_OPENING_DICE_ROLL_RESULT=2 DECKLAND_TEST_MUTE_AUDIO=1 \
CODEX_TEST_STABLE_USER_ID="starting-dice-seat-2" \
"$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" \
	--script res://tests/LanStartingPlayerDiceClient.gd -- --expected-seat=2 >"$CLIENT_TWO_LOG" 2>&1 &
CLIENT_TWO_PID=$!

HOST_STATUS=0
CLIENT_ONE_STATUS=0
CLIENT_TWO_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$CLIENT_ONE_PID" || CLIENT_ONE_STATUS=$?
wait "$CLIENT_TWO_PID" || CLIENT_TWO_STATUS=$?
if [[ "$HOST_STATUS" -ne 0 || "$CLIENT_ONE_STATUS" -ne 0 || "$CLIENT_TWO_STATUS" -ne 0 ]]; then
	echo "LAN starting-player dice smoke failed: host=$HOST_STATUS client1=$CLIENT_ONE_STATUS client2=$CLIENT_TWO_STATUS" >&2
	print_logs
	exit 1
fi

require_marker "$HOST_LOG" "LAN_STARTING_DICE_HOST_DURING roll=2 turn=2"
require_marker "$HOST_LOG" "LAN_STARTING_DICE_HOST_AFTER roll=2 turn=2"
require_marker "$HOST_LOG" "LAN_STARTING_DICE_HOST_DONE"
require_marker "$CLIENT_ONE_LOG" "LAN_STARTING_DICE_CLIENT_1_DURING roll=2 turn=2"
require_marker "$CLIENT_ONE_LOG" "LAN_STARTING_DICE_CLIENT_1_AFTER roll=2 turn=2 can_act=false"
require_marker "$CLIENT_ONE_LOG" "LAN_STARTING_DICE_CLIENT_1_DONE"
require_marker "$CLIENT_TWO_LOG" "LAN_STARTING_DICE_CLIENT_2_DURING roll=2 turn=2"
require_marker "$CLIENT_TWO_LOG" "LAN_STARTING_DICE_CLIENT_2_AFTER roll=2 turn=2 can_act=true"
require_marker "$CLIENT_TWO_LOG" "LAN_STARTING_DICE_CLIENT_2_DONE"

if grep -Fq "SCRIPT ERROR" "$HOST_LOG" \
		|| grep -Fq "SCRIPT ERROR" "$CLIENT_ONE_LOG" \
		|| grep -Fq "SCRIPT ERROR" "$CLIENT_TWO_LOG" \
		|| grep -Fq "ERROR: System." "$HOST_LOG" \
		|| grep -Fq "ERROR: System." "$CLIENT_ONE_LOG" \
		|| grep -Fq "ERROR: System." "$CLIENT_TWO_LOG"; then
	print_logs
	exit 1
fi

echo "LAN_STARTING_PLAYER_DICE_SMOKE_OK"
