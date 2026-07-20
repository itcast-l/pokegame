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
CLIENT_1_LOG="$TMP_DIR/client_1.log"
CLIENT_2_LOG="$TMP_DIR/client_2.log"
PIDS=()

cleanup() {
	for pid in "${PIDS[@]:-}"; do
		if kill -0 "$pid" 2>/dev/null; then
			kill "$pid" 2>/dev/null || true
		fi
	done
	rm -rf "$TMP_DIR"
}

print_logs() {
	for log in "$HOST_LOG" "$CLIENT_1_LOG" "$CLIENT_2_LOG"; do
		echo "--- $log ---" >&2
		cat "$log" >&2 || true
	done
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

"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanInactiveHumanTurnSkipHost.gd >"$HOST_LOG" 2>&1 &
PIDS+=("$!")
sleep 1
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanInactiveHumanTurnSkipClient.gd -- --expected-seat=1 >"$CLIENT_1_LOG" 2>&1 &
PIDS+=("$!")
sleep 0.5
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanInactiveHumanTurnSkipClient.gd -- --expected-seat=2 >"$CLIENT_2_LOG" 2>&1 &
PIDS+=("$!")

STATUS=0
for pid in "${PIDS[@]}"; do
	wait "$pid" || STATUS=$?
done

if [[ "$STATUS" -ne 0 ]]; then
	echo "LAN inactive-human-turn-skip smoke failed: status=$STATUS" >&2
	print_logs
	exit 1
fi

require_log_line "$HOST_LOG" "INACTIVE_SKIP_HOST_START_MATCH"
require_log_line "$HOST_LOG" "INACTIVE_SKIP_HOST_FIXTURE inactive=2"
require_log_line "$HOST_LOG" "INACTIVE_SKIP_HOST_PLAYED_CARD"
require_log_line "$HOST_LOG" "INACTIVE_SKIP_HOST_SKIPPED_TO_1"
require_log_line "$HOST_LOG" "INACTIVE_SKIP_HOST_DONE"
for seat in 1 2; do
	client_log="$TMP_DIR/client_${seat}.log"
	require_log_line "$client_log" "INACTIVE_SKIP_CLIENT_${seat}_BOARD_INIT"
	require_log_line "$client_log" "INACTIVE_SKIP_CLIENT_${seat}_SKIPPED_TO_1"
	require_log_line "$client_log" "INACTIVE_SKIP_CLIENT_${seat}_DONE"
	reject_log_line "$client_log" "INACTIVE_SKIP_CLIENT_${seat}_FAIL"
done
for log in "$HOST_LOG" "$CLIENT_1_LOG" "$CLIENT_2_LOG"; do
	reject_log_line "$log" "SCRIPT ERROR"
	reject_log_line "$log" "The multiplayer instance isn't currently active."
done
reject_log_line "$HOST_LOG" "INACTIVE_SKIP_HOST_FAIL"

echo "LAN_INACTIVE_HUMAN_TURN_SKIP_SMOKE_OK"
