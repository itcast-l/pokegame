#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNNER="$ROOT_DIR/tests/run_verbose_smoke_with_leak_assertions.sh"

bash "$RUNNER" res://tests/AdventureUpgradeRunResumeSmoke.gd ADVENTURE_UPGRADE_RUN_RESUME_SMOKE_OK
bash "$RUNNER" res://tests/AdventureUpgradeRunResumeLocalizationLayoutSmoke.gd ADVENTURE_RUN_RESUME_LOCALIZATION_LAYOUT_SMOKE_OK
bash "$RUNNER" res://tests/AdventureTutorialOverlayLocalizationLayoutSmoke.gd ADVENTURE_TUTORIAL_OVERLAY_LOCALIZATION_LAYOUT_SMOKE_OK

echo "PHASE_FOUR_UI_VERBOSE_LEAK_SUITE_OK"
