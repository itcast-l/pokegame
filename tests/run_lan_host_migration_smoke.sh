#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"
MIGRATION_ADDRESS="${CODEX_LAN_MIGRATION_ADDRESS:-127.0.0.1}"

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
	if [[ "${KEEP_MIGRATION_TMP:-0}" == "1" ]]; then
		echo "Migration smoke logs kept at $TMP_DIR" >&2
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
		if [[ ! -e "$file" ]]; then
			sleep 0.25
			continue
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

wait_for_any_client_marker() {
	local case_dir="$1"
	local marker="$2"
	local timeout_seconds="$3"
	local deadline=$((SECONDS + timeout_seconds))
	while (( SECONDS < deadline )); do
		for log in "$case_dir"/client_*.log; do
			if grep -Fq "$marker" "$log" 2>/dev/null; then
				return 0
			fi
		done
		sleep 0.25
	done
	return 1
}

run_case() {
	local label="$1"
	local players="$2"
	local item_mode="$3"
	local case_dir="$TMP_DIR/$label"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"
	local host_log="$case_dir/host.log"
	local host_pid
	local -a client_pids=()

	echo "== host migration $label =="
	CODEX_LAN_MIGRATION_FIXTURE=1 CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=host --seat=0 --players="$players" --item-mode="$item_mode" \
		--result-dir="$result_dir" >"$host_log" 2>&1 &
	host_pid=$!
	PIDS+=("$host_pid")
	sleep 1

	for ((seat = 1; seat < players; seat++)); do
		local client_log="$case_dir/client_${seat}.log"
		CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-$seat" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=client --seat="$seat" --players="$players" --item-mode="$item_mode" \
			--result-dir="$result_dir" --allow-client-result-without-host \
			--require-all-migration-peers >"$client_log" 2>&1 &
		client_pids+=("$!")
		PIDS+=("$!")
	done

	if ! wait_for_any_client_marker "$case_dir" "STANDBY_ACKED" 60 \
		|| ! wait_for_marker "$host_log" "MIGRATION_PLAN_READY" 60 \
		|| ! wait_for_marker "$host_log" "FULL_MATCH_HOST_START_MATCH" 60; then
		echo "Host did not reach an ACKed, started match before migration: $label" >&2
		print_logs "$case_dir"
		return 1
	fi

	# This is intentionally an external hard kill. It must not execute the normal
	# LeaveSession path, so the clients exercise the announced migration plan.
	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true

	if ! wait_for_processes 150 "${client_pids[@]}"; then
		echo "Clients did not finish after host migration: $label" >&2
		print_logs "$case_dir"
		return 1
	fi

	for ((seat = 1; seat < players; seat++)); do
		local client_log="$case_dir/client_${seat}.log"
		if ! grep -Fq "FULL_MATCH_CLIENT_WINNER" "$client_log" \
			|| ! grep -Fq "FULL_MATCH_CLIENT_STATE 10" "$client_log" \
			|| ! grep -Fq "FULL_MATCH_CLIENT_STATE 7" "$client_log" \
			|| ! grep -Fq "MIGRATION_UI_STATE visible=true" "$client_log" \
			|| ! grep -Fq "private_leak=false" "$result_dir/client_${seat}.result"; then
			echo "Client did not complete migration assertions: $label seat=$seat" >&2
			print_logs "$case_dir"
			return 1
		fi
	done

	echo "LAN_HOST_MIGRATION_CASE_OK $label"
}

