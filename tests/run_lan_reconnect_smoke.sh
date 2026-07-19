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
HOST_PID=""
CLIENT_PID=""

cleanup() {
	if [[ -n "$CLIENT_PID" ]] && kill -0 "$CLIENT_PID" 2>/dev/null; then
		kill "$CLIENT_PID" 2>/dev/null || true
	fi
	if [[ -n "$HOST_PID" ]] && kill -0 "$HOST_PID" 2>/dev/null; then
		kill "$HOST_PID" 2>/dev/null || true
	fi
	rm -rf "$TMP_DIR"
}

print_logs() {
	local host_log="$1"
	local client_log="$2"
	echo "--- host log ---" >&2
	cat "$host_log" >&2 || true
	echo "--- client log ---" >&2
	cat "$client_log" >&2 || true
}

require_log_line() {
	local file="$1"
	local needle="$2"
	if ! grep -Fq "$needle" "$file"; then
		echo "Missing log marker: $needle" >&2
		return 1
	fi
}

reject_log_line() {
	local file="$1"
	local needle="$2"
	if grep -Fq "$needle" "$file"; then
		echo "Unexpected log marker: $needle" >&2
		return 1
	fi
}

trap cleanup EXIT

run_case() {
	local label="$1"
	local paused_client="$2"
	local case_dir="$TMP_DIR/$label"
	local host_log="$case_dir/host.log"
	local client_log="$case_dir/client.log"
	mkdir -p "$case_dir"

	"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanReconnectSmokeHost.gd >"$host_log" 2>&1 &
	HOST_PID=$!
	sleep 1
	if [[ "$paused_client" == "1" ]]; then
		CODEX_LAN_RECONNECT_PAUSED=1 "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanReconnectSmokeClient.gd >"$client_log" 2>&1 &
	else
		"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanReconnectSmokeClient.gd >"$client_log" 2>&1 &
	fi
	CLIENT_PID=$!

	local host_status=0
	local client_status=0
	wait "$HOST_PID" || host_status=$?
	wait "$CLIENT_PID" || client_status=$?
	HOST_PID=""
	CLIENT_PID=""

	if [[ "$host_status" -ne 0 || "$client_status" -ne 0 ]]; then
		echo "LAN reconnect smoke failed ($label): host=$host_status client=$client_status" >&2
		print_logs "$host_log" "$client_log"
		return 1
	fi

	for marker in HOST_START_MATCH HOST_BOARD_INIT HOST_FORCE_DISCONNECT HOST_SAW_AI HOST_SAW_RECLAIM HOST_DONE; do
		require_log_line "$host_log" "$marker" || { print_logs "$host_log" "$client_log"; return 1; }
	done
	for marker in CLIENT_BOARD_INIT "CLIENT_STATE 8" CLIENT_RECONNECTED CLIENT_DONE; do
		require_log_line "$client_log" "$marker" || { print_logs "$host_log" "$client_log"; return 1; }
	done
	if [[ "$paused_client" == "1" ]]; then
		for marker in CLIENT_PAUSED_FOR_RECONNECT CLIENT_PAUSE_PRESERVED; do
			require_log_line "$client_log" "$marker" || { print_logs "$host_log" "$client_log"; return 1; }
		done
	fi
	reject_log_line "$host_log" "The multiplayer instance isn't currently active." || { print_logs "$host_log" "$client_log"; return 1; }
	reject_log_line "$client_log" "The multiplayer instance isn't currently active." || { print_logs "$host_log" "$client_log"; return 1; }
	reject_log_line "$host_log" "SCRIPT ERROR" || { print_logs "$host_log" "$client_log"; return 1; }
	reject_log_line "$client_log" "SCRIPT ERROR" || { print_logs "$host_log" "$client_log"; return 1; }
	echo "LAN_RECONNECT_CASE_OK $label"
}

run_case "standard" 0
run_case "paused_client" 1

echo "LAN_RECONNECT_SMOKE_OK"
