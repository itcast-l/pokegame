#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tests/godot_test_env.sh"
DEFAULT_GODOT_BIN="/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot"

if [[ -n "${GODOT_BIN:-}" ]]; then
	GODOT_CMD="$GODOT_BIN"
elif command -v Godot >/dev/null 2>&1; then
	GODOT_CMD="$(command -v Godot)"
elif [[ -x "$DEFAULT_GODOT_BIN" ]]; then
	GODOT_CMD="$DEFAULT_GODOT_BIN"
else
	echo "Missing Godot binary. Set GODOT_BIN." >&2
	exit 1
fi

TMP_DIR="$(mktemp -d)"
codex_setup_godot_test_home "$TMP_DIR"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

run_step() {
	local label="$1"
	shift
	echo "== $label =="
	"$@"
}

run_step "scene smoke" \
	"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/GodotSceneSmoke.gd
run_step "game board mode regression smoke" \
	bash "$ROOT_DIR/tests/run_game_board_mode_regression_smoke.sh"
run_step "lan basic turn sync smoke" \
	bash "$ROOT_DIR/tests/run_lan_basic_turn_sync_smoke.sh"
run_step "lan inactive human turn skip smoke" \
	bash "$ROOT_DIR/tests/run_lan_inactive_human_turn_skip_smoke.sh"
run_step "lan reconnect smoke" \
	bash "$ROOT_DIR/tests/run_lan_reconnect_smoke.sh"
run_step "lan own-turn reconnect smoke" \
	bash "$ROOT_DIR/tests/run_lan_own_turn_reconnect_smoke.sh"
run_step "lan reconnect during visual smoke" \
	bash "$ROOT_DIR/tests/run_lan_reconnect_during_visual_smoke.sh"
run_step "lan reclaim pending smoke" \
	bash "$ROOT_DIR/tests/run_lan_reclaim_pending_smoke.sh"
run_step "lan host ended match smoke" \
	bash "$ROOT_DIR/tests/run_lan_host_ended_match_smoke.sh"
run_step "lan no-reconnect until match end smoke" \
	bash "$ROOT_DIR/tests/run_lan_no_reconnect_until_match_end_smoke.sh"
run_step "lan rematch return-to-lobby smoke" \
	bash "$ROOT_DIR/tests/run_lan_rematch_return_to_lobby_smoke.sh"
run_step "lan rematch offline-seat smoke" \
	bash "$ROOT_DIR/tests/run_lan_rematch_offline_seat_smoke.sh"
run_step "lan three-player disconnect smoke" \
	bash "$ROOT_DIR/tests/run_lan_three_player_disconnect_smoke.sh"
run_step "lan pending discard smoke" \
	bash "$ROOT_DIR/tests/run_lan_pending_discard_smoke.sh"
run_step "lan pending discard fallback smoke" \
	bash "$ROOT_DIR/tests/run_lan_pending_discard_fallback_smoke.sh"

echo "MULTIPLAYER_RECONNECT_SMOKE_SUITE_OK"