run_pending_case() {
	local label="$1"
	local fixture="$2"
	local pending_marker="$3"
	local case_dir="$TMP_DIR/$label"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"
	local host_log="$case_dir/host.log"
	local host_pid
	local -a client_pids=()

	echo "== host migration $label =="
	CODEX_LAN_MIGRATION_FIXTURE=1 CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=host --seat=0 --players=2 --item-mode=1 \
		--result-dir="$result_dir" --migration-fixture="$fixture" >"$host_log" 2>&1 &
	host_pid=$!
	PIDS+=("$host_pid")
	sleep 1

	local client_log="$case_dir/client_1.log"
	CODEX_LAN_MIGRATION_FIXTURE=1 CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-1" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=client --seat=1 --players=2 --item-mode=1 \
			--result-dir="$result_dir" --allow-client-result-without-host --require-all-migration-peers >"$client_log" 2>&1 &
	client_pids+=("$!")
	PIDS+=("$!")

	if ! wait_for_marker "$host_log" "$pending_marker" 90 \
		|| ! wait_for_marker "$host_log" "MIGRATION_PLAN_READY" 90 \
		|| ! wait_for_marker "$host_log" "FULL_MATCH_HOST_START_MATCH" 90; then
		echo "Migration fixture did not reach its kill point: $label" >&2
		print_logs "$case_dir"
		return 1
	fi

	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true
	if ! wait_for_processes 150 "${client_pids[@]}"; then
		echo "Client did not finish fixture migration: $label" >&2
		print_logs "$case_dir"
		return 1
	fi

	if ! grep -Fq "FULL_MATCH_CLIENT_WINNER" "$client_log" \
		|| ! grep -Fq "FULL_MATCH_CLIENT_STATE 10" "$client_log" \
		|| ! grep -Fq "FULL_MATCH_CLIENT_STATE 7" "$client_log" \
		|| ! grep -Fq "MIGRATION_UI_STATE visible=true" "$client_log" \
		|| ! grep -Fq "private_leak=false" "$result_dir/client_1.result"; then
		echo "Pending fixture migration assertions failed: $label" >&2
		print_logs "$case_dir"
		return 1
	fi

	echo "LAN_HOST_MIGRATION_FIXTURE_OK $label"
}

run_current_turn_case() {
	local label="current_turn"
	local case_dir="$TMP_DIR/$label"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"
	local host_log="$case_dir/host.log"
	local host_pid
	local -a client_pids=()

	echo "== host migration $label =="
	CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=host --seat=0 --players=2 --item-mode=0 \
		--result-dir="$result_dir" --kill-when=current-turn >"$host_log" 2>&1 &
	host_pid=$!
	PIDS+=("$host_pid")
	sleep 1

	local client_log="$case_dir/client_1.log"
	CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-1" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=client --seat=1 --players=2 --item-mode=0 \
		--result-dir="$result_dir" --allow-client-result-without-host --require-all-migration-peers >"$client_log" 2>&1 &
	client_pids+=("$!")
	PIDS+=("$!")

	if ! wait_for_marker "$host_log" "MIGRATION_KILL_READY current_turn=0" 90 \
		|| ! wait_for_marker "$host_log" "MIGRATION_PLAN_READY" 90; then
		echo "Current-turn migration did not reach kill point" >&2
		print_logs "$case_dir"
		return 1
	fi
	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true
	if ! wait_for_processes 150 "${client_pids[@]}"; then
		echo "Current-turn migration did not finish" >&2
		print_logs "$case_dir"
		return 1
	fi
	if ! grep -Fq "FULL_MATCH_CLIENT_WINNER" "$client_log" \
		|| ! grep -Fq "MIGRATION_UI_STATE visible=true" "$client_log"; then
		echo "Current-turn migration assertions failed" >&2
		print_logs "$case_dir"
		return 1
	fi
	echo "LAN_HOST_MIGRATION_CURRENT_TURN_OK"
}

