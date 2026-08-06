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
RECOVERY_LOG="$TMP_DIR/recovery.log"
HOST_DISCONNECT_HOST_LOG="$TMP_DIR/host_disconnect_host.log"
HOST_DISCONNECT_CLIENT_LOG="$TMP_DIR/host_disconnect_client.log"
REENTRANT_HOST_LOG="$TMP_DIR/reentrant_host.log"
REENTRANT_CLIENT_ONE_LOG="$TMP_DIR/reentrant_client_one.log"
REENTRANT_CLIENT_TWO_LOG="$TMP_DIR/reentrant_client_two.log"
SAME_LOBBY_HOST_LOG="$TMP_DIR/same_lobby_host.log"
SAME_LOBBY_CLIENT_ONE_LOG="$TMP_DIR/same_lobby_client_one.log"
SAME_LOBBY_CLIENT_TWO_LOG="$TMP_DIR/same_lobby_client_two.log"
IDLE_REPLACEMENT_HOST_LOG="$TMP_DIR/idle_replacement_host.log"
IDLE_REPLACEMENT_CLIENT_LOG="$TMP_DIR/idle_replacement_client.log"
HOST_PID=""
OBSERVER_PID=""
DROPPER_PID=""
RECOVERY_PID=""
HOST_DISCONNECT_CLIENT_PID=""

cleanup() {
	for pid in "$HOST_DISCONNECT_CLIENT_PID" "$RECOVERY_PID" "$DROPPER_PID" "$OBSERVER_PID" "$HOST_PID"; do
		if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
			kill "$pid" 2>/dev/null || true
		fi
	done
	rm -rf "$TMP_DIR"
}

