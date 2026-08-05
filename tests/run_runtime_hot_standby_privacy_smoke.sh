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

TMP_DIR="$(mktemp -d)"
codex_setup_godot_test_home "$TMP_DIR"
RESULT_DIR="$TMP_DIR/results"
mkdir -p "$RESULT_DIR"
HOST_LOG="$TMP_DIR/host.log"
CLIENT_1_LOG="$TMP_DIR/client_1.log"
CLIENT_2_LOG="$TMP_DIR/client_2.log"
PIDS=()

cleanup() {
	for pid in "${PIDS[@]:-}"; do
		kill "$pid" 2>/dev/null || true
	done
	for pid in "${PIDS[@]:-}"; do
		wait "$pid" 2>/dev/null || true
	done
	if [[ -n "$TMP_DIR" && -d "$TMP_DIR" ]]; then
		rm -rf -- "$TMP_DIR"
	fi
}
trap cleanup EXIT

print_peer_logs() {
	local label
	local log_path
	for label in host client_1 client_2; do
		case "$label" in
			host) log_path="$HOST_LOG" ;;
			client_1) log_path="$CLIENT_1_LOG" ;;
			client_2) log_path="$CLIENT_2_LOG" ;;
		esac
		echo "===== $label log =====" >&2
		if [[ -f "$log_path" ]]; then
			cat "$log_path" >&2
		else
			echo "<log not created>" >&2
		fi
	done
}

fail_with_logs() {
	echo "Runtime hot-standby privacy smoke failed: $1" >&2
	print_peer_logs
	exit 1
}

run_peer() {
	local script_path="$1"
	local log_path="$2"
	shift 2
	CODEX_LAN_MIGRATION_FIXTURE=1 DECKLAND_TEST_MUTE_AUDIO=1 "$GODOT_CMD" --headless --audio-driver Dummy --path "$ROOT_DIR" --script "$script_path" -- "$@" >"$log_path" 2>&1 &
	PIDS+=("$!")
}

run_peer res://tests/RuntimeHotStandbyPrivacyHost.gd "$HOST_LOG" "--result-dir=$RESULT_DIR"
HOST_PID="${PIDS[0]}"
READY_DEADLINE=$((SECONDS + 15))
while [[ ! -f "$RESULT_DIR/host.ready" ]]; do
	if ! kill -0 "$HOST_PID" 2>/dev/null; then
		HOST_STATUS=0
		wait "$HOST_PID" || HOST_STATUS=$?
		fail_with_logs "host exited before readiness (status=$HOST_STATUS)"
	fi
	if (( SECONDS >= READY_DEADLINE )); then
		fail_with_logs "host readiness marker was not written within 15 seconds"
	fi
	sleep 0.05
done
run_peer res://tests/RuntimeHotStandbyPrivacyClient.gd "$CLIENT_1_LOG" "--seat=1" "--result-dir=$RESULT_DIR"
run_peer res://tests/RuntimeHotStandbyPrivacyClient.gd "$CLIENT_2_LOG" "--seat=2" "--result-dir=$RESULT_DIR"

statuses=()
for pid in "${PIDS[@]}"; do
	status=0
	wait "$pid" || status=$?
	statuses+=("$status")
done
if [[ "${statuses[*]}" != "0 0 0" ]]; then
	fail_with_logs "one or more peers exited non-zero (${statuses[*]})"
fi

value() {
	awk -F= -v key="$2" '$1 == key { print substr($0, index($0, "=") + 1); exit }' "$1"
}

require_result_file() {
	local file_path="$1"
	shift
	if [[ ! -s "$file_path" ]]; then
		fail_with_logs "missing or empty result file: $file_path"
	fi
	local required_key
	for required_key in "$@"; do
		if ! awk -F= -v key="$required_key" '
			$1 == key { count++; if (length(substr($0, index($0, "=") + 1)) > 0) nonempty = 1 }
			END { exit(count == 1 && nonempty ? 0 : 1) }
		' "$file_path"; then
			fail_with_logs "result file lacks exactly one non-empty $required_key field: $file_path"
		fi
	done
}

require_result_file "$RESULT_DIR/host.result" first_candidate second_candidate artifacts