run_active_lobby_case() {
	local label="$1"
	local players="$2"
	local inject_old_token="$3"
	local case_dir="$TMP_DIR/$label"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"
	local host_log="$case_dir/host.log"
	local host_pid
	local -a client_pids=()

	echo "== host migration $label =="
	CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- \
		--role=host --seat=0 --players="$players" --item-mode=1 \
		--result-dir="$result_dir" --active-lobby-migration --expect-migration-failure >"$host_log" 2>&1 &
	host_pid=$!
	PIDS+=("$host_pid")
	sleep 1

	for ((seat = 1; seat < players; seat++)); do
		local client_log="$case_dir/client_${seat}.log"
		if [[ "$inject_old_token" == "1" && "$seat" -eq 2 ]]; then
			CODEX_LAN_INJECT_OLD_HOST_TOKEN=1 \
			CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-$seat" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
				--script res://tests/LanFullMatchPeer.gd -- \
				--role=client --seat="$seat" --players="$players" --item-mode=1 \
				--result-dir="$result_dir" --active-lobby-migration --expect-migration-failure >"$client_log" 2>&1 &
		else
			local keep_open=""
			if [[ "$inject_old_token" == "1" && "$seat" -eq 1 ]]; then
				keep_open="--active-lobby-keep-open"
			fi
			CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-$seat" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
				--script res://tests/LanFullMatchPeer.gd -- \
				--role=client --seat="$seat" --players="$players" --item-mode=1 \
				--result-dir="$result_dir" --active-lobby-migration $keep_open >"$client_log" 2>&1 &
		fi
		client_pids+=("$!")
		PIDS+=("$!")
		if [[ "$inject_old_token" == "1" && "$seat" -eq 1 ]] \
			&& ! wait_for_marker "$case_dir/client_1.log" "STANDBY_ACKED" 60; then
			echo "Old-host token candidate did not reach standby: $label" >&2
			print_logs "$case_dir"
			return 1
		fi
	done

	if ! wait_for_marker "$host_log" "MIGRATION_ACTIVE_LOBBY_REQUESTED" 90; then
		echo "Active lobby migration did not start: $label" >&2
		print_logs "$case_dir"
		return 1
	fi
	if [[ "$inject_old_token" == "1" ]]; then
		if ! wait_for_processes 60 "$host_pid" "${client_pids[1]}"; then
			echo "Active lobby migration did not settle: $label" >&2
			print_logs "$case_dir"
			return 1
		fi
	elif ! wait_for_processes 60 "$host_pid" "${client_pids[@]}"; then
		echo "Active lobby migration did not settle: $label" >&2
		print_logs "$case_dir"
		return 1
	fi

	if ! grep -Fq "MIGRATION_FAILURE_STATE" "$host_log"; then
		echo "Source host did not leave active lobby migration: $label" >&2
		print_logs "$case_dir"
		return 1
	fi
	if [[ "$inject_old_token" == "1" ]]; then
		if ! grep -Fq "MIGRATION_OLD_HOST_TOKEN_INJECTED" "$case_dir/client_2.log" \
			|| ! grep -Fq "MIGRATION_FAILURE_STATE" "$case_dir/client_2.log" \
			|| ! grep -Fq "MIGRATION_FAILURE_UI" "$case_dir/client_2.log"; then
			echo "Old-host token rejection assertions failed" >&2
			print_logs "$case_dir"
			return 1
		fi
	else
		if ! grep -Fq "MIGRATION_LOBBY_RECOVERED" "$case_dir/client_1.log"; then
			echo "Lobby candidate did not recover" >&2
			print_logs "$case_dir"
			return 1
		fi
	fi
	if [[ "$inject_old_token" == "1" ]]; then
		kill "${client_pids[0]}" 2>/dev/null || true
		wait "${client_pids[0]}" 2>/dev/null || true
	fi
	echo "LAN_HOST_MIGRATION_LOBBY_OK $label"
}

run_failure_case() {
	local label="$1"
	local host_env_name="$2"
	local client_env_name="$3"
	local players="${4:-2}"
	local case_dir="$TMP_DIR/$label"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"
	local host_log="$case_dir/host.log"
	local host_pid
	local -a client_pids=()

	echo "== host migration failure $label =="
	if [[ "$host_env_name" == "corrupt_hash" ]]; then
		CODEX_LAN_MIGRATION_CORRUPT_HASH=1 \
		CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
			--script res://tests/LanFullMatchPeer.gd -- \
			--role=host --seat=0 --players="$players" --item-mode=1 --result-dir="$result_dir" >"$host_log" 2>&1 &
	else
		CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
			--script res://tests/LanFullMatchPeer.gd -- \
			--role=host --seat=0 --players="$players" --item-mode=1 --result-dir="$result_dir" >"$host_log" 2>&1 &
	fi
	host_pid=$!
	PIDS+=("$host_pid")
	sleep 1

	for ((seat = 1; seat < players; seat++)); do
		local client_log="$case_dir/client_${seat}.log"
		if [[ "$client_env_name" == "force_bind" ]]; then
			CODEX_LAN_FORCE_BIND_FAILURE=1 \
			CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-$seat" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
				--script res://tests/LanFullMatchPeer.gd -- \
				--role=client --seat="$seat" --players="$players" --item-mode=1 --result-dir="$result_dir" --expect-migration-failure >"$client_log" 2>&1 &
		else
			CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-$seat" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
				--script res://tests/LanFullMatchPeer.gd -- \
				--role=client --seat="$seat" --players="$players" --item-mode=1 --result-dir="$result_dir" --expect-migration-failure >"$client_log" 2>&1 &
		fi
		client_pids+=("$!")
		PIDS+=("$!")
	done

	if ! wait_for_marker "$host_log" "MIGRATION_PLAN_READY" 90 \
		|| ! wait_for_marker "$host_log" "FULL_MATCH_HOST_START_MATCH" 90; then
		echo "Failure case did not reach migration plan: $label" >&2
		print_logs "$case_dir"
		return 1
	fi
	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true
	if ! wait_for_processes 40 "${client_pids[@]}"; then
		echo "Failure case did not settle: $label" >&2
		print_logs "$case_dir"
		return 1
	fi

	local expected_code="CheckpointHashMismatch"
	if [[ "$client_env_name" == "force_bind" ]]; then
		expected_code="BindTimeout"
	fi
	for ((seat = 1; seat < players; seat++)); do
		local client_log="$case_dir/client_${seat}.log"
		if ! grep -Fq "MIGRATION_FAILED code=$expected_code" "$client_log" \
			|| ! grep -Fq "MIGRATION_FAILURE_UI" "$client_log" \
			|| ! grep -Fq "artifacts=false" "$client_log"; then
			echo "Failure code/cleanup assertions failed: $label seat=$seat" >&2
			print_logs "$case_dir"
			return 1
		fi
	done
	echo "LAN_HOST_MIGRATION_FAILURE_OK $label"
}

