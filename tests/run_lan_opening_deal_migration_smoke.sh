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

run_case() {
	local point="$1"
	local tmp_dir
	tmp_dir="$(mktemp -d)"
	codex_setup_godot_test_home "$tmp_dir"
	local host_log="$tmp_dir/host.log"
	local client_log="$tmp_dir/client.log"
	CODEX_OPENING_MIGRATION_FIXTURE=1 CODEX_OPENING_DEAL_FAULT_POINT="$point" \
	CODEX_LAN_MATCH_SEED=20260720 CODEX_LAN_MIGRATION_ADDRESS=127.0.0.1 \
	CODEX_TEST_STABLE_USER_ID="opening-$point-seat-0" \
	"$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanOpeningDealMigrationHost.gd >"$host_log" 2>&1 &
	local host_pid=$!
	sleep 1
	CODEX_OPENING_MIGRATION_FIXTURE=1 CODEX_OPENING_DEAL_FAULT_POINT="$point" \
	CODEX_LAN_MATCH_SEED=20260720 CODEX_LAN_MIGRATION_ADDRESS=127.0.0.1 \
	CODEX_TEST_STABLE_USER_ID="opening-$point-seat-1" \
	"$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanOpeningDealMigrationClient.gd -- --point="$point" >"$client_log" 2>&1 &
	local client_pid=$!

	if ! wait_for_marker "$host_log" "OPENING_DEAL_FAULT_REACHED point=$point " 45 \
			|| ! wait_for_marker "$client_log" "OPENING_DEAL_FAULT_REACHED point=$point " 45 \
			|| ! wait_for_marker "$client_log" "STANDBY_ACKED checkpoint=" 45 \
			|| ! wait_for_marker "$host_log" "MIGRATION_PLAN_READY" 45; then
		echo "Opening-deal migration did not reach synchronized fault point: $point" >&2
		cat "$host_log" >&2 || true
		cat "$client_log" >&2 || true
		kill "$host_pid" "$client_pid" 2>/dev/null || true
		wait "$host_pid" 2>/dev/null || true
		wait "$client_pid" 2>/dev/null || true
		rm -rf "$tmp_dir"
		return 1
	fi

	kill -9 "$host_pid"
	wait "$host_pid" 2>/dev/null || true
	local status=0
	wait "$client_pid" || status=$?
	if [[ "$status" -ne 0 ]] \
			|| ! grep -Fq "OPENING_DEAL_STALE_CALLBACK_RELEASED point=$point" "$client_log" \
			|| ! grep -Fq "OPENING_DEAL_MIGRATION_CLIENT_DONE point=$point" "$client_log" \
			|| grep -Fq "SCRIPT ERROR" "$client_log"; then
		echo "Opening-deal migration assertions failed: $point" >&2
		cat "$host_log" >&2 || true
		cat "$client_log" >&2 || true
		rm -rf "$tmp_dir"
		return 1
	fi
	rm -rf "$tmp_dir"
	echo "LAN_OPENING_DEAL_MIGRATION_CASE_OK $point"
}

run_case hands_start
run_case hands_mid
run_case piles_start
echo "LAN_OPENING_DEAL_MIGRATION_SMOKE_OK"
