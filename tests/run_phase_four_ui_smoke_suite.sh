#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

bash "$ROOT_DIR/tests/run_adventure_upgrade_run_resume_smoke.sh"
bash "$ROOT_DIR/tests/run_adventure_upgrade_run_resume_localization_layout_smoke.sh"
bash "$ROOT_DIR/tests/run_adventure_tutorial_overlay_localization_layout_smoke.sh"
