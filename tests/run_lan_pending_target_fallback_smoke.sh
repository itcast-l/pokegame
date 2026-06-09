#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export CODEX_FORCE_AI_FALLBACK=1

bash "$ROOT_DIR/tests/run_lan_pending_target_smoke.sh"
