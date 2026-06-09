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

HOLD_SECONDS="${CODEX_VISUAL_MANUAL_HOLD_SECONDS:-5}"
HOST_HOLD_SECONDS="${CODEX_VISUAL_HOST_HOLD_SECONDS:-12}"
CAPTURE_DIR="${CODEX_VISUAL_CAPTURE_DIR:-}"

CODEX_VISUAL_HOST_HOLD_SECONDS="$HOST_HOLD_SECONDS" \
CODEX_VISUAL_MANUAL_HOLD_SECONDS="$HOLD_SECONDS" \
CODEX_VISUAL_CAPTURE_DIR="$CAPTURE_DIR" \
	"$GODOT_CMD" --path "$ROOT_DIR" --script res://tests/LanReconnectDuringVisualHost.gd &
HOST_PID=$!

sleep 1

CODEX_VISUAL_HOST_HOLD_SECONDS="$HOST_HOLD_SECONDS" \
CODEX_VISUAL_MANUAL_HOLD_SECONDS="$HOLD_SECONDS" \
CODEX_VISUAL_CAPTURE_DIR="$CAPTURE_DIR" \
	"$GODOT_CMD" --path "$ROOT_DIR" --script res://tests/LanReconnectDuringVisualClient.gd &
CLIENT_PID=$!

wait "$HOST_PID"
wait "$CLIENT_PID"
