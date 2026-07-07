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
LOG_FILE="$TMP_DIR/localization-layout-audit.log"
LOCALIZATION_LAYOUT_SCENE_ALLOWLIST="$(cat <<'EOF'
res://scenes/main_menu_scene.tscn;res://scenes/main_menu_player_count_dialog.tscn;res://scenes/main_menu_settings_dialog.tscn;res://scenes/main_menu_controls_dialog.tscn;res://scenes/main_menu_exit_dialog.tscn;res://scenes/main_menu_avatar_entry.tscn;res://scenes/character_avatar_edit_scene.tscn;res://scenes/avatar_unlock_prompt.tscn;res://scenes/multiplayer_lobby_scene.tscn;res://scenes/multiplayer_lobby_room_row.tscn;res://scenes/multiplayer_lobby_player_row.tscn;res://scenes/game_board_result_banner.tscn;res://scenes/pause_menu.tscn;res://scenes/item_selection_overlay.tscn;res://scenes/adventure_avatar_menu_scene.tscn;res://scenes/adventure_level_entry.tscn;res://scenes/adventure_mode_entry.tscn;res://scenes/adventure_upgrade_selection_overlay.tscn
EOF
)"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

set +e
LOCALIZATION_LAYOUT_SCENE_ALLOWLIST="$LOCALIZATION_LAYOUT_SCENE_ALLOWLIST" \
	"$GODOT_CMD" --headless --path "$ROOT_DIR" --script res://tests/LocalizationLayoutAudit.gd >"$LOG_FILE" 2>&1
GODOT_STATUS=$?
set -e

if [[ "$GODOT_STATUS" -ne 0 ]]; then
	cat "$LOG_FILE" >&2
	exit "$GODOT_STATUS"
fi

if grep -Fq "SCRIPT ERROR" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

if ! grep -Fq "LOCALIZATION_LAYOUT_AUDIT_OK" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

cat "$LOG_FILE"
