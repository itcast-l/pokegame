#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
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

for variable in \
	CODEX_BUILD_ID \
	CODEX_STEAM_MIGRATION_ROLE \
	CODEX_STEAM_MIGRATION_SCENARIO \
	CODEX_STEAM_MIGRATION_PEER_LABEL \
	CODEX_STEAM_MIGRATION_EXPECTED_OUTCOME \
	CODEX_STEAM_MIGRATION_RESULT \
	CODEX_STEAM_MIGRATION_LOG; do
	if [[ -z "${!variable:-}" ]]; then
		echo "Missing $variable" >&2
		exit 1
	fi
done
if [[ "$CODEX_STEAM_MIGRATION_ROLE" == "peer" && -z "${CODEX_STEAM_LOBBY_ID:-}" ]]; then
	echo "Missing CODEX_STEAM_LOBBY_ID for peer role" >&2
	exit 1
fi

mkdir -p "$(dirname "$CODEX_STEAM_MIGRATION_RESULT")" "$(dirname "$CODEX_STEAM_MIGRATION_LOG")"
DECKLAND_TEST_MUTE_AUDIO=1 "$GODOT_CMD" \
	--headless \
	--audio-driver Dummy \
	--path "$ROOT_DIR" \
	--script res://tests/SteamHostMigrationManualPeer.gd \
	2>&1 | tee "$CODEX_STEAM_MIGRATION_LOG"
