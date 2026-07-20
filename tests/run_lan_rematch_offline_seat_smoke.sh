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
DEPARTING_LOG="$TMP_DIR/departing.log"
REPLACEMENT_LOG="$TMP_DIR/replacement.log"
HOST_PID=""
DEPARTING_PID=""
REPLACEMENT_PID=""

cleanup() {
	for pid in "$REPLACEMENT_PID" "$DEPARTING_PID" "$HOST_PID"; do
		if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
			kill "$pid" 2>/dev/null || true
		fi
	done
	rm -rf "$TMP_DIR"
}

print_logs() {
	echo "--- host log ---" >&2
	cat "$HOST_LOG" >&2 || true
	echo "--- departing client log ---" >&2
	cat "$DEPARTING_LOG" >&2 || true
	echo "--- replacement client log ---" >&2
	cat "$REPLACEMENT_LOG" >&2 || true
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

"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanRematchOfflineSeatHost.gd >"$HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanRematchOfflineSeatDepartingClient.gd >"$DEPARTING_LOG" 2>&1 &
DEPARTING_PID=$!

DEPARTING_STATUS=0
wait "$DEPARTING_PID" || DEPARTING_STATUS=$?
if [[ "$DEPARTING_STATUS" -ne 0 ]]; then
	echo "Departing rematch client failed: $DEPARTING_STATUS" >&2
	print_logs
	exit 1
fi

WAITING_FOR_REPLACEMENT=false
for _ in {1..300}; do
	if grep -Fq "HOST_WAITING_REPLACEMENT" "$HOST_LOG"; then
		WAITING_FOR_REPLACEMENT=true
		break
	fi
	if ! kill -0 "$HOST_PID" 2>/dev/null; then
		break
	fi
	sleep 0.1
done
if [[ "$WAITING_FOR_REPLACEMENT" != true ]]; then
	echo "Host did not return to lobby for replacement" >&2
	print_logs
	exit 1
fi

"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanRematchOfflineSeatReplacementClient.gd >"$REPLACEMENT_LOG" 2>&1 &
REPLACEMENT_PID=$!

HOST_STATUS=0
REPLACEMENT_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$REPLACEMENT_PID" || REPLACEMENT_STATUS=$?
if [[ "$HOST_STATUS" -ne 0 || "$REPLACEMENT_STATUS" -ne 0 ]]; then
	echo "LAN rematch offline-seat smoke failed: host=$HOST_STATUS replacement=$REPLACEMENT_STATUS" >&2
	print_logs
	exit 1
fi

require_log_line "$DEPARTING_LOG" "DEPARTING_DISCONNECT"
require_log_line "$DEPARTING_LOG" "DEPARTING_DONE"
require_log_line "$HOST_LOG" "HOST_SAW_OFFLINE_PLAYER"
require_log_line "$HOST_LOG" "HOST_OFFLINE_ROSTER_REMOVED"
require_log_line "$HOST_LOG" "HOST_REPLACEMENT_UNREADY"
require_log_line "$HOST_LOG" "HOST_UNREADY_START_REJECTED"
require_log_line "$HOST_LOG" "HOST_START_SECOND_MATCH"
require_log_line "$HOST_LOG" "HOST_SECOND_BOARD_INIT"
require_log_line "$HOST_LOG" "HOST_DONE"
require_log_line "$REPLACEMENT_LOG" "REPLACEMENT_JOINED_UNREADY"
require_log_line "$REPLACEMENT_LOG" "REPLACEMENT_READY"
require_log_line "$REPLACEMENT_LOG" "REPLACEMENT_BOARD_INIT"
require_log_line "$REPLACEMENT_LOG" "REPLACEMENT_DONE"
reject_log_line "$HOST_LOG" "HOST_FAIL"
reject_log_line "$DEPARTING_LOG" "DEPARTING_FAIL"
reject_log_line "$REPLACEMENT_LOG" "REPLACEMENT_FAIL"

echo "LAN_REMATCH_OFFLINE_SEAT_SMOKE_OK"
