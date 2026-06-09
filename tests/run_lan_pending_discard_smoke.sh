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
CLIENT_LOG="$TMP_DIR/client.log"
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
	echo "--- host log ---" >&2
	cat "$HOST_LOG" >&2 || true
	echo "--- client log ---" >&2
	cat "$CLIENT_LOG" >&2 || true
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

"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanPendingDiscardHost.gd >"$HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanPendingDiscardClient.gd >"$CLIENT_LOG" 2>&1 &
CLIENT_PID=$!

HOST_STATUS=0
CLIENT_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$CLIENT_PID" || CLIENT_STATUS=$?

if [[ "$HOST_STATUS" -ne 0 || "$CLIENT_STATUS" -ne 0 ]]; then
	echo "LAN pending-discard smoke failed: host=$HOST_STATUS client=$CLIENT_STATUS" >&2
	print_logs
	exit 1
fi

require_log_line "$HOST_LOG" "HOST_START_MATCH"
require_log_line "$HOST_LOG" "HOST_BOARD_INIT"
require_log_line "$HOST_LOG" "HOST_STARTED_PENDING_DISCARD"
require_log_line "$HOST_LOG" "HOST_FORCE_DISCONNECT"
require_log_line "$HOST_LOG" "HOST_SAW_AI"
require_log_line "$HOST_LOG" "HOST_DISCARD_RESOLVED"
require_log_line "$HOST_LOG" "HOST_DONE"
if [[ "${CODEX_FORCE_AI_FALLBACK:-}" == "1" ]]; then
	require_log_line "$HOST_LOG" "HOST_FORCED_DISCARD_FALLBACK"
	require_log_line "$HOST_LOG" "AI proxy discard heuristic failed"
fi
require_log_line "$CLIENT_LOG" "CLIENT_BOARD_INIT"
require_log_line "$CLIENT_LOG" "CLIENT_RECONNECTING"
require_log_line "$CLIENT_LOG" "CLIENT_DONE"
reject_log_line "$HOST_LOG" "The multiplayer instance isn't currently active."
reject_log_line "$CLIENT_LOG" "The multiplayer instance isn't currently active."

echo "LAN_PENDING_DISCARD_SMOKE_OK"
