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
OBSERVER_LOG="$TMP_DIR/observer.log"
DROPPER_LOG="$TMP_DIR/dropper.log"
HOST_PID=""
OBSERVER_PID=""
DROPPER_PID=""

cleanup() {
	if [[ -n "$DROPPER_PID" ]] && kill -0 "$DROPPER_PID" 2>/dev/null; then
		kill "$DROPPER_PID" 2>/dev/null || true
	fi
	if [[ -n "$OBSERVER_PID" ]] && kill -0 "$OBSERVER_PID" 2>/dev/null; then
		kill "$OBSERVER_PID" 2>/dev/null || true
	fi
	if [[ -n "$HOST_PID" ]] && kill -0 "$HOST_PID" 2>/dev/null; then
		kill "$HOST_PID" 2>/dev/null || true
	fi
	rm -rf "$TMP_DIR"
}

print_logs() {
	echo "--- host log ---" >&2
	cat "$HOST_LOG" >&2 || true
	echo "--- observer log ---" >&2
	cat "$OBSERVER_LOG" >&2 || true
	echo "--- dropper log ---" >&2
	cat "$DROPPER_LOG" >&2 || true
}

require_log_line() {
	local file="$1"
	local needle="$2"
	if ! grep -Fq "$needle" "$file"; then
		echo "Missing log marker: $needle" >&2
		print_logs
		exit 1
	fi
}

reject_log_line() {
	local file="$1"
	local needle="$2"
	if grep -Fq "$needle" "$file"; then
		echo "Unexpected log marker: $needle" >&2
		print_logs
		exit 1
	fi
}

trap cleanup EXIT

"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanThreePlayerDisconnectHost.gd >"$HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanThreePlayerDisconnectObserver.gd >"$OBSERVER_LOG" 2>&1 &
OBSERVER_PID=$!
sleep 1
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanThreePlayerDisconnectDropper.gd >"$DROPPER_LOG" 2>&1 &
DROPPER_PID=$!

HOST_STATUS=0
OBSERVER_STATUS=0
DROPPER_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$OBSERVER_PID" || OBSERVER_STATUS=$?
wait "$DROPPER_PID" || DROPPER_STATUS=$?

if [[ "$HOST_STATUS" -ne 0 || "$OBSERVER_STATUS" -ne 0 || "$DROPPER_STATUS" -ne 0 ]]; then
	echo "LAN three-player disconnect smoke failed: host=$HOST_STATUS observer=$OBSERVER_STATUS dropper=$DROPPER_STATUS" >&2
	print_logs
	exit 1
fi

require_log_line "$HOST_LOG" "HOST_START_MATCH"
require_log_line "$HOST_LOG" "HOST_BOARD_INIT"
require_log_line "$HOST_LOG" "HOST_SAW_AI_SEAT_2"
require_log_line "$HOST_LOG" "HOST_DONE"
require_log_line "$OBSERVER_LOG" "OBSERVER_BOARD_INIT"
require_log_line "$OBSERVER_LOG" "OBSERVER_STILL_INGAME"
require_log_line "$OBSERVER_LOG" "OBSERVER_DONE"
require_log_line "$DROPPER_LOG" "DROPPER_BOARD_INIT"
require_log_line "$DROPPER_LOG" "DROPPER_EXIT"
reject_log_line "$OBSERVER_LOG" "OBSERVER_FAIL"
reject_log_line "$OBSERVER_LOG" "OBSERVER_STATE 8"
reject_log_line "$OBSERVER_LOG" "OBSERVER_STATE 9"
reject_log_line "$HOST_LOG" "The multiplayer instance isn't currently active."
reject_log_line "$OBSERVER_LOG" "The multiplayer instance isn't currently active."

echo "LAN_THREE_PLAYER_DISCONNECT_SMOKE_OK"
