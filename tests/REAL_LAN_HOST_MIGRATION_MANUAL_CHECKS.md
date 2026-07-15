# Real LAN Host Migration Manual Check

This check is the final P0 evidence that cannot be produced by the local
multi-process smoke suite. It requires two separate physical devices on the
same LAN, both using the same repository revision and Godot `4.6.1` Mono.

Use a real IPv4 address of the Host device. Do not use `127.0.0.1`, and do not
use two interfaces of the same computer as the two devices. ENet uses UDP
port `37146` for both the initial LAN match and the migration server; allow
that port through both device firewalls.

## Host Device

Run this from the repository root on the Host device. Set `HOST_IP` to the
Host device's LAN address and choose a directory whose contents can be copied
out as evidence.

```bash
set -euo pipefail
ROOT_DIR="$PWD"
GODOT_BIN="/path/to/Godot_mono"
HOST_IP="192.168.11.61"
LOG_DIR="/tmp/deckland-p0-real-lan-host"
RESULT_DIR="$LOG_DIR/results"
mkdir -p "$RESULT_DIR"
rm -f "$LOG_DIR/host.log"
HOST_PID=""
cleanup() {
  if [[ -n "$HOST_PID" ]] && kill -0 "$HOST_PID" 2>/dev/null; then
    kill "$HOST_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

CODEX_LAN_MIGRATION_ADDRESS="$HOST_IP" \
CODEX_LAN_MATCH_SEED=20260714 \
CODEX_TEST_STABLE_USER_ID=real-lan-host \
"$GODOT_BIN" --headless --path "$ROOT_DIR" \
  --script res://tests/LanFullMatchPeer.gd -- \
  --role=host --seat=0 --players=2 --item-mode=0 \
  --result-dir="$RESULT_DIR" >"$LOG_DIR/host.log" 2>&1 &
HOST_PID=$!

deadline=$((SECONDS + 180))
reached_plan=0
while (( SECONDS < deadline )); do
  if ! kill -0 "$HOST_PID" 2>/dev/null; then
    cat "$LOG_DIR/host.log"
    exit 1
  fi
  if grep -Fq "FULL_MATCH_HOST_START_MATCH" "$LOG_DIR/host.log" \
    && grep -Fq "STANDBY_ACKED" "$LOG_DIR/host.log" \
    && grep -Fq "MIGRATION_PLAN_READY" "$LOG_DIR/host.log"; then
    reached_plan=1
    break
  fi
  sleep 0.25
done

if (( reached_plan == 0 )); then
  cat "$LOG_DIR/host.log"
  kill "$HOST_PID" 2>/dev/null || true
  wait "$HOST_PID" 2>/dev/null || true
  exit 1
fi
kill -9 "$HOST_PID"
wait "$HOST_PID" 2>/dev/null || true
printf '%s\n' "REAL_LAN_HOST_KILL_9 pid=$HOST_PID" | tee -a "$LOG_DIR/host.log"
```

The Host log must contain `FULL_MATCH_HOST_START_MATCH`, `STANDBY_ACKED`,
`MIGRATION_PLAN_READY`, and `REAL_LAN_HOST_KILL_9`. It must not contain a
normal `FULL_MATCH_HOST_DONE` marker.

## Client Device

Start this on the second physical device after the Host process is listening,
using the Host device's LAN address. The process must be allowed to continue
until it exits after the migrated match reaches a winner.

```bash
set -euo pipefail
ROOT_DIR="$PWD"
GODOT_BIN="/path/to/Godot_mono"
HOST_IP="192.168.11.61"
LOG_DIR="/tmp/deckland-p0-real-lan-client"
RESULT_DIR="$LOG_DIR/results"
mkdir -p "$RESULT_DIR"
rm -f "$LOG_DIR/client.log"

CODEX_LAN_MIGRATION_ADDRESS="$HOST_IP" \
CODEX_LAN_MATCH_SEED=20260714 \
CODEX_TEST_STABLE_USER_ID=real-lan-client \
"$GODOT_BIN" --headless --path "$ROOT_DIR" \
  --script res://tests/LanFullMatchPeer.gd -- \
  --role=client --seat=1 --players=2 --item-mode=0 --host="$HOST_IP" \
  --result-dir="$RESULT_DIR" \
  --allow-client-result-without-host --require-all-migration-peers \
  2>&1 | tee "$LOG_DIR/client.log"
```

The Client log must contain all of the following:

```text
MIGRATION_PLAN_ACCEPTED
MIGRATION_STATE_ENTERED role=client
MIGRATION_HOST_BOUND seat=1
MIGRATION_CHECKPOINT_RESTORED
MIGRATION_RECOVERY_READY
FULL_MATCH_CLIENT_STATE 7
FULL_MATCH_CLIENT_WINNER
FULL_MATCH_CLIENT_DONE
```

Finish the evidence check on the Client device:

```bash
grep -Fq "MIGRATION_HOST_BOUND seat=1" "$LOG_DIR/client.log"
grep -Fq "MIGRATION_CHECKPOINT_RESTORED" "$LOG_DIR/client.log"
grep -Fq "FULL_MATCH_CLIENT_WINNER" "$LOG_DIR/client.log"
grep -Fq "FULL_MATCH_CLIENT_DONE" "$LOG_DIR/client.log"
grep -Fq "private_leak=false" "$RESULT_DIR/client_1.result"
grep -Eq '^winner=[0-9]+$' "$RESULT_DIR/client_1.result"
```

Copy both log directories and `client_1.result` into the review artifact. The
P0 plan may only mark the real-device gate complete after these markers are
present from two physically separate devices.
