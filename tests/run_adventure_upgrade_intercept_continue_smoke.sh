#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bash "$ROOT_DIR/tests/run_adventure_upgrade_interaction_smoke_case.sh" \
	res://tests/AdventureUpgradeInterceptContinueSmoke.gd \
	ADVENTURE_UPGRADE_INTERCEPT_CONTINUE_SMOKE_OK \
	adventure-upgrade-intercept-continue
