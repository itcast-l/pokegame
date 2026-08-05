#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$ROOT_DIR/tests/run_lan_host_ended_match_smoke.sh"
echo "SAFE_SESSION_LEAVE_SMOKE_OK"
