#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
source "$ROOT_DIR/tests/lan_full_match_assertions.sh"
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
RESULT_DIR="$TMP_DIR/results"
mkdir -p "$RESULT_DIR"
HOST_LOG="$TMP_DIR/host.log"
CLIENT_LOG="$TMP_DIR/client.log"
HOST_PID=""
CLIENT_PID=""

cleanup() {
	if [[ -n "$CLIENT_PID" ]] && kill -0 "$CLIENT_PID" 2>/dev/null; then kill "$CLIENT_PID" 2>/dev/null || true; fi
	if [[ -n "$HOST_PID" ]] && kill -0 "$HOST_PID" 2>/dev/null; then kill "$HOST_PID" 2>/dev/null || true; fi
	rm -rf "$TMP_DIR"
}
trap cleanup EXIT

CODEX_LAN_MATCH_SEED=20260714 "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanFullMatchPeer.gd -- --role=host --seat=0 --players=2 --item-mode=0 --result-dir="$RESULT_DIR" >"$HOST_LOG" 2>&1 &
HOST_PID=$!
sleep 1
CODEX_LAN_MATCH_SEED=20260714 "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanFullMatchPeer.gd -- --role=client --seat=1 --players=2 --item-mode=0 --result-dir="$RESULT_DIR" >"$CLIENT_LOG" 2>&1 &
CLIENT_PID=$!

HOST_STATUS=0
CLIENT_STATUS=0
wait "$HOST_PID" || HOST_STATUS=$?
wait "$CLIENT_PID" || CLIENT_STATUS=$?
if [[ "$HOST_STATUS" -ne 0 || "$CLIENT_STATUS" -ne 0 ]]; then
	echo "LAN full-match 2p smoke failed: host=$HOST_STATUS client=$CLIENT_STATUS" >&2
	cat "$HOST_LOG" >&2 || true
	cat "$CLIENT_LOG" >&2 || true
	exit 1
fi

grep -Fq "FULL_MATCH_HOST_START_MATCH" "$HOST_LOG"
grep -Fq "FULL_MATCH_HOST_WINNER" "$HOST_LOG"
grep -Fq "FULL_MATCH_CLIENT_WINNER" "$CLIENT_LOG"
test -s "$RESULT_DIR/expected_0.result"
test -s "$RESULT_DIR/client_1.result"
grep -Fq "private_leak=false" "$RESULT_DIR/expected_0.result"
grep -Fq "private_leak=false" "$RESULT_DIR/client_1.result"
assert_lan_full_match_result "$RESULT_DIR/expected_0.result" 2
assert_lan_full_match_result "$RESULT_DIR/expected_1.result" 2
assert_lan_full_match_result "$RESULT_DIR/client_1.result" 2
assert_lan_full_match_public_state_equal "$RESULT_DIR/expected_1.result" "$RESULT_DIR/client_1.result" 2
grep -Fq "fingerprint=$(sed -n 's/^fingerprint=//p' "$RESULT_DIR/expected_1.result")" "$RESULT_DIR/client_1.result"
echo "LAN_FULL_MATCH_2P_STANDARD_SMOKE_OK"