FIRST_CANDIDATE="$(value "$RESULT_DIR/host.result" first_candidate)"
SECOND_CANDIDATE="$(value "$RESULT_DIR/host.result" second_candidate)"
if [[ ! "$FIRST_CANDIDATE" =~ ^[12]$ ]] || [[ ! "$SECOND_CANDIDATE" =~ ^[12]$ ]] || [[ "$FIRST_CANDIDATE" == "$SECOND_CANDIDATE" ]] || [[ "$(value "$RESULT_DIR/host.result" artifacts)" != "false" ]]; then
	fail_with_logs "host result validation failed"
fi

NON_CANDIDATE=1
if [[ "$FIRST_CANDIDATE" == "1" ]]; then
	NON_CANDIDATE=2
fi
FIRST_PHASE="$RESULT_DIR/phase1_$FIRST_CANDIDATE.result"
NON_FIRST_PHASE="$RESULT_DIR/phase1_$NON_CANDIDATE.result"
SECOND_PHASE="$RESULT_DIR/phase2_$SECOND_CANDIDATE.result"
REVOKED_PHASE="$RESULT_DIR/phase2_$FIRST_CANDIDATE.result"

for result_file in \
	"$RESULT_DIR/phase1_1.result" \
	"$RESULT_DIR/phase1_2.result" \
	"$RESULT_DIR/phase2_1.result" \
	"$RESULT_DIR/phase2_2.result" \
	"$RESULT_DIR/phase3_1.result" \
	"$RESULT_DIR/phase3_2.result"; do
	require_result_file \
		"$result_file" \
		seat \
		cache_count \
		checkpoint_id \
		checkpoint_fingerprint \
		key_fingerprint \
		phase1_key_fingerprint
done

if (( $(value "$FIRST_PHASE" cache_count) < 1 )) || [[ "$(value "$FIRST_PHASE" checkpoint_id)" == "-1" ]] || [[ "$(value "$FIRST_PHASE" checkpoint_fingerprint)" == "0" ]] || [[ "$(value "$NON_FIRST_PHASE" cache_count)" != "0" ]] || (( $(value "$SECOND_PHASE" cache_count) < 1 )) || [[ "$(value "$REVOKED_PHASE" cache_count)" != "0" ]] || [[ "$(value "$REVOKED_PHASE" checkpoint_id)" != "-1" ]] || [[ "$(value "$REVOKED_PHASE" checkpoint_fingerprint)" != "0" ]] || [[ "$(value "$REVOKED_PHASE" key_fingerprint)" == "$(value "$REVOKED_PHASE" phase1_key_fingerprint)" ]]; then
	fail_with_logs "candidate rotation privacy assertions failed"
fi

for seat in 1 2; do
	PHASE_THREE="$RESULT_DIR/phase3_$seat.result"
	if [[ "$(value "$PHASE_THREE" cache_count)" != "0" ]] || [[ "$(value "$PHASE_THREE" checkpoint_id)" != "-1" ]] || [[ "$(value "$PHASE_THREE" checkpoint_fingerprint)" != "0" ]] || [[ "$(value "$PHASE_THREE" key_fingerprint)" != "0" ]]; then
		fail_with_logs "session-close privacy assertions failed for seat $seat"
	fi
done

if ! grep -Fq "RUNTIME_HOT_STANDBY_PRIVACY_HOST_OK" "$HOST_LOG"; then
	fail_with_logs "host success marker missing"
fi
if ! grep -Fq "RUNTIME_HOT_STANDBY_PRIVACY_CLIENT_OK" "$CLIENT_1_LOG"; then
	fail_with_logs "first client process success marker missing"
fi
if ! grep -Fq "RUNTIME_HOT_STANDBY_PRIVACY_CLIENT_OK" "$CLIENT_2_LOG"; then
	fail_with_logs "second client process success marker missing"
fi
if ! grep -Fq "RUNTIME_HOT_STANDBY_PRIVACY_CLIENT_OK seat=1" "$CLIENT_1_LOG" "$CLIENT_2_LOG"; then
	fail_with_logs "client 1 success marker missing"
fi
if ! grep -Fq "RUNTIME_HOT_STANDBY_PRIVACY_CLIENT_OK seat=2" "$CLIENT_1_LOG" "$CLIENT_2_LOG"; then
	fail_with_logs "client 2 success marker missing"
fi
cat "$HOST_LOG" "$CLIENT_1_LOG" "$CLIENT_2_LOG"
echo "RUNTIME_HOT_STANDBY_PRIVACY_SMOKE_OK"
