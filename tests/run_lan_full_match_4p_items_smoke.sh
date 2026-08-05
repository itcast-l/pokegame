#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
source "$ROOT_DIR/tests/lan_full_match_assertions.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"
if [[ -n "${GODOT_BIN:-}" ]]; then GODOT_CMD="$GODOT_BIN"; elif command -v Godot >/dev/null 2>&1; then GODOT_CMD="$(command -v Godot)"; elif [[ -x "$DEFAULT_GODOT_BIN" ]]; then GODOT_CMD="$DEFAULT_GODOT_BIN"; else echo "Missing Godot binary. Set GODOT_BIN." >&2; exit 1; fi

GODOT_EXECUTABLE="$GODOT_CMD"
codex_run_muted_godot() { exec "$GODOT_EXECUTABLE" --audio-driver Dummy "$@"; }
GODOT_CMD=codex_run_muted_godot
export DECKLAND_TEST_MUTE_AUDIO=1

TMP_DIR="$(mktemp -d)"
codex_setup_godot_test_home "$TMP_DIR"
RESULT_DIR="$TMP_DIR/results"
mkdir -p "$RESULT_DIR"
PIDS=()
LOGS=()
cleanup() {
	local status=$?
	trap - EXIT
	for pid in "${PIDS[@]:-}"; do kill "$pid" 2>/dev/null || true; done
	if (( status != 0 )); then
		echo "LAN full-match 4p smoke assertion failed" >&2
		for log in "${LOGS[@]:-}"; do cat "$log" >&2 || true; done
	fi
	if [[ "${KEEP_LAN_FULL_MATCH_TMP:-0}" == "1" ]]; then
		echo "LAN full-match logs kept at $TMP_DIR" >&2
	else
		rm -rf "$TMP_DIR"
	fi
	exit "$status"
}
trap cleanup EXIT

CODEX_LAN_MATCH_SEED=20260714 "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanFullMatchPeer.gd -- --role=host --seat=0 --players=4 --item-mode=1 --result-dir="$RESULT_DIR" >"$TMP_DIR/host.log" 2>&1 & PIDS+=("$!"); LOGS+=("$TMP_DIR/host.log")
sleep 1
for seat in 1 2 3; do "$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanFullMatchPeer.gd -- --role=client --seat="$seat" --players=4 --item-mode=1 --result-dir="$RESULT_DIR" >"$TMP_DIR/client_${seat}.log" 2>&1 & PIDS+=("$!"); LOGS+=("$TMP_DIR/client_${seat}.log"); done

STATUS=0
for pid in "${PIDS[@]}"; do wait "$pid" || STATUS=$?; done
if [[ "$STATUS" -ne 0 ]]; then for log in "${LOGS[@]}"; do cat "$log" >&2 || true; done; exit 1; fi
grep -Fq "FULL_MATCH_HOST_START_MATCH" "$TMP_DIR/host.log"
assert_lan_full_match_result "$RESULT_DIR/expected_0.result" 4
for seat in 1 2 3; do grep -Fq "FULL_MATCH_CLIENT_WINNER" "$TMP_DIR/client_${seat}.log"; grep -Fq "private_leak=false" "$RESULT_DIR/client_${seat}.result"; test -s "$RESULT_DIR/client_${seat}.result"; done
for seat in 1 2 3; do
	assert_lan_full_match_result "$RESULT_DIR/expected_${seat}.result" 4
	assert_lan_full_match_result "$RESULT_DIR/client_${seat}.result" 4
	assert_lan_full_match_public_state_equal "$RESULT_DIR/expected_${seat}.result" "$RESULT_DIR/client_${seat}.result" 4
	expected_fingerprint="$(sed -n 's/^fingerprint=//p' "$RESULT_DIR/expected_${seat}.result")"
	grep -Fq "fingerprint=$expected_fingerprint" "$RESULT_DIR/client_${seat}.result"
done
echo "LAN_FULL_MATCH_4P_ITEMS_SMOKE_OK"
