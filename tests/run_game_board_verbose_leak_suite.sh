#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNNER="$ROOT_DIR/tests/run_verbose_smoke_with_leak_assertions.sh"

bash "$RUNNER" res://tests/AdventureUpgradeRunStartSmoke.gd ADVENTURE_UPGRADE_RUN_START_SMOKE_OK
bash "$RUNNER" res://tests/AdventureUpgradeOverlayDisplayFailureSmoke.gd ADVENTURE_UPGRADE_OVERLAY_DISPLAY_FAILURE_SMOKE_OK
bash "$RUNNER" res://tests/AdventureUpgradeVictoryDisplayFailureSmoke.gd ADVENTURE_UPGRADE_VICTORY_DISPLAY_FAILURE_SMOKE_OK
bash "$RUNNER" res://tests/SelectionOverlayControllerIsolationSmoke.gd SELECTION_OVERLAY_CONTROLLER_ISOLATION_SMOKE_OK

echo "GAME_BOARD_VERBOSE_LEAK_SUITE_OK"
