#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bash "$ROOT_DIR/tests/run_lan_full_match_2p_standard_smoke.sh"
bash "$ROOT_DIR/tests/run_lan_full_match_3p_items_smoke.sh"
bash "$ROOT_DIR/tests/run_lan_full_match_4p_items_smoke.sh"
echo "LAN_FULL_MATCH_SMOKE_SUITE_OK"
