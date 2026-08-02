#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"
NETEM_DRIVER="${CODEX_NETEM_DRIVER:-$ROOT_DIR/tools/host_migration_netem_linux.sh}"
MIGRATION_ADDRESS="${CODEX_LAN_MIGRATION_ADDRESS:-127.0.0.1}"
export CODEX_LAN_SKIP_MIGRATION_PORT_PROBE=1

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
if [[ ! -x "$NETEM_DRIVER" ]]; then
	echo "Network impairment driver is not executable: $NETEM_DRIVER" >&2
	exit 1
fi

TMP_DIR="$(mktemp -d)"
codex_setup_godot_test_home "$TMP_DIR"
SUITE_LOG="${CODEX_NETWORK_IMPAIRMENT_SUITE_LOG:-$TMP_DIR/suite.log}"
mkdir -p "$(dirname "$SUITE_LOG")"
exec > >(tee "$SUITE_LOG") 2>&1
PIDS=()
IMPAIRMENT_ACTIVE=0

clear_impairment() {
	if (( IMPAIRMENT_ACTIVE == 1 )); then
		"$NETEM_DRIVER" clear || true
		IMPAIRMENT_ACTIVE=0
	fi
}

cleanup() {
	clear_impairment
	for pid in "${PIDS[@]:-}"; do
		kill "$pid" 2>/dev/null || true
	done
	if [[ "${KEEP_NETWORK_IMPAIRMENT_TMP:-0}" == "1" ]]; then
		echo "Network impairment logs kept at $TMP_DIR" >&2
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
		sleep 0.25
	done
	return 1
}

wait_for_marker_after() {
	local file="$1"
	local preceding_marker="$2"
	local marker="$3"
	local timeout_seconds="$4"
	local deadline=$((SECONDS + timeout_seconds))
	while (( SECONDS < deadline )); do
		if awk -v preceding_marker="$preceding_marker" -v marker="$marker" '
			index($0, preceding_marker) { preceding_seen = 1; next }
			preceding_seen && index($0, marker) { found = 1; exit }
			END { exit found ? 0 : 1 }
		' "$file" 2>/dev/null; then
			return 0
		fi
		sleep 0.25
	done
	return 1
}

wait_for_processes() {
	local timeout_seconds="$1"
	shift
	local deadline=$((SECONDS + timeout_seconds))
	while (( SECONDS < deadline )); do
		local running=0
		for pid in "$@"; do
			if kill -0 "$pid" 2>/dev/null; then
				running=1
				break
			fi
		done
		if (( running == 0 )); then
			return 0
		fi
		sleep 0.5
	done
	return 1
}

assert_result_consistency() {
	local expected="$1"
	local actual="$2"
	local key
	for key in winner fingerprint snapshot_id private_leak table_count score_0 score_1 score_2 deck_0 deck_1 deck_2 hand_0 hand_1 hand_2 item_hand_0 item_hand_1 item_hand_2; do
		local expected_value
		local actual_value
		expected_value="$(sed -n "s/^${key}=//p" "$expected")"
		actual_value="$(sed -n "s/^${key}=//p" "$actual")"
		if [[ -z "$expected_value" || "$expected_value" != "$actual_value" ]]; then
			echo "Result mismatch key=$key expected=$expected_value actual=$actual_value" >&2
			return 1
		fi
	done
}

start_three_player_case() {
	local label="$1"
	local case_dir="$2"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"

	DECKLAND_TEST_MUTE_AUDIO=1 CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="netem-$label-seat-0" \
		"$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=host --seat=0 --players=3 --item-mode=1 \
		--result-dir="$result_dir" >"$case_dir/host.log" 2>&1 &
	CASE_HOST_PID=$!
	PIDS+=("$CASE_HOST_PID")
	sleep 1

	CASE_CLIENT_PIDS=()
	for seat in 1 2; do
		DECKLAND_TEST_MUTE_AUDIO=1 CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="netem-$label-seat-$seat" \
			"$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" \
			--script res://tests/LanFullMatchPeer.gd -- \
			--role=client --seat="$seat" --players=3 --item-mode=1 \
			--result-dir="$result_dir" --allow-client-result-without-host \
			--require-all-migration-peers >"$case_dir/client_${seat}.log" 2>&1 &
		CASE_CLIENT_PIDS+=("$!")
		PIDS+=("$!")
	done
}

assert_success_case() {
	local label="$1"
	local case_dir="$2"
	local result_dir="$case_dir/results"
	for seat in 1 2; do
		local client_log="$case_dir/client_${seat}.log"
		if ! grep -Fq "FULL_MATCH_CLIENT_STATE 10" "$client_log" \
			|| ! grep -Fq "FULL_MATCH_CLIENT_WINNER" "$client_log" \
			|| ! grep -Fq "FULL_MATCH_CLIENT_DONE" "$client_log" \
			|| grep -Eq 'SCRIPT ERROR|Unhandled exception|MIGRATION_FAILED' "$client_log"; then
			echo "Network impairment assertions failed: $label seat=$seat" >&2
			print_logs "$case_dir"
			return 1
		fi
		assert_result_consistency "$result_dir/expected_${seat}.result" "$result_dir/client_${seat}.result"
	done
	if grep -Eq 'SCRIPT ERROR|Unhandled exception' "$case_dir/host.log"; then
		echo "Source host logged a runtime error: $label" >&2
		print_logs "$case_dir"
		return 1
	fi
}

