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
LOG_FILE="$TMP_DIR/vertical_backdrop_authoring_preview_smoke.log"
OUTPUT_PATH="$TMP_DIR/vertical-backdrop-authoring-guide-1080x720.png"

cleanup() {
	rm -rf "$TMP_DIR"
}

trap cleanup EXIT

GODOT_ARGS=(--path "$ROOT_DIR" --script res://tests/VerticalBackdropAuthoringPreview.gd)
if [[ "${CODEX_VERTICAL_BACKDROP_AUTHORING_HEADLESS:-0}" != "0" ]]; then
	GODOT_ARGS=(--headless "${GODOT_ARGS[@]}")
fi

set +e
CODEX_VERTICAL_BACKDROP_GUIDE_OUTPUT="$OUTPUT_PATH" \
	CODEX_VERTICAL_BACKDROP_KEEP_RUNTIME_SCRIPT="1" \
	"$GODOT_CMD" "${GODOT_ARGS[@]}" >"$LOG_FILE" 2>&1
GODOT_STATUS=$?
set -e

if [[ "$GODOT_STATUS" -ne 0 ]]; then
	cat "$LOG_FILE" >&2
	exit "$GODOT_STATUS"
fi

if grep -Eq "SCRIPT ERROR|^ERROR:|Failed to load resource|No loader found" "$LOG_FILE"; then
	cat "$LOG_FILE" >&2
	exit 1
fi

grep -Fq "VERTICAL_BACKDROP_AUTHORING_PREVIEW_OK" "$LOG_FILE" || { cat "$LOG_FILE" >&2; exit 1; }
[[ -f "$OUTPUT_PATH" ]] || { cat "$LOG_FILE" >&2; echo "Missing guide output: $OUTPUT_PATH" >&2; exit 1; }

IMAGE_INFO="$(file "$OUTPUT_PATH")"
echo "$IMAGE_INFO"
grep -Fq "1080 x 720" <<<"$IMAGE_INFO" || { cat "$LOG_FILE" >&2; echo "$IMAGE_INFO" >&2; exit 1; }

cat "$LOG_FILE"
