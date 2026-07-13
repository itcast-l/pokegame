#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bash "$ROOT_DIR/tests/run_adventure_upgrade_interaction_smoke_case.sh" \
	res://tests/AdventureUpgradeContinueShortReturnCycleGuardSmoke.gd \
	ADVENTURE_UPGRADE_CONTINUE_SHORT_RETURN_CYCLE_GUARD_SMOKE_OK \
	adventure-upgrade-continue-short-return-cycle-guard