run_candidate_exit_timeout_case() {
	local label="candidate_exit_timeout"
	local case_dir="$TMP_DIR/$label"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"
	local host_log="$case_dir/host.log"
	local host_pid
	local -a client_pids=()

	echo "== host migration failure $label =="
	CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- --role=host --seat=0 --players=3 --item-mode=1 --result-dir="$result_dir" >"$host_log" 2>&1 &
	host_pid=$!
	PIDS+=("$host_pid")
	sleep 1
	for ((seat = 1; seat < 3; seat++)); do
		local client_log="$case_dir/client_${seat}.log"
		CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-$seat" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
			--script res://tests/LanFullMatchPeer.gd -- --role=client --seat="$seat" --players=3 --item-mode=1 --result-dir="$result_dir" --expect-migration-failure >"$client_log" 2>&1 &
		client_pids+=("$!")
		PIDS+=("$!")
	done
	if ! wait_for_marker "$host_log" "MIGRATION_PLAN_READY" 90 \
		|| ! wait_for_marker "$host_log" "FULL_MATCH_HOST_START_MATCH" 90; then
		echo "Candidate-exit case did not reach migration plan" >&2
		print_logs "$case_dir"
		return 1
	fi
	if ! wait_for_any_client_marker "$case_dir" "MIGRATION_PLAN_ACCEPTED" 30; then
		echo "Candidate-exit case did not deliver migration plan to a client" >&2
		print_logs "$case_dir"
		return 1
	fi

	local candidate_seat=-1
	local follower_seat=-1
	for seat in 1 2; do
		if grep -Fq "MIGRATION_PLAN_ACCEPTED candidate=lan-migration-${label}-seat-${seat} " "$case_dir/client_${seat}.log"; then
			candidate_seat="$seat"
		else
			follower_seat="$seat"
		fi
	done
	if (( candidate_seat < 1 || follower_seat < 1 )); then
		echo "Candidate-exit case could not identify candidate process" >&2
		print_logs "$case_dir"
		return 1
	fi
	local candidate_index=$((candidate_seat - 1))
	kill -9 "${client_pids[$candidate_index]}"
	wait "${client_pids[$candidate_index]}" 2>/dev/null || true
	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true
	local follower_index=$((follower_seat - 1))
	if ! wait_for_processes 30 "${client_pids[$follower_index]}"; then
		echo "Follower did not time out after candidate exit" >&2
		print_logs "$case_dir"
		return 1
	fi
	if ! grep -Fq "MIGRATION_FAILED code=ConnectTimeout" "$case_dir/client_${follower_seat}.log" \
		|| ! grep -Fq "MIGRATION_FAILURE_UI" "$case_dir/client_${follower_seat}.log" \
		|| ! grep -Fq "artifacts=false" "$case_dir/client_${follower_seat}.log"; then
		echo "Candidate-exit timeout assertions failed" >&2
		print_logs "$case_dir"
		return 1
	fi
	echo "LAN_HOST_MIGRATION_CANDIDATE_EXIT_OK"
}

