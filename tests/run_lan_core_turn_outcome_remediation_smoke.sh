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

run_case() {
	local player_count="$1"
	local tmp_dir
	tmp_dir="$(mktemp -d)"
	local host_log="$tmp_dir/host.log"
	local pids=()
	codex_setup_godot_test_home "$tmp_dir"
	"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanCoreTurnOutcomeHost.gd -- --player-count="$player_count" >"$host_log" 2>&1 &
	pids+=("$!")
	sleep 1
	for ((seat = 1; seat < player_count; seat++)); do
		local client_log="$tmp_dir/client_${seat}.log"
		"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LanCoreTurnOutcomeClient.gd -- --player-count="$player_count" --expected-seat="$seat" >"$client_log" 2>&1 &
		pids+=("$!")
		sleep 0.35
	done
	local status=0
	for pid in "${pids[@]}"; do
		wait "$pid" || status=$?
	done
	if [[ "$status" -ne 0 ]] || ! grep -Fq "CORE_TURN_LAN_HOST_${player_count}_DONE" "$host_log"; then
		for log in "$tmp_dir"/*.log; do
			echo "--- $log ---" >&2
			cat "$log" >&2 || true
		done
		rm -rf "$tmp_dir"
		exit 1
	fi
	for ((seat = 1; seat < player_count; seat++)); do
		if ! grep -Fq "CORE_TURN_LAN_CLIENT_${player_count}_${seat}_DONE" "$tmp_dir/client_${seat}.log"; then
			cat "$tmp_dir/client_${seat}.log" >&2
			rm -rf "$tmp_dir"
			exit 1
		fi
	done
	rm -rf "$tmp_dir"
}

run_case 3
run_case 4
echo "LAN_CORE_TURN_OUTCOME_REMEDIATION_SMOKE_OK"
