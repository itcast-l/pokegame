#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
export CODEX_LAN_SKIP_MIGRATION_PORT_PROBE=1
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
	local case_name="$1"
	local tmp_dir
	tmp_dir="$(mktemp -d)"
	codex_setup_godot_test_home "$tmp_dir"
	local result_path="$tmp_dir/result.txt"
	local host_log="$tmp_dir/host.log"
	local client_log="$tmp_dir/client.log"
	CODEX_TEST_STABLE_USER_ID="selection-$case_name-seat-0" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanRequiredSelectionFallbackHost.gd -- \
		--case="$case_name" --result-path="$result_path" >"$host_log" 2>&1 &
	local host_pid=$!
	sleep 1
	CODEX_TEST_STABLE_USER_ID="selection-$case_name-seat-1" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
		--script res://tests/LanRequiredSelectionFallbackClient.gd -- \
		--case="$case_name" --result-path="$result_path" >"$client_log" 2>&1 &
	local client_pid=$!
	local status=0
	wait "$client_pid" || status=$?
	wait "$host_pid" || status=$?
	if [[ "$status" -ne 0 ]] \
			|| [[ ! -s "$result_path" ]] \
			|| ! grep -Fq "REQUIRED_SELECTION_HOST_DONE case=$case_name" "$host_log" \
			|| ! grep -Fq "REQUIRED_SELECTION_CLIENT_DONE case=$case_name" "$client_log" \
			|| grep -Fq "SCRIPT ERROR" "$host_log" \
			|| grep -Fq "SCRIPT ERROR" "$client_log"; then
		echo "Required selection fallback case failed: $case_name" >&2
		cat "$host_log" >&2 || true
		cat "$client_log" >&2 || true
		rm -rf "$tmp_dir"
		return 1
	fi
	if [[ "$case_name" == "zero_candidate" ]]; then
		grep -Fq "client_action_id=0" "$result_path"
		grep -Fq "final=0" "$result_path"
		if grep -Fq "outcome=InProgress" "$result_path"; then
			echo "Zero-candidate case did not rerun match outcome" >&2
			cat "$result_path" >&2
			return 1
		fi
	else
		grep -Fq "client_action_id=1" "$result_path"
		local initial final
		initial="$(sed -n 's/^initial=//p' "$result_path")"
		final="$(sed -n 's/^final=//p' "$result_path")"
		if (( final != initial - 1 )); then
			echo "Remote fallback did not submit exactly one selection: $case_name" >&2
			cat "$result_path" >&2
			return 1
		fi
	fi
	rm -rf "$tmp_dir"
	echo "LAN_REQUIRED_SELECTION_FALLBACK_CASE_OK $case_name"
}

run_case resource_failure
run_case instantiation_failure
run_case unexpected_release
run_case zero_candidate
echo "LAN_REQUIRED_SELECTION_FALLBACK_SMOKE_OK"
