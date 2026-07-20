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
PIDS=()

cleanup() {
	for pid in "${PIDS[@]:-}"; do
		kill "$pid" 2>/dev/null || true
	done
	if [[ "${KEEP_AUTHORITY_TRANSACTION_TMP:-0}" == "1" ]]; then
		echo "Authority transaction logs kept at $TMP_DIR" >&2
	else
		rm -rf "$TMP_DIR"
	fi
}
trap cleanup EXIT

print_logs() {
	local case_dir="$1"
	for log in "$case_dir"/*.log; do
		echo "--- $log ---" >&2
		cat "$log" >&2 || true
	done
}

wait_for_marker() {
	local file="$1"
	local marker="$2"
	local timeout_seconds="$3"
	local deadline=$((SECONDS + timeout_seconds))
	while (( SECONDS < deadline )); do
		if grep -Fq "$marker" "$file" 2>/dev/null; then
			return 0
		fi
		sleep 0.2
	done
	return 1
}

wait_for_pattern() {
	local file="$1"
	local pattern="$2"
	local timeout_seconds="$3"
	local deadline=$((SECONDS + timeout_seconds))
	while (( SECONDS < deadline )); do
		if grep -Eq "$pattern" "$file" 2>/dev/null; then
			return 0
		fi
		sleep 0.2
	done
	return 1
}

wait_for_process() {
	local pid="$1"
	local timeout_seconds="$2"
	local deadline=$((SECONDS + timeout_seconds))
	while (( SECONDS < deadline )); do
		if ! kill -0 "$pid" 2>/dev/null; then
			wait "$pid"
			return $?
		fi
		sleep 0.25
	done
	return 1
}

read_field() {
	local file="$1"
	local key="$2"
	sed -n "s/^${key}=//p" "$file" | tail -n 1
}

start_peer() {
	local role="$1"
	local fixture="$2"
	local mode="$3"
	local result_path="$4"
	local log_path="$5"
	local stable_id="$6"
	local fault_point="${7:-}"
	CODEX_AUTHORITY_TRANSACTION_FIXTURE=1 \
	CODEX_AUTHORITY_FAULT_POINT="$fault_point" \
	CODEX_LAN_MATCH_SEED=20260720 \
	CODEX_LAN_MIGRATION_FIXTURE=1 \
	CODEX_LAN_MIGRATION_ADDRESS=127.0.0.1 \
	CODEX_TEST_STABLE_USER_ID="$stable_id" \
	"$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanAuthorityTransactionFaultPeer.gd -- \
		--role="$role" --fixture="$fixture" --mode="$mode" \
		--result-path="$result_path" >"$log_path" 2>&1 &
	STARTED_PID=$!
	PIDS+=("$STARTED_PID")
}

run_baseline() {
	local fixture="$1"
	local case_dir="$TMP_DIR/baseline_$fixture"
	mkdir -p "$case_dir"
	local result_path="$case_dir/result.txt"
	start_peer host "$fixture" baseline "$result_path" "$case_dir/host.log" "authority-baseline-$fixture-seat-0"
	local host_pid="$STARTED_PID"
	sleep 1
	start_peer client "$fixture" baseline "$result_path" "$case_dir/client.log" "authority-baseline-$fixture-seat-1"
	local client_pid="$STARTED_PID"
	if ! wait_for_process "$client_pid" 100 || ! wait_for_process "$host_pid" 30 \
			|| [[ ! -s "$result_path" ]] \
			|| ! grep -Fq "AUTHORITY_TRANSACTION_BASELINE_HOST_DONE" "$case_dir/host.log" \
			|| ! grep -Fq "AUTHORITY_TRANSACTION_BASELINE_CLIENT_DONE" "$case_dir/client.log"; then
		echo "Authority transaction baseline failed: $fixture" >&2
		print_logs "$case_dir"
		return 1
	fi
	if [[ "$(read_field "$result_path" next_action_id)" != "1" \
			|| "$(read_field "$result_path" client_action_id)" != "1" \
			|| "$(read_field "$result_path" authority_action_id)" != "0" ]]; then
		echo "Authority transaction baseline action ids are invalid: $fixture" >&2
		cat "$result_path" >&2
		return 1
	fi
	echo "$result_path"
}

compare_result() {
	local baseline="$1"
	local recovered="$2"
	local point="$3"
	for key in fingerprint score_0 score_1 rng next_action_id client_action_id turn outcome authority_action_id; do
		local expected
		local actual
		expected="$(read_field "$baseline" "$key")"
		actual="$(read_field "$recovered" "$key")"
		if [[ -z "$expected" || "$actual" != "$expected" ]]; then
			echo "Authority transaction mismatch point=$point key=$key expected=$expected actual=$actual" >&2
			return 1
		fi
	done
	local baseline_generation
	local recovered_generation
	baseline_generation="$(read_field "$baseline" generation)"
	recovered_generation="$(read_field "$recovered" generation)"
	if (( recovered_generation <= baseline_generation )); then
		echo "Authority generation did not advance point=$point baseline=$baseline_generation recovered=$recovered_generation" >&2
		return 1
	fi
}

run_fault_case() {
	local point="$1"
	local fixture="$2"
	local baseline="$3"
	local case_dir="$TMP_DIR/fault_$point"
	mkdir -p "$case_dir"
	local result_path="$case_dir/result.txt"
	start_peer host "$fixture" fault "$result_path" "$case_dir/host.log" "authority-$point-seat-0" "$point"
	local host_pid="$STARTED_PID"
	sleep 1
	start_peer client "$fixture" fault "$result_path" "$case_dir/client.log" "authority-$point-seat-1" "$point"
	local client_pid="$STARTED_PID"

	if ! wait_for_marker "$case_dir/host.log" "AUTHORITY_FAULT_REACHED point=$point " 90; then
		echo "Authority fault point was not reached: $point" >&2
		print_logs "$case_dir"
		return 1
	fi
	local marker
	local checkpoint
	local action_id
	local source_generation
	marker="$(grep -F "AUTHORITY_FAULT_REACHED point=$point " "$case_dir/host.log" | tail -n 1)"
	checkpoint="$(sed -E 's/.*checkpoint=([0-9]+).*/\1/' <<<"$marker")"
	action_id="$(sed -E 's/.*action=([0-9]+).*/\1/' <<<"$marker")"
	source_generation="$(sed -E 's/.*generation=([0-9]+).*/\1/' <<<"$marker")"
	if ! wait_for_marker "$case_dir/client.log" "STANDBY_ACKED checkpoint=$checkpoint " 30 \
			|| ! wait_for_pattern "$case_dir/host.log" "MIGRATION_PLAN_READY .*checkpoint=${checkpoint} " 30; then
		echo "Prepared/finalizing checkpoint was not ACKed before kill: $point checkpoint=$checkpoint" >&2
		print_logs "$case_dir"
		return 1
	fi

	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true
	if ! wait_for_process "$client_pid" 120 \
			|| [[ ! -s "$result_path" ]] \
			|| ! grep -Fq "AUTHORITY_TRANSACTION_RECOVERED fixture=$fixture" "$case_dir/client.log"; then
		echo "Authority fault recovery failed: $point" >&2
		print_logs "$case_dir"
		return 1
	fi
	if ! compare_result "$baseline" "$result_path" "$point"; then
		cat "$baseline" >&2
		cat "$result_path" >&2
		print_logs "$case_dir"
		return 1
	fi
	if [[ "$(read_field "$result_path" next_action_id)" != "$action_id" ]] \
			|| (( $(read_field "$result_path" generation) <= source_generation )) \
			|| (( $(read_field "$result_path" authority_epoch) <= 0 )); then
		echo "Authority identity/generation assertion failed: $point" >&2
		cat "$result_path" >&2
		return 1
	fi
	if grep -Fq "SCRIPT ERROR" "$case_dir/host.log" || grep -Fq "SCRIPT ERROR" "$case_dir/client.log"; then
		echo "Script error in authority transaction case: $point" >&2
		print_logs "$case_dir"
		return 1
	fi
	echo "LAN_AUTHORITY_TRANSACTION_FAULT_CASE_OK $point"
}

BASE_CAPTURE_RESULT="$(run_baseline base_capture)"
ITEM_SHUFFLE_RESULT="$(run_baseline item_shuffle)"

for point in after_move after_insert before_capture after_score after_capture; do
	run_fault_case "$point" base_capture "$BASE_CAPTURE_RESULT"
done
for point in after_item_remove before_effect_complete before_turn_advance after_turn_advance; do
	run_fault_case "$point" item_shuffle "$ITEM_SHUFFLE_RESULT"
done

echo "LAN_AUTHORITY_TRANSACTION_FAULT_SMOKE_OK"