print_logs() {
	for name in host observer dropper recovery; do
		echo "--- $name log ---" >&2
		cat "$TMP_DIR/$name.log" >&2 || true
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

"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectHost.gd >"$HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=observer "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$OBSERVER_LOG" 2>&1 &
OBSERVER_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=dropper "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$DROPPER_LOG" 2>&1 &
DROPPER_PID=$!

DROPPER_STATUS=0
wait "$DROPPER_PID" || DROPPER_STATUS=$?
if [[ "$DROPPER_STATUS" -ne 0 ]]; then
	echo "LAN loading-disconnect dropper failed: $DROPPER_STATUS" >&2
	print_logs
	exit 1
fi

for _ in $(seq 1 100); do
	if grep -Fq "HOST_CANCELLATION_VERIFIED" "$HOST_LOG"; then
		break
	fi
	sleep 0.1
done
require_log_line "$HOST_LOG" "HOST_CANCELLATION_VERIFIED"

CODEX_LOADING_DISCONNECT_MODE=recovery "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$RECOVERY_LOG" 2>&1 &
RECOVERY_PID=$!

HOST_STATUS=0
OBSERVER_STATUS=0
RECOVERY_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$OBSERVER_PID" || OBSERVER_STATUS=$?
wait "$RECOVERY_PID" || RECOVERY_STATUS=$?

if [[ "$HOST_STATUS" -ne 0 || "$OBSERVER_STATUS" -ne 0 || "$RECOVERY_STATUS" -ne 0 ]]; then
	echo "LAN loading-disconnect smoke failed: host=$HOST_STATUS observer=$OBSERVER_STATUS recovery=$RECOVERY_STATUS" >&2
	print_logs
	exit 1
fi

require_log_line "$HOST_LOG" "HOST_START_FIRST_MATCH"
require_log_line "$HOST_LOG" "HOST_CANCELLATION_VERIFIED"
require_log_line "$HOST_LOG" "HOST_START_RECOVERY_MATCH"
require_log_line "$HOST_LOG" "HOST_DONE"
require_log_line "$OBSERVER_LOG" "CLIENT_OBSERVER_ENTERED_BOARD"
require_log_line "$OBSERVER_LOG" "CLIENT_OBSERVER_RETURNED_TO_LOBBY"
require_log_line "$OBSERVER_LOG" "CLIENT_OBSERVER_REREADY"
require_log_line "$OBSERVER_LOG" "CLIENT_OBSERVER_RECOVERY_DONE"
require_log_line "$DROPPER_LOG" "CLIENT_DROPPER_EXIT_DURING_LOADING"
require_log_line "$RECOVERY_LOG" "CLIENT_RECOVERY_DONE"
reject_log_line "$HOST_LOG" "HOST_FAIL"
reject_log_line "$OBSERVER_LOG" "CLIENT_OBSERVER_FAIL"
reject_log_line "$RECOVERY_LOG" "CLIENT_RECOVERY_FAIL"

echo "LAN_LOADING_DISCONNECT_NON_HOST_SMOKE_OK"

CODEX_LOADING_DISCONNECT_HOST_MODE=host_disconnect "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectHost.gd >"$HOST_DISCONNECT_HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=host_disconnect "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$HOST_DISCONNECT_CLIENT_LOG" 2>&1 &
HOST_DISCONNECT_CLIENT_PID=$!

HOST_STATUS=0
CLIENT_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$HOST_DISCONNECT_CLIENT_PID" || CLIENT_STATUS=$?
HOST_DISCONNECT_CLIENT_PID=""

if [[ "$HOST_STATUS" -ne 0 || "$CLIENT_STATUS" -ne 0 ]]; then
	echo "LAN loading-disconnect host-disconnect phase failed: host=$HOST_STATUS client=$CLIENT_STATUS" >&2
	echo "--- host-disconnect host log ---" >&2
	cat "$HOST_DISCONNECT_HOST_LOG" >&2 || true
	echo "--- host-disconnect client log ---" >&2
	cat "$HOST_DISCONNECT_CLIENT_LOG" >&2 || true
	exit 1
fi

require_log_line "$HOST_DISCONNECT_HOST_LOG" "HOST_EXIT_DURING_LOADING"
require_log_line "$HOST_DISCONNECT_CLIENT_LOG" "CLIENT_HOST_DISCONNECT_ERROR NETWORK_ERROR_HOST_DISCONNECTED"
require_log_line "$HOST_DISCONNECT_CLIENT_LOG" "CLIENT_HOST_DISCONNECT_ERROR_COUNT 1"
require_log_line "$HOST_DISCONNECT_CLIENT_LOG" "CLIENT_HOST_DISCONNECT_ERROR_BEFORE_LOBBY"
require_log_line "$HOST_DISCONNECT_CLIENT_LOG" "CLIENT_HOST_DISCONNECT_RETURNED_TO_LOBBY"
reject_log_line "$HOST_DISCONNECT_CLIENT_LOG" "CLIENT_HOST_DISCONNECT_FAIL"

CODEX_LOADING_DISCONNECT_HOST_MODE=host_disconnect "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectHost.gd >"$IDLE_REPLACEMENT_HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=host_disconnect_idle_replacement "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$IDLE_REPLACEMENT_CLIENT_LOG" 2>&1 &
HOST_DISCONNECT_CLIENT_PID=$!

HOST_STATUS=0
CLIENT_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$HOST_DISCONNECT_CLIENT_PID" || CLIENT_STATUS=$?
HOST_DISCONNECT_CLIENT_PID=""

if [[ "$HOST_STATUS" -ne 0 || "$CLIENT_STATUS" -ne 0 ]]; then
	echo "LAN loading-disconnect idle-replacement phase failed: host=$HOST_STATUS client=$CLIENT_STATUS" >&2
	cat "$IDLE_REPLACEMENT_HOST_LOG" >&2 || true
	cat "$IDLE_REPLACEMENT_CLIENT_LOG" >&2 || true
	exit 1
fi

require_log_line "$IDLE_REPLACEMENT_HOST_LOG" "HOST_EXIT_DURING_LOADING"
require_log_line "$IDLE_REPLACEMENT_CLIENT_LOG" "CLIENT_IDLE_REPLACEMENT_START"
require_log_line "$IDLE_REPLACEMENT_CLIENT_LOG" "CLIENT_IDLE_REPLACEMENT_DEFERRED_WAIT_COMPLETE"
require_log_line "$IDLE_REPLACEMENT_CLIENT_LOG" "CLIENT_IDLE_REPLACEMENT_RESULT PASS"
reject_log_line "$IDLE_REPLACEMENT_CLIENT_LOG" "CLIENT_HOST_DISCONNECT_IDLE_REPLACEMENT_FAIL"

CODEX_LOADING_DISCONNECT_HOST_MODE=reentrant "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectHost.gd >"$REENTRANT_HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=reentrant "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$REENTRANT_CLIENT_ONE_LOG" 2>&1 &
OBSERVER_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=reentrant "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$REENTRANT_CLIENT_TWO_LOG" 2>&1 &
DROPPER_PID=$!

HOST_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
if [[ "$HOST_STATUS" -ne 0 ]]; then
	echo "LAN loading-disconnect reentrant phase failed: host=$HOST_STATUS" >&2
	cat "$REENTRANT_HOST_LOG" >&2 || true
	exit 1
fi

require_log_line "$REENTRANT_HOST_LOG" "HOST_REENTRANT_CANCEL"
require_log_line "$REENTRANT_HOST_LOG" "HOST_REENTRANT_START_MATCH"
require_log_line "$REENTRANT_HOST_LOG" "HOST_REENTRANT_DEFERRED_WAIT_COMPLETE"
require_log_line "$REENTRANT_HOST_LOG" "HOST_REENTRANT_RESULT PASS"
reject_log_line "$REENTRANT_HOST_LOG" "HOST_FAIL"
reject_log_line "$REENTRANT_HOST_LOG" "HOST_REENTRANT_RESULT FAIL"

CODEX_LOADING_DISCONNECT_HOST_MODE=same_lobby_reentrant "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectHost.gd >"$SAME_LOBBY_HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=reentrant "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$SAME_LOBBY_CLIENT_ONE_LOG" 2>&1 &
OBSERVER_PID=$!
sleep 1
CODEX_LOADING_DISCONNECT_MODE=reentrant "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanLoadingDisconnectClient.gd >"$SAME_LOBBY_CLIENT_TWO_LOG" 2>&1 &
DROPPER_PID=$!

HOST_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
if [[ "$HOST_STATUS" -ne 0 ]]; then
	echo "LAN loading-disconnect same-lobby reentrant phase failed: host=$HOST_STATUS" >&2
	cat "$SAME_LOBBY_HOST_LOG" >&2 || true
	exit 1
fi

require_log_line "$SAME_LOBBY_HOST_LOG" "HOST_REENTRANT_CANCEL"
require_log_line "$SAME_LOBBY_HOST_LOG" "HOST_SAME_LOBBY_REPLACEMENT_START"
require_log_line "$SAME_LOBBY_HOST_LOG" "HOST_SAME_LOBBY_DEFERRED_WAIT_COMPLETE"
require_log_line "$SAME_LOBBY_HOST_LOG" "HOST_SAME_LOBBY_RESULT PASS"
reject_log_line "$SAME_LOBBY_HOST_LOG" "HOST_FAIL"
reject_log_line "$SAME_LOBBY_HOST_LOG" "HOST_REENTRANT_RESULT FAIL"

echo "LAN_LOADING_DISCONNECT_SMOKE_OK"