run_stale_epoch_case() {
	local label="stale_epoch"
	local case_dir="$TMP_DIR/$label"
	local result_dir="$case_dir/results"
	mkdir -p "$result_dir"
	local host_log="$case_dir/host.log"
	local host_pid
	local -a client_pids=()

	echo "== host migration $label =="
	CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanFullMatchPeer.gd -- --role=host --seat=0 --players=3 --item-mode=1 --result-dir="$result_dir" >"$host_log" 2>&1 &
	host_pid=$!
	PIDS+=("$host_pid")
	sleep 1
	for ((seat = 1; seat < 3; seat++)); do
		local client_log="$case_dir/client_${seat}.log"
		CODEX_LAN_INJECT_OLD_EPOCH=1 \
		CODEX_LAN_MATCH_SEED=20260714 CODEX_LAN_MIGRATION_ADDRESS="$MIGRATION_ADDRESS" CODEX_TEST_STABLE_USER_ID="lan-migration-$label-seat-$seat" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
			--script res://tests/LanFullMatchPeer.gd -- --role=client --seat="$seat" --players=3 --item-mode=1 --result-dir="$result_dir" --allow-client-result-without-host --require-all-migration-peers >"$client_log" 2>&1 &
		client_pids+=("$!")
		PIDS+=("$!")
	done
	if ! wait_for_marker "$host_log" "MIGRATION_PLAN_READY" 90 \
		|| ! wait_for_marker "$host_log" "FULL_MATCH_HOST_START_MATCH" 90; then
		echo "Stale-epoch case did not reach migration plan" >&2
		print_logs "$case_dir"
		return 1
	fi
	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true
	if ! wait_for_processes 150 "${client_pids[@]}"; then
		echo "Stale-epoch case did not finish" >&2
		print_logs "$case_dir"
		return 1
	fi
	local stale_injection_seen=0
	local stale_rejection_seen=0
	for client_log in "$case_dir"/client_*.log; do
		if grep -Fq "MIGRATION_STALE_EPOCH_INJECTED" "$client_log"; then
			stale_injection_seen=1
		fi
		if grep -Fq "MIGRATION_PLAN_REJECT reason=Context" "$client_log"; then
			stale_rejection_seen=1
		fi
	done
	if (( stale_injection_seen == 0 || stale_rejection_seen == 0 )); then
		echo "Stale-epoch injection was not rejected" >&2
		print_logs "$case_dir"
		return 1
	fi
	echo "LAN_HOST_MIGRATION_STALE_EPOCH_OK"
}

if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "2p_standard" ]]; then
	run_case "2p_standard" 2 0
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "3p_items" ]]; then
	run_case "3p_items" 3 1
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "4p_items" ]]; then
	run_case "4p_items" 4 1
fi
echo "LAN_HOST_MIGRATION_SMOKE_OK"

if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "active_lobby" ]]; then
	run_active_lobby_case "active_lobby" 2 0
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "current_turn" ]]; then
	run_current_turn_case
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "pending_target" ]]; then
	run_pending_case "pending_target" "target" "MIGRATION_PENDING_READY mode=TargetPlayer"
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "pending_discard" ]]; then
	run_pending_case "pending_discard" "discard" "MIGRATION_PENDING_READY mode=HandLimitDiscard"
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "pending_attachment" ]]; then
	run_pending_case "pending_attachment" "attachment" "MIGRATION_PENDING_READY mode=AttachmentRewardHandCard"
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "visual_kill" ]]; then
	run_pending_case "visual_kill" "visual" "MIGRATION_VISUAL_ACTIVE"
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "stale_epoch" ]]; then
	run_stale_epoch_case
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "old_host_token" ]]; then
	run_active_lobby_case "old_host_token" 3 1
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "candidate_exit" ]]; then
	run_candidate_exit_timeout_case
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "wrong_hash" ]]; then
	run_failure_case "wrong_hash" "corrupt_hash" "" 2
fi
if [[ -z "${MIGRATION_ONLY_CASE:-}" || "${MIGRATION_ONLY_CASE}" == "bind_timeout" ]]; then
	run_failure_case "bind_timeout" "" "force_bind" 2
fi
echo "LAN_HOST_MIGRATION_FAILURE_SUITE_OK"