run_impairment_case() {
	local profile="$1"
	local duration_seconds="$2"
	local case_dir="$TMP_DIR/$profile"
	echo "== network impairment $profile =="
	start_three_player_case "$profile" "$case_dir"

	if ! wait_for_marker "$case_dir/host.log" "FULL_MATCH_HOST_STATE 7" 90 \
		|| ! wait_for_marker "$case_dir/host.log" "MIGRATION_PLAN_READY phase=InGame" 90; then
		echo "Case did not reach a migration-ready match: $profile" >&2
		print_logs "$case_dir"
		return 1
	fi

	"$NETEM_DRIVER" apply "$profile"
	IMPAIRMENT_ACTIVE=1
	printf 'NETWORK_IMPAIRMENT_APPLIED profile=%s duration=%s\n' "$profile" "$duration_seconds" | tee -a "$case_dir/orchestrator.log"
	sleep "$duration_seconds"
	clear_impairment
	printf 'NETWORK_IMPAIRMENT_CLEARED profile=%s\n' "$profile" | tee -a "$case_dir/host.log" "$case_dir/orchestrator.log"

	if [[ "$profile" == "short_disconnect" ]]; then
		for seat in 1 2; do
			if ! wait_for_marker "$case_dir/client_${seat}.log" "FULL_MATCH_CLIENT_STATE 8" 30 \
				|| ! wait_for_marker_after "$case_dir/client_${seat}.log" "FULL_MATCH_CLIENT_STATE 8" "FULL_MATCH_CLIENT_STATE 7" 60; then
				echo "Short disconnect did not reconnect cleanly: seat=$seat" >&2
				print_logs "$case_dir"
				return 1
			fi
		done
	fi

	if ! wait_for_marker_after "$case_dir/host.log" "NETWORK_IMPAIRMENT_CLEARED profile=$profile" "MIGRATION_PLAN_READY phase=InGame" 90; then
		echo "Migration availability did not reconverge after impairment: $profile" >&2
		print_logs "$case_dir"
		return 1
	fi

	kill -9 "$CASE_HOST_PID"
	wait "$CASE_HOST_PID" 2>/dev/null || true
	if ! wait_for_processes 180 "${CASE_CLIENT_PIDS[@]}"; then
		echo "Clients did not reach a finite outcome after impairment: $profile" >&2
		print_logs "$case_dir"
		return 1
	fi
	assert_success_case "$profile" "$case_dir"
	echo "LAN_NETWORK_IMPAIRMENT_CASE_OK $profile"
}

run_simultaneous_loss_case() {
	local label="host_candidate_simultaneous_loss"
	local case_dir="$TMP_DIR/$label"
	echo "== network impairment $label =="
	start_three_player_case "$label" "$case_dir"
	if ! wait_for_marker "$case_dir/host.log" "MIGRATION_PLAN_READY phase=InGame" 90; then
		echo "Simultaneous-loss case did not reach a migration plan" >&2
		print_logs "$case_dir"
		return 1
	fi
	for seat in 1 2; do
		if ! wait_for_marker "$case_dir/client_${seat}.log" "FULL_MATCH_CLIENT_LOCAL_SEAT" 30; then
			echo "Simultaneous-loss case could not resolve client seats" >&2
			print_logs "$case_dir"
			return 1
		fi
	done

	local candidate_seat
	candidate_seat="$(sed -n 's/.*MIGRATION_PLAN_READY phase=InGame candidate_seat=\([0-9][0-9]*\).*/\1/p' "$case_dir/host.log" | tail -n 1)"
	if [[ "$candidate_seat" != "1" && "$candidate_seat" != "2" ]]; then
		echo "Could not identify the signed candidate" >&2
		print_logs "$case_dir"
		return 1
	fi
	local follower_seat=$((3 - candidate_seat))
	local candidate_index=$((candidate_seat - 1))
	local follower_index=$((follower_seat - 1))
	kill -9 "${CASE_CLIENT_PIDS[$candidate_index]}" "$CASE_HOST_PID"
	wait "${CASE_CLIENT_PIDS[$candidate_index]}" 2>/dev/null || true
	wait "$CASE_HOST_PID" 2>/dev/null || true

	if ! wait_for_processes 35 "${CASE_CLIENT_PIDS[$follower_index]}" \
		|| ! grep -Fq "MIGRATION_FAILED code=ConnectTimeout" "$case_dir/client_${follower_seat}.log" \
		|| ! grep -Fq "MIGRATION_FAILURE_UI" "$case_dir/client_${follower_seat}.log" \
		|| ! grep -Fq "artifacts=false" "$case_dir/client_${follower_seat}.log"; then
		echo "Simultaneous host/candidate loss did not fail safely" >&2
		print_logs "$case_dir"
		return 1
	fi
	echo "LAN_NETWORK_IMPAIRMENT_CASE_OK $label"
}

ONLY_CASE="${NETWORK_IMPAIRMENT_ONLY_CASE:-}"
if [[ -z "$ONLY_CASE" || "$ONLY_CASE" == "latency" ]]; then
	run_impairment_case latency 8
fi
if [[ -z "$ONLY_CASE" || "$ONLY_CASE" == "packet_loss" ]]; then
	run_impairment_case packet_loss 8
fi
if [[ -z "$ONLY_CASE" || "$ONLY_CASE" == "duplicate_packet" ]]; then
	run_impairment_case duplicate_packet 8
fi
if [[ -z "$ONLY_CASE" || "$ONLY_CASE" == "short_disconnect" ]]; then
	run_impairment_case short_disconnect 4
fi
if [[ -z "$ONLY_CASE" || "$ONLY_CASE" == "host_candidate_simultaneous_loss" ]]; then
	run_simultaneous_loss_case
fi

echo "LAN_HOST_MIGRATION_NETWORK_IMPAIRMENT_SUITE_OK"
echo "NETWORK_IMPAIRMENT_SUITE_LOG $SUITE_LOG"
