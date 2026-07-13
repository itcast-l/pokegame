#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

run_step() {
	local label="$1"
	local script="$2"
	echo "== $label =="
	bash "$ROOT_DIR/tests/$script"
}

run_step "catalog" run_adventure_suit_upgrade_catalog_smoke.sh
run_step "extra base action" run_adventure_upgrade_extra_base_action_smoke.sh
run_step "primary root return" run_adventure_upgrade_clubs_return_smoke.sh
run_step "short branch return" run_adventure_upgrade_clubs_short_return_smoke.sh
run_step "continue/primary root cycle guard" run_adventure_upgrade_continue_primary_root_cycle_guard_smoke.sh
run_step "continue/short return cycle guard" run_adventure_upgrade_continue_short_return_cycle_guard_smoke.sh
run_step "intercept then continue" run_adventure_upgrade_intercept_continue_smoke.sh
run_step "real turn scope" run_adventure_upgrade_real_turn_scope_smoke.sh
run_step "gap rooting" run_adventure_upgrade_clubs_gap_rooting_smoke.sh
run_step "climb canopy" run_adventure_upgrade_clubs_climb_canopy_smoke.sh
run_step "crown canopy" run_adventure_upgrade_clubs_crown_canopy_smoke.sh
run_step "attached capture context" run_adventure_upgrade_attached_capture_context_smoke.sh
run_step "complete capture context" run_adventure_upgrade_complete_capture_context_smoke.sh
run_step "rejudge once" run_adventure_upgrade_rejudge_once_smoke.sh
run_step "question reward timing" run_adventure_upgrade_question_reward_timing_smoke.sh
run_step "spades pending" run_adventure_upgrade_spades_pending_smoke.sh
run_step "battle scope reset" run_adventure_upgrade_battle_scope_reset_smoke.sh
run_step "big capture" run_adventure_upgrade_big_capture_smoke.sh
run_step "big capture victory order" run_adventure_upgrade_big_capture_victory_order_smoke.sh
run_step "run resume" run_adventure_upgrade_run_resume_smoke.sh
run_step "snapshot v5 migration" run_adventure_upgrade_snapshot_v5_migration_smoke.sh

echo "ADVENTURE_UPGRADE_INTERACTION_SMOKE_SUITE_OK"
